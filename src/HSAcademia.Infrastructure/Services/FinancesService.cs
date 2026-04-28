using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Finances;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

public class FinancesService
{
    private readonly AppDbContext _context;

    public FinancesService(AppDbContext context)
    {
        _context = context;
    }

    // ─────────────────────────────────────────────────────────────
    // Configuration
    // ─────────────────────────────────────────────────────────────
    public async Task<FinancialConfigDto> GetConfigAsync(Guid academyId)
    {
        var config = await _context.AcademyFinancialConfigs.FirstOrDefaultAsync(c => c.AcademyId == academyId);
        if (config == null)
        {
            config = new AcademyFinancialConfig { AcademyId = academyId, DefaultPaymentDay = 5 };
            _context.AcademyFinancialConfigs.Add(config);
            await _context.SaveChangesAsync();
        }
        return new FinancialConfigDto { DefaultPaymentDay = config.DefaultPaymentDay };
    }

    public async Task<FinancialConfigDto> UpdateConfigAsync(Guid academyId, UpdateFinancialConfigDto dto)
    {
        var config = await _context.AcademyFinancialConfigs.FirstOrDefaultAsync(c => c.AcademyId == academyId);
        if (config == null)
        {
            config = new AcademyFinancialConfig { AcademyId = academyId, DefaultPaymentDay = dto.DefaultPaymentDay };
            _context.AcademyFinancialConfigs.Add(config);
        }
        else
        {
            config.DefaultPaymentDay = dto.DefaultPaymentDay;
        }

        // Update pending debts for the current month
        var today = DateTime.UtcNow;
        var pendingDebts = await _context.PaymentRecords
            .Where(p => p.AcademyId == academyId && !p.IsPaid && !p.IsDeleted && p.DueDate.Month == today.Month && p.DueDate.Year == today.Year)
            .ToListAsync();

        foreach (var debt in pendingDebts)
        {
            var daysInMonth = DateTime.DaysInMonth(debt.DueDate.Year, debt.DueDate.Month);
            var newDay = Math.Min(dto.DefaultPaymentDay, daysInMonth);
            debt.DueDate = new DateTime(debt.DueDate.Year, debt.DueDate.Month, newDay, 0, 0, 0, DateTimeKind.Utc);
        }

        await _context.SaveChangesAsync();
        return new FinancialConfigDto { DefaultPaymentDay = config.DefaultPaymentDay };
    }

    // ─────────────────────────────────────────────────────────────
    // Contar sesiones de entrenamiento en el calendario
    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Counts Training events for a category (or academy-wide if no category)
    /// in a given year/month. Used for session-based proration.
    /// Returns (totalSessions, sessionsFromDate).
    /// </summary>
    private async Task<(int total, int fromDate)> GetSessionCountsAsync(
        Guid academyId, Guid? categoryId, int year, int month, DateTime? fromDate)
    {
        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = from.AddMonths(1);

        var query = _context.Events
            .Where(e => e.AcademyId == academyId
                     && !e.IsDeleted
                     && e.Type == EventType.Training
                     && e.StartTime >= from
                     && e.StartTime < to);

        // Match category or events without category (academy-wide trainings)
        if (categoryId.HasValue)
            query = query.Where(e => e.CategoryId == categoryId || e.CategoryId == null);

        var sessions = await query.Select(e => e.StartTime).ToListAsync();
        int total    = sessions.Count;
        int after    = fromDate.HasValue
            ? sessions.Count(s => s.Date >= fromDate.Value.Date)
            : total;

        return (total, after);
    }

