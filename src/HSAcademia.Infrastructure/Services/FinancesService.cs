using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Finances;
using HSAcademia.Domain.Entities;
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
        await _context.SaveChangesAsync();
        return new FinancialConfigDto { DefaultPaymentDay = config.DefaultPaymentDay };
    }

    // ─────────────────────────────────────────────────────────────
    // Motor de Generación de Deudas (con prorrateo automático)
    // ─────────────────────────────────────────────────────────────
    public async Task<int> GenerateMonthlyDebtsAsync(Guid academyId)
    {
        var config = await GetConfigAsync(academyId);
        var today  = DateTime.UtcNow;

        var periodStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
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

            // ── Proration logic ──────────────────────────────────
            bool isProrated      = false;
            decimal chargedAmount = fullAmount;
            DateTime? proratedStart = null;
            int? totalDays = null, daysCharged = null;

            // Check enrollment date proration (started mid-month)
            var enrollUtc = student.EnrollmentDate.Kind == DateTimeKind.Utc
                ? student.EnrollmentDate
                : student.EnrollmentDate.ToUniversalTime();

            if (enrollUtc.Year == today.Year && enrollUtc.Month == today.Month && enrollUtc.Day > 1)
            {
                daysCharged   = daysInMonth - enrollUtc.Day + 1;
                totalDays     = daysInMonth;
                chargedAmount = Math.Round(fullAmount * daysCharged.Value / totalDays.Value, 2);
                isProrated    = true;
                proratedStart = new DateTime(today.Year, today.Month, enrollUtc.Day, 0, 0, 0, DateTimeKind.Utc);
            }
            else if (student.PaymentStartDate.HasValue)
            {
                var ps = student.PaymentStartDate.Value.ToUniversalTime();
                if (ps.Year == today.Year && ps.Month == today.Month && ps.Day > 1)
                {
                    daysCharged   = daysInMonth - ps.Day + 1;
                    totalDays     = daysInMonth;
                    chargedAmount = Math.Round(fullAmount * daysCharged.Value / totalDays.Value, 2);
                    isProrated    = true;
                    proratedStart = new DateTime(today.Year, today.Month, ps.Day, 0, 0, 0, DateTimeKind.Utc);
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
                ProratedTotalDays   = totalDays,
                ProratedDaysCharged = daysCharged
            });
            generatedCount++;
        }

        if (generatedCount > 0)
            await _context.SaveChangesAsync();

        return generatedCount;
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

        var fullAmount = record.Student.PreferentialFee ?? record.Student.Category?.MonthlyFee ?? 0m;

        if (dto.NewAmount.HasValue)
        {
            record.Amount     = dto.NewAmount.Value;
            record.IsProrated = false;
            record.ProratedStartDate    = null;
            record.ProratedTotalDays    = null;
            record.ProratedDaysCharged  = null;
        }
        else if (dto.ProratedStartDate.HasValue)
        {
            var due         = record.DueDate;
            var dim         = DateTime.DaysInMonth(due.Year, due.Month);
            var startDay    = dto.ProratedStartDate.Value.Day;
            var charged     = dim - startDay + 1;
            record.Amount               = Math.Round(fullAmount * charged / dim, 2);
            record.IsProrated           = true;
            record.ProratedStartDate    = dto.ProratedStartDate.Value.ToUniversalTime();
            record.ProratedTotalDays    = dim;
            record.ProratedDaysCharged  = charged;
        }
        else
        {
            record.Amount     = fullAmount;
            record.IsProrated = false;
            record.ProratedStartDate   = null;
            record.ProratedTotalDays   = null;
            record.ProratedDaysCharged = null;
        }

        if (record.AmountPaid > record.Amount)
            record.AmountPaid = record.Amount;

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
