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
            config = new AcademyFinancialConfig { AcademyId = academyId, DefaultPaymentDay = 5, CurrentReceiptNumber = 43 };
            _context.AcademyFinancialConfigs.Add(config);
            await _context.SaveChangesAsync();
        }
        return new FinancialConfigDto { DefaultPaymentDay = config.DefaultPaymentDay };
    }

    public async Task<string> GenerateNextReceiptNumberAsync(Guid academyId)
    {
        var config = await _context.AcademyFinancialConfigs.FirstOrDefaultAsync(c => c.AcademyId == academyId);
        if (config == null)
        {
            config = new AcademyFinancialConfig { AcademyId = academyId, DefaultPaymentDay = 5, CurrentReceiptNumber = 43 };
            _context.AcademyFinancialConfigs.Add(config);
        }
        
        config.CurrentReceiptNumber++;
        await _context.SaveChangesAsync();
        
        return config.CurrentReceiptNumber.ToString().PadLeft(6, '0');
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
    private async Task<(int total, int activeSessions)> GetSessionCountsAsync(
        Guid academyId, Guid? categoryId, int year, int month, DateTime? fromDate, DateTime? toDate)
    {
        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = from.AddMonths(1);

        var query = _context.Events
            .Where(e => e.AcademyId == academyId
                     && !e.IsDeleted
                     && e.Type == EventType.Training
                     && e.StartTime >= from
                     && e.StartTime < to);

        if (categoryId.HasValue)
            query = query.Where(e => e.CategoryId == categoryId || e.CategoryId == null);

        var sessions = await query.Select(e => e.StartTime).ToListAsync();
        int total    = sessions.Count;
        
        var validSessions = sessions.AsEnumerable();
        if (fromDate.HasValue) validSessions = validSessions.Where(s => s.Date >= fromDate.Value.Date);
        if (toDate.HasValue)   validSessions = validSessions.Where(s => s.Date <= toDate.Value.Date);
        
        int active = validSessions.Count();

        return (total, active);
    }

    // ─────────────────────────────────────────────────────────────
    // Motor de Generación de Deudas (prorrateo por sesiones)
    // ─────────────────────────────────────────────────────────────
    public async Task<(int Generated, int Replaced, int Cleaned)> GenerateMonthlyDebtsAsync(
        Guid academyId, int? targetYear = null, int? targetMonth = null)
    {
        var config = await GetConfigAsync(academyId);
        var today  = DateTime.UtcNow;

        int year  = targetYear  ?? today.Year;
        int month = targetMonth ?? today.Month;

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var dueDayNum   = Math.Min(config.DefaultPaymentDay, daysInMonth);
        var dueDate     = new DateTime(year, month, dueDayNum, 0, 0, 0, DateTimeKind.Utc);
        var targetRef   = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        string description = $"Mensualidad {new DateTime(year, month, 1):MMMM yyyy}";

        var students = await _context.Students
            .Include(s => s.Category)
            .Where(s => s.AcademyId == academyId && 
                       (s.IsActive || 
                       (s.WithdrawalDate.HasValue && s.WithdrawalDate.Value >= targetRef)))
            .ToListAsync();

        int generatedCount = 0, replacedCount = 0, cleanedCount = 0;

        // ── STEP 1: Cleanup — soft-delete unpaid records that fall BEFORE ──
        //   each student's effective start month (PaymentStartDate ?? EnrollmentDate)
        var studentIds = students.Select(s => s.Id).ToList();
        var allUnpaidMonthly = await _context.PaymentRecords
            .Where(p => p.AcademyId == academyId
                     && !p.IsPaid && !p.IsDeleted
                     && p.Type == PaymentType.MonthlyFee
                     && studentIds.Contains(p.StudentId))
            .ToListAsync();

        foreach (var record in allUnpaidMonthly)
        {
            var student = students.FirstOrDefault(s => s.Id == record.StudentId);
            if (student == null) continue;

            var effStart = (student.PaymentStartDate.HasValue
                ? student.PaymentStartDate.Value
                : student.EnrollmentDate).ToUniversalTime();

            var effStartMonth  = new DateTime(effStart.Year,  effStart.Month,  1, 0, 0, 0, DateTimeKind.Utc);
            var recordMonth    = new DateTime(record.DueDate.Year, record.DueDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            if (recordMonth < effStartMonth)
            {
                record.IsDeleted  = true;
                record.DeletedAt  = DateTime.UtcNow;
                cleanedCount++;
            }
        }

        // ── STEP 2: Generate / replace for the target month ───────────────
        foreach (var student in students)
        {
            var effStart = (student.PaymentStartDate.HasValue
                ? student.PaymentStartDate.Value
                : student.EnrollmentDate).ToUniversalTime();

            var effStartMonth = new DateTime(effStart.Year, effStart.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            // Skip if target month is BEFORE the student's effective start month
            if (targetRef < effStartMonth) continue;

            var existingDebt = await _context.PaymentRecords
                .Include(p => p.Installments)
                .FirstOrDefaultAsync(p =>
                    p.AcademyId == academyId &&
                    p.StudentId == student.Id &&
                    p.Type == PaymentType.MonthlyFee &&
                    p.Description == description &&
                    !p.IsDeleted);

            // Paid → never overwrite (mes cerrado implícito)
            if (existingDebt != null && existingDebt.IsPaid) continue;

            // Unpaid existing → soft-delete to regenerate
            if (existingDebt != null)
            {
                existingDebt.IsDeleted = true;
                existingDebt.DeletedAt = DateTime.UtcNow;
                replacedCount++;
            }

            var fullAmount = student.PreferentialFee ?? student.Category?.MonthlyFee ?? 0m;
            if (fullAmount <= 0) continue;

            // ── Proration: only if student started THIS target month after day 1 ──
            DateTime? startForProration = null;
            if (effStart.Year == year && effStart.Month == month && effStart.Day > 1)
                startForProration = effStart;

            DateTime? endForProration = (!student.IsActive && student.WithdrawalDate.HasValue && 
                                          student.WithdrawalDate.Value.Year == year && 
                                          student.WithdrawalDate.Value.Month == month)
                ? student.WithdrawalDate.Value
                : null;

            bool isProrated = false;
            decimal chargedAmount = fullAmount;
            DateTime? proratedStart = null;
            int? totalSessions = null, sessionsCharged = null;

            if (startForProration.HasValue || endForProration.HasValue)
            {
                var (total, activeCount) = await GetSessionCountsAsync(
                    academyId, student.CategoryId, year, month, startForProration, endForProration);

                if (total > 0)
                {
                    chargedAmount   = Math.Round(fullAmount * activeCount / total, 2);
                    isProrated      = true;
                    proratedStart   = startForProration ?? targetRef;
                    totalSessions   = total;
                    sessionsCharged = activeCount;
                }
                else
                {
                    int startDay    = startForProration?.Day ?? 1;
                    int endDay      = endForProration?.Day ?? daysInMonth;
                    int charged     = Math.Max(0, endDay - startDay + 1);
                    chargedAmount   = Math.Round(fullAmount * charged / daysInMonth, 2);
                    isProrated      = true;
                    proratedStart   = new DateTime(year, month, startDay, 0, 0, 0, DateTimeKind.Utc);
                    totalSessions   = daysInMonth;
                    sessionsCharged = charged;
                }
            }

            decimal discountAmount = 0m;
            if (student.IsGuest)
            {
                discountAmount = chargedAmount;
                chargedAmount  = 0m;
            }
            else if (student.IsScholarship)
            {
                discountAmount = chargedAmount;
                chargedAmount  = 0m;
            }
            else if (student.ScholarshipPercentage.HasValue && student.ScholarshipPercentage.Value > 0)
            {
                discountAmount = Math.Round(chargedAmount * student.ScholarshipPercentage.Value / 100, 2);
                chargedAmount -= discountAmount;
            }

            _context.PaymentRecords.Add(new PaymentRecord
            {
                AcademyId           = academyId,
                StudentId           = student.Id,
                Description         = description,
                Amount              = chargedAmount,
                AmountPaid          = 0m,
                DiscountAmount      = discountAmount,
                DueDate             = dueDate,
                IsPaid              = chargedAmount == 0m,
                PaidDate            = chargedAmount == 0m ? DateTime.UtcNow : null,
                Type                = PaymentType.MonthlyFee,
                IsProrated          = isProrated,
                ProratedStartDate   = proratedStart,
                ProratedTotalDays   = totalSessions,
                ProratedDaysCharged = sessionsCharged
            });
            generatedCount++;
        }

        if (generatedCount > 0 || replacedCount > 0 || cleanedCount > 0)
            await _context.SaveChangesAsync();

        return (generatedCount, replacedCount, cleanedCount);
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

        decimal discountAmount = 0m;
        if (student.IsGuest)
        {
            discountAmount = fullAmount;
            fullAmount = 0m;
        }
        else if (student.IsScholarship)
        {
            discountAmount = fullAmount;
            fullAmount = 0m;
        }
        else if (student.ScholarshipPercentage.HasValue && student.ScholarshipPercentage.Value > 0)
        {
            discountAmount = Math.Round(fullAmount * student.ScholarshipPercentage.Value / 100, 2);
            fullAmount -= discountAmount;
        }

        // Next month is always full month (no proration needed)
        var record = new PaymentRecord
        {
            AcademyId           = academyId,
            StudentId           = studentId,
            Description         = description,
            Amount              = fullAmount,
            AmountPaid          = 0m,
            DiscountAmount      = discountAmount,
            DueDate             = dueDate,
            IsPaid              = fullAmount == 0m,
            PaidDate            = fullAmount == 0m ? DateTime.UtcNow : null,
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
    public async Task<List<PaymentRecordDto>> GetPendingDebtsAsync(Guid academyId, int? year = null, int? month = null)
    {
        var today = DateTime.UtcNow;
        var query = _context.PaymentRecords
            .Include(p => p.Student).ThenInclude(s => s.Category)
            .Include(p => p.Installments)
            .Where(p => p.AcademyId == academyId && !p.IsPaid && !p.IsDeleted);

        if (year.HasValue && month.HasValue)
            query = query.Where(p => p.DueDate.Year == year.Value && p.DueDate.Month == month.Value);

        var debts = await query.OrderBy(p => p.DueDate).ToListAsync();
        return debts.Select(p => MapToDto(p, today)).ToList();
    }

    public async Task<List<PaymentRecordDto>> GetAllDebtsAsync(Guid academyId, int? year = null, int? month = null)
    {
        var today = DateTime.UtcNow;
        var query = _context.PaymentRecords
            .Include(p => p.Student).ThenInclude(s => s.Category)
            .Include(p => p.Installments)
            .Where(p => p.AcademyId == academyId && !p.IsDeleted);

        if (year.HasValue && month.HasValue)
            query = query.Where(p => p.DueDate.Year == year.Value && p.DueDate.Month == month.Value);

        var debts = await query.OrderByDescending(p => p.DueDate).ToListAsync();
        return debts.Select(p => MapToDto(p, today)).ToList();
    }

    public async Task<List<PaymentRecordDto>> GetMyDebtsAsync(Guid academyId, Guid guardianUserId)
    {
        var today = DateTime.UtcNow;
        var debts = await _context.PaymentRecords
            .Include(p => p.Student).ThenInclude(s => s.Category)
            .Include(p => p.Installments)
            .Where(p => p.AcademyId == academyId && 
                       (p.Student.GuardianId == guardianUserId || p.Student.UserId == guardianUserId) && 
                       !p.IsDeleted)
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

        // Generate a sequential receipt number stored in the DB
        var receiptNum = await GenerateNextReceiptNumberAsync(academyId);

        _context.PaymentInstallments.Add(new PaymentInstallment
        {
            PaymentRecordId = record.Id,
            AmountPaid      = dto.AmountPaid,
            PaidAt          = DateTime.UtcNow,
            Method          = method,
            OperationNumber = dto.OperationNumber,
            VoucherUrl      = dto.VoucherUrl,
            Notes           = dto.Notes,
            ReceiptNumber   = receiptNum
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
            
            // Aplicar descuento de beca/invitado al monto recalculado
            decimal discountAmount = 0m;
            if (student.IsGuest)
            {
                discountAmount = record.Amount;
                record.Amount = 0m;
            }
            else if (student.IsScholarship)
            {
                discountAmount = record.Amount;
                record.Amount = 0m;
            }
            else if (student.ScholarshipPercentage.HasValue && student.ScholarshipPercentage.Value > 0)
            {
                discountAmount = Math.Round(record.Amount * student.ScholarshipPercentage.Value / 100, 2);
                record.Amount -= discountAmount;
            }
            record.DiscountAmount = discountAmount;
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
        
        // El monto que iba a ser cobrado se registra como pérdida/descuento
        record.DiscountAmount = record.Amount;
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
            DiscountAmount = p.DiscountAmount,
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
                Notes           = i.Notes,
                ReceiptNumber   = i.ReceiptNumber
            }).ToList()
        };
    }
}