    // ─────────────────────────────────────────────────────────────
    // Motor de Generación de Deudas (prorrateo por sesiones)
    // ─────────────────────────────────────────────────────────────
    public async Task<int> GenerateMonthlyDebtsAsync(Guid academyId)
    {
        var config = await GetConfigAsync(academyId);
        var today  = DateTime.UtcNow;

        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        var dueDayNum   = Math.Min(config.DefaultPaymentDay, daysInMonth);
        var dueDate     = new DateTime(today.Year, today.Month, dueDayNum, 0, 0, 0, DateTimeKind.Utc);

        var students = await _context.Students
            .Include(s => s.Category)
            .Where(s => s.AcademyId == academyId && s.IsActive && !s.IsGuest && !s.IsScholarship)
            .ToListAsync();

        int generatedCount = 0;

        foreach (var student in students)
        {
            if (student.PaymentStartDate.HasValue && student.PaymentStartDate.Value.ToUniversalTime() > today)
                continue;

            string description = $"Mensualidad {today:MMMM yyyy}";

            var existingDebt = await _context.PaymentRecords.FirstOrDefaultAsync(p =>
                p.AcademyId == academyId &&
                p.StudentId == student.Id &&
                p.Type == PaymentType.MonthlyFee &&
                p.Description == description);

            if (existingDebt != null) continue;

            var fullAmount = student.PreferentialFee ?? student.Category?.MonthlyFee ?? 0m;
            if (fullAmount <= 0) continue;

            // ── Determine start date for proration ───────────────
            DateTime? startForProration = null;
            var enrollUtc = student.EnrollmentDate.Kind == DateTimeKind.Utc
                ? student.EnrollmentDate
                : student.EnrollmentDate.ToUniversalTime();

            if (enrollUtc.Year == today.Year && enrollUtc.Month == today.Month && enrollUtc.Day > 1)
                startForProration = enrollUtc;
            else if (student.PaymentStartDate.HasValue)
            {
                var ps = student.PaymentStartDate.Value.ToUniversalTime();
                if (ps.Year == today.Year && ps.Month == today.Month && ps.Day > 1)
                    startForProration = ps;
            }

            // ── Session-based proration ──────────────────────────
            bool isProrated       = false;
            decimal chargedAmount = fullAmount;
            DateTime? proratedStart = null;
            int? totalSessions = null, sessionsCharged = null;

            if (startForProration.HasValue)
            {
                var (total, fromDate) = await GetSessionCountsAsync(
                    academyId, student.CategoryId, today.Year, today.Month, startForProration);

                if (total > 0)
                {
                    // Session-based proration
                    chargedAmount  = Math.Round(fullAmount * fromDate / total, 2);
                    isProrated     = true;
                    proratedStart  = new DateTime(today.Year, today.Month,
                        startForProration.Value.Day, 0, 0, 0, DateTimeKind.Utc);
                    totalSessions    = total;
                    sessionsCharged  = fromDate;
                }
                else
                {
                    // No sessions in calendar → fall back to day-based proration
                    int startDay   = startForProration.Value.Day;
                    int charged    = daysInMonth - startDay + 1;
                    chargedAmount  = Math.Round(fullAmount * charged / daysInMonth, 2);
                    isProrated     = true;
                    proratedStart  = new DateTime(today.Year, today.Month, startDay, 0, 0, 0, DateTimeKind.Utc);
                    totalSessions    = daysInMonth;  // store days as fallback
                    sessionsCharged  = charged;
                }
            }

            _context.PaymentRecords.Add(new PaymentRecord
            {
                AcademyId           = academyId,
                StudentId           = student.Id,
                Description         = description,
                Amount              = chargedAmount,
                AmountPaid          = 0m,
                DueDate             = dueDate,
                IsPaid              = false,
                Type                = PaymentType.MonthlyFee,
                IsProrated          = isProrated,
                ProratedStartDate   = proratedStart,
                ProratedTotalDays   = totalSessions,
                ProratedDaysCharged = sessionsCharged
            });
            generatedCount++;
        }

        if (generatedCount > 0)
            await _context.SaveChangesAsync();

        return generatedCount;
    }

    // ─────────────────────────────────────────────────────────────
    // Generar deuda del mes siguiente para un alumno específico
    // ─────────────────────────────────────────────────────────────
    public async Task<PaymentRecordDto> GenerateNextMonthDebtAsync(Guid academyId, Guid studentId)
    {
        var config  = await GetConfigAsync(academyId);
        var student = await _context.Students
            .Include(s => s.Category)
            .FirstOrDefaultAsync(s => s.Id == studentId && s.AcademyId == academyId)
            ?? throw new Exception("Alumno no encontrado.");

        // Next month relative to today
        var today     = DateTime.UtcNow;
        var nextMonth = today.AddMonths(1);
        int year      = nextMonth.Year;
        int month     = nextMonth.Month;

        string description = $"Mensualidad {new DateTime(year, month, 1):MMMM yyyy}";

        var existing = await _context.PaymentRecords.FirstOrDefaultAsync(p =>
            p.AcademyId == academyId &&
            p.StudentId == studentId &&
            p.Type == PaymentType.MonthlyFee &&
            p.Description == description);

        if (existing != null)
            throw new Exception($"Ya existe un cobro para {description}.");

        var fullAmount = student.PreferentialFee ?? student.Category?.MonthlyFee ?? 0m;
        if (fullAmount <= 0) throw new Exception("El alumno no tiene tarifa configurada.");

        var daysInMonth  = DateTime.DaysInMonth(year, month);
        var dueDayNum    = Math.Min(config.DefaultPaymentDay, daysInMonth);
        var dueDate      = new DateTime(year, month, dueDayNum, 0, 0, 0, DateTimeKind.Utc);

        // Next month is always full month (no proration needed)
        var record = new PaymentRecord
        {
            AcademyId           = academyId,
            StudentId           = studentId,
            Description         = description,
            Amount              = fullAmount,
            AmountPaid          = 0m,
            DueDate             = dueDate,
            IsPaid              = false,
            Type                = PaymentType.MonthlyFee,
            IsProrated          = false
        };

        _context.PaymentRecords.Add(record);
        await _context.SaveChangesAsync();

        // Reload with navigations
        var loaded = await _context.PaymentRecords
            .Include(p => p.Student).ThenInclude(s => s.Category)
            .Include(p => p.Installments)
            .FirstAsync(p => p.Id == record.Id);

        return MapToDto(loaded, today);
    }

    // ─────────────────────────────────────────────────────────────
    // Query debts
    // ─────────────────────────────────────────────────────────────
    public async Task<List<PaymentRecordDto>> GetPendingDebtsAsync(Guid academyId)
    {
        var today = DateTime.UtcNow;
        var debts = await _context.PaymentRecords
            .Include(p => p.Student).ThenInclude(s => s.Category)
            .Include(p => p.Installments)
            .Where(p => p.AcademyId == academyId && !p.IsPaid && !p.IsDeleted)
            .OrderBy(p => p.DueDate)
            .ToListAsync();
        return debts.Select(p => MapToDto(p, today)).ToList();
    }

    public async Task<List<PaymentRecordDto>> GetAllDebtsAsync(Guid academyId)
    {
        var today = DateTime.UtcNow;
        var debts = await _context.PaymentRecords
            .Include(p => p.Student).ThenInclude(s => s.Category)
            .Include(p => p.Installments)
            .Where(p => p.AcademyId == academyId && !p.IsDeleted)
            .OrderByDescending(p => p.DueDate)
            .ToListAsync();
        return debts.Select(p => MapToDto(p, today)).ToList();
    }

    public async Task<List<PaymentRecordDto>> GetMyDebtsAsync(Guid academyId, Guid guardianUserId)
    {
        var today = DateTime.UtcNow;
        var debts = await _context.PaymentRecords
            .Include(p => p.Student).ThenInclude(s => s.Category)
            .Include(p => p.Installments)
            .Where(p => p.AcademyId == academyId && p.Student.GuardianId == guardianUserId && !p.IsDeleted)
            .OrderBy(p => p.DueDate)
            .ToListAsync();
        return debts.Select(p => MapToDto(p, today)).ToList();
    }

    // ─────────────────────────────────────────────────────────────
    // Registrar pago (total o parcial) con medio + voucher
    // ─────────────────────────────────────────────────────────────
    public async Task<PaymentRecordDto> RegisterPaymentAsync(Guid academyId, RegisterPaymentDto dto)
    {
        var record = await _context.PaymentRecords
            .Include(p => p.Student).ThenInclude(s => s.Category)
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == dto.PaymentRecordId && p.AcademyId == academyId)
            ?? throw new Exception("Registro de pago no encontrado.");

        if (record.IsPaid)
            throw new Exception("Este cobro ya está completamente pagado.");
        if (dto.AmountPaid <= 0)
            throw new Exception("El monto a pagar debe ser mayor a cero.");

        var pending = record.Amount - record.AmountPaid;
        if (dto.AmountPaid > pending + 0.01m)
            throw new Exception($"El monto ingresado ({dto.AmountPaid:N2}) supera el saldo pendiente ({pending:N2}).");

        if (!Enum.TryParse<PaymentMethod>(dto.Method, true, out var method))
            method = PaymentMethod.Cash;

        _context.PaymentInstallments.Add(new PaymentInstallment
        {
            PaymentRecordId = record.Id,
            AmountPaid      = dto.AmountPaid,
            PaidAt          = DateTime.UtcNow,
            Method          = method,
            OperationNumber = dto.OperationNumber,
            VoucherUrl      = dto.VoucherUrl,
            Notes           = dto.Notes
        });

        record.AmountPaid += dto.AmountPaid;

        if (record.AmountPaid >= record.Amount - 0.01m)
        {
            record.AmountPaid = record.Amount;
            record.IsPaid     = true;
            record.PaidDate   = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return MapToDto(record, DateTime.UtcNow);
    }

    // ─────────────────────────────────────────────────────────────
    // Recalcular monto (prorrateo manual o importe libre)
    // Usa el día de cobro de la configuración para recalcular DueDate.
    // Si aún no se ha completado el pago, actualiza Amount y DueDate.
    // ─────────────────────────────────────────────────────────────
    public async Task<PaymentRecordDto> RecalculatePaymentAsync(Guid academyId, RecalculatePaymentDto dto)
    {
        var record = await _context.PaymentRecords
            .Include(p => p.Student).ThenInclude(s => s.Category)
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == dto.PaymentRecordId && p.AcademyId == academyId)
            ?? throw new Exception("Registro de pago no encontrado.");

        if (record.IsPaid)
            throw new Exception("No se puede recalcular un pago ya completado.");

        // ── Obtener configuración de la academia ──────────────────
        var config = await GetConfigAsync(academyId);

        // ── Recalcular DueDate con el día configurado ─────────────
        var dueRef       = record.DueDate;  // mes/año de referencia
        var daysInMonth  = DateTime.DaysInMonth(dueRef.Year, dueRef.Month);
        var configuredDay = Math.Min(config.DefaultPaymentDay, daysInMonth);
        record.DueDate   = new DateTime(dueRef.Year, dueRef.Month, configuredDay, 0, 0, 0, DateTimeKind.Utc);

        var fullAmount = record.Student.PreferentialFee ?? record.Student.Category?.MonthlyFee ?? 0m;

        if (dto.NewAmount.HasValue)
        {
            // Monto fijo manual: sin prorrateo
            record.Amount              = dto.NewAmount.Value;
            record.IsProrated          = false;
            record.ProratedStartDate   = null;
            record.ProratedTotalDays   = null;
            record.ProratedDaysCharged = null;
        }
        else if (dto.ProratedStartDate.HasValue)
        {
            // Prorrateo con fecha de inicio manual
            var startDay = dto.ProratedStartDate.Value.Day;
            var charged  = daysInMonth - startDay + 1;
            record.Amount              = Math.Round(fullAmount * charged / daysInMonth, 2);
            record.IsProrated          = true;
            record.ProratedStartDate   = dto.ProratedStartDate.Value.ToUniversalTime();
            record.ProratedTotalDays   = daysInMonth;
            record.ProratedDaysCharged = charged;
        }
        else
        {
            // Sin monto manual ni fecha: recalcular desde categoría + prorrateo automático
            // según EnrollmentDate o PaymentStartDate del alumno para el mes de referencia
            var student = record.Student;
            bool autoProrated = false;

            // Verificar EnrollmentDate
            var enrollUtc = student.EnrollmentDate.Kind == DateTimeKind.Utc
                ? student.EnrollmentDate
                : student.EnrollmentDate.ToUniversalTime();

            if (enrollUtc.Year == dueRef.Year && enrollUtc.Month == dueRef.Month && enrollUtc.Day > 1)
            {
                var charged  = daysInMonth - enrollUtc.Day + 1;
                record.Amount              = Math.Round(fullAmount * charged / daysInMonth, 2);
                record.IsProrated          = true;
                record.ProratedStartDate   = new DateTime(dueRef.Year, dueRef.Month, enrollUtc.Day, 0, 0, 0, DateTimeKind.Utc);
                record.ProratedTotalDays   = daysInMonth;
                record.ProratedDaysCharged = charged;
                autoProrated = true;
            }
            else if (student.PaymentStartDate.HasValue)
            {
                var psUtc = student.PaymentStartDate.Value.ToUniversalTime();
                if (psUtc.Year == dueRef.Year && psUtc.Month == dueRef.Month && psUtc.Day > 1)
                {
                    var charged  = daysInMonth - psUtc.Day + 1;
                    record.Amount              = Math.Round(fullAmount * charged / daysInMonth, 2);
                    record.IsProrated          = true;
                    record.ProratedStartDate   = new DateTime(dueRef.Year, dueRef.Month, psUtc.Day, 0, 0, 0, DateTimeKind.Utc);
                    record.ProratedTotalDays   = daysInMonth;
                    record.ProratedDaysCharged = charged;
                    autoProrated = true;
                }
            }

            if (!autoProrated)
            {
                // Mes completo: importe de categoría sin prorrateo
                record.Amount              = fullAmount;
                record.IsProrated          = false;
                record.ProratedStartDate   = null;
                record.ProratedTotalDays   = null;
                record.ProratedDaysCharged = null;
            }
        }

        // ── Ajuste de AmountPaid si supera el nuevo Amount ────────
        if (record.AmountPaid > record.Amount)
            record.AmountPaid = record.Amount;

        // ── Marcar como pagado si el monto abonado cubre el total ─
        if (record.AmountPaid >= record.Amount - 0.01m && record.Amount > 0)
        {
            record.IsPaid   = true;
            record.PaidDate = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return MapToDto(record, DateTime.UtcNow);
    }

    // ─────────────────────────────────────────────────────────────
    // Exonerar cobro (clase gratuita, recuperación, etc.)
    // ─────────────────────────────────────────────────────────────
    public async Task<PaymentRecordDto> ExcludePaymentAsync(Guid academyId, ExcludePaymentDto dto)
    {
        var record = await _context.PaymentRecords
            .Include(p => p.Student).ThenInclude(s => s.Category)
            .Include(p => p.Installments)
            .FirstOrDefaultAsync(p => p.Id == dto.PaymentRecordId && p.AcademyId == academyId)
            ?? throw new Exception("Registro de pago no encontrado.");

        if (!Enum.TryParse<ExclusionType>(dto.ExclusionType, true, out var excType))
            excType = ExclusionType.FreeClass;

        record.ExclusionType = excType;
        record.ExclusionNote = dto.ExclusionNote;
        record.Amount        = 0m;
        record.AmountPaid    = 0m;
        record.IsPaid        = true;
        record.PaidDate      = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(record, DateTime.UtcNow);
    }

    // ─────────────────────────────────────────────────────────────
    // Legacy quick-pay (full amount, cash)
    // ─────────────────────────────────────────────────────────────
    public async Task<PaymentRecordDto> MarkAsPaidAsync(Guid academyId, Guid paymentId)
    {
        var r = await _context.PaymentRecords
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.AcademyId == academyId)
            ?? throw new Exception("Registro no encontrado.");

        return await RegisterPaymentAsync(academyId, new RegisterPaymentDto
        {
            PaymentRecordId = paymentId,
            AmountPaid      = r.Amount - r.AmountPaid,
            Method          = "Cash"
        });
    }

    // ─────────────────────────────────────────────────────────────
    // Mobile: pago múltiple en app
    // ─────────────────────────────────────────────────────────────
    public async Task ProcessMobilePaymentAsync(Guid academyId, Guid guardianUserId, List<Guid> paymentIds)
    {
        if (paymentIds == null || !paymentIds.Any()) return;

        var records = await _context.PaymentRecords
            .Include(p => p.Student)
            .Where(p =>
                p.AcademyId == academyId &&
                paymentIds.Contains(p.Id) &&
                p.Student.GuardianId == guardianUserId &&
                !p.IsPaid)
            .ToListAsync();

        if (records.Count != paymentIds.Count)
            throw new Exception("Algunas deudas no fueron encontradas o ya están pagadas.");

        var now = DateTime.UtcNow;
        foreach (var record in records)
        {
            _context.PaymentInstallments.Add(new PaymentInstallment
            {
                PaymentRecordId = record.Id,
                AmountPaid      = record.Amount - record.AmountPaid,
                PaidAt          = now,
                Method          = PaymentMethod.Other,
                Notes           = "Pago móvil"
            });
            record.AmountPaid = record.Amount;
            record.IsPaid     = true;
            record.PaidDate   = now;
        }

        await _context.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Mapper
    // ─────────────────────────────────────────────────────────────
    private static PaymentRecordDto MapToDto(PaymentRecord p, DateTime today)
    {
        string status;
        int daysOverdue = 0;

        if (p.IsPaid)
            status = "Pagado";
        else if (today.Date > p.DueDate.Date)
        {
            daysOverdue = (today.Date - p.DueDate.Date).Days;
            status = "Vencido";
        }
        else
            status = "En Curso";

        return new PaymentRecordDto
        {
            Id           = p.Id,
            StudentId    = p.StudentId,
            StudentName  = p.Student.FirstName + " " + p.Student.LastName,
            CategoryName = p.Student.Category?.Name ?? "-",
            Description  = p.Description,
            Amount       = p.Amount,
            AmountPaid   = p.AmountPaid,
            DueDate      = p.DueDate,
            IsPaid       = p.IsPaid,
            PaidDate     = p.PaidDate,
            Type         = (int)p.Type,
            DaysOverdue  = daysOverdue,
            Status       = status,
            IsProrated           = p.IsProrated,
            ProratedStartDate    = p.ProratedStartDate,
            ProratedTotalDays    = p.ProratedTotalDays,
            ProratedDaysCharged  = p.ProratedDaysCharged,
            ExclusionType = p.ExclusionType.ToString(),
            ExclusionNote = p.ExclusionNote,
            Installments  = p.Installments.OrderBy(i => i.PaidAt).Select(i => new PaymentInstallmentDto
            {
                Id              = i.Id,
                AmountPaid      = i.AmountPaid,
                PaidAt          = i.PaidAt,
                Method          = i.Method.ToString(),
                OperationNumber = i.OperationNumber,
                VoucherUrl      = i.VoucherUrl,
                Notes           = i.Notes
            }).ToList()
        };
    }
}
