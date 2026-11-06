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
    private readonly FinancialPeriodGuard _periods;
    private readonly AuditService _audit;

    public FinancesService(AppDbContext context, FinancialPeriodGuard periods, AuditService audit)
    {
        _context = context;
        _periods = periods;
        _audit = audit;
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers de periodo
    // ─────────────────────────────────────────────────────────────
    private static (int Year, int Month) PeriodOf(PaymentRecord p) =>
        (p.PeriodYear ?? p.DueDate.Year, p.PeriodMonth ?? p.DueDate.Month);

    /// <summary>Filtra por mes: las mensualidades usan su periodo; el resto, la fecha de vencimiento.</summary>
    private static IQueryable<PaymentRecord> WhereInMonth(IQueryable<PaymentRecord> q, int year, int month) =>
        q.Where(p =>
            (p.PeriodYear != null && p.PeriodYear == year && p.PeriodMonth == month) ||
            (p.PeriodYear == null && p.DueDate.Year == year && p.DueDate.Month == month));

    private static DateTime DueDateFor(int year, int month, int paymentDay)
    {
        var day = Math.Min(paymentDay, DateTime.DaysInMonth(year, month));
        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
    }

    // ─────────────────────────────────────────────────────────────
    // Configuration
    // ─────────────────────────────────────────────────────────────
    private async Task<AcademyFinancialConfig> GetOrCreateConfigEntityAsync(Guid academyId)
    {
        var config = await _context.AcademyFinancialConfigs.FirstOrDefaultAsync(c => c.AcademyId == academyId);
        if (config == null)
        {
            config = new AcademyFinancialConfig { AcademyId = academyId, DefaultPaymentDay = 5, CurrentReceiptNumber = 0 };
            _context.AcademyFinancialConfigs.Add(config);
            await _context.SaveChangesAsync();
        }
        return config;
    }

    public async Task<FinancialConfigDto> GetConfigAsync(Guid academyId)
    {
        var config = await GetOrCreateConfigEntityAsync(academyId);
        return new FinancialConfigDto { DefaultPaymentDay = config.DefaultPaymentDay };
    }

    /// <summary>
    /// Reserva el siguiente número de recibo de forma segura ante cobros simultáneos
    /// (token de concurrencia + reintento). Debe llamarse dentro de una transacción
    /// (ver <see cref="DbContextExtensions.InTransactionAsync"/>) y ANTES de agregar otros cambios,
    /// para que si la operación falla el número se revierta y no queden saltos.
    /// </summary>
    public async Task<string> ReserveReceiptNumberAsync(Guid academyId)
    {
        var config = await GetOrCreateConfigEntityAsync(academyId);
        for (var attempt = 1; ; attempt++)
        {
            config.CurrentReceiptNumber++;
            try
            {
                await _context.SaveChangesAsync();
                return config.CurrentReceiptNumber.ToString().PadLeft(6, '0');
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < 5)
            {
                foreach (var entry in ex.Entries) await entry.ReloadAsync();
            }
        }
    }

    public async Task<FinancialConfigDto> UpdateConfigAsync(Guid academyId, UpdateFinancialConfigDto dto)
    {
        if (dto.DefaultPaymentDay < 1 || dto.DefaultPaymentDay > 31)
            throw new Exception("El día de cobro debe estar entre 1 y 31.");

        var config = await GetOrCreateConfigEntityAsync(academyId);
        config.DefaultPaymentDay = dto.DefaultPaymentDay;

        // Ajustar el vencimiento de las mensualidades pendientes del mes actual
        var now = AcademyClock.Now;
        var pendingDebts = await WhereInMonth(_context.PaymentRecords
                .Where(p => p.AcademyId == academyId && !p.IsPaid && p.Type == PaymentType.MonthlyFee),
                now.Year, now.Month)
            .ToListAsync();

        foreach (var debt in pendingDebts)
        {
            var (y, m) = PeriodOf(debt);
            debt.DueDate = DueDateFor(y, m, dto.DefaultPaymentDay);
        }

        _audit.Add(academyId, "finances.config.update", nameof(AcademyFinancialConfig), config.Id,
            new { dto.DefaultPaymentDay, adjustedDebts = pendingDebts.Count });
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

    /// <summary>Aplica beca/invitado al monto. Devuelve (montoACobrar, descuento).</summary>
    private static (decimal Charged, decimal Discount) ApplyBenefits(Student student, decimal amount)
    {
        if (student.IsGuest || student.IsScholarship)
            return (0m, amount);

        if (student.ScholarshipPercentage.HasValue && student.ScholarshipPercentage.Value > 0)
        {
            var pct = Math.Min(student.ScholarshipPercentage.Value, 100m);
            var discount = Math.Round(amount * pct / 100, 2);
            return (amount - discount, discount);
        }
        return (amount, 0m);
    }

    // ─────────────────────────────────────────────────────────────
    // Motor de Generación de Deudas (prorrateo por sesiones)
    // ─────────────────────────────────────────────────────────────
    public async Task<(int Generated, int Replaced, int Cleaned)> GenerateMonthlyDebtsAsync(
        Guid academyId, int? targetYear = null, int? targetMonth = null)
    {
        var now   = AcademyClock.Now;
        int year  = targetYear  ?? now.Year;
        int month = targetMonth ?? now.Month;
        if (month < 1 || month > 12 || year < 2000 || year > 2100)
            throw new Exception("Periodo inválido.");

        await _periods.EnsureOpenAsync(academyId, year, month, "generar cobros");

        var config      = await GetConfigAsync(academyId);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var dueDate     = DueDateFor(year, month, config.DefaultPaymentDay);
        var targetRef   = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var description = Culture.MonthlyFeeDescription(year, month);

        var students = await _context.Students
            .Include(s => s.Category)
            .Where(s => s.AcademyId == academyId &&
                       (s.IsActive ||
                       (s.WithdrawalDate.HasValue && s.WithdrawalDate.Value >= targetRef)))
            .ToListAsync();

        int generatedCount = 0, replacedCount = 0, cleanedCount = 0;

        // ── STEP 1: Cleanup — soft-delete unpaid records that fall BEFORE ──
        //   each student's effective start month (PaymentStartDate ?? EnrollmentDate)
        //   EXCEPCIÓN: nunca eliminar registros con pagos parciales registrados.
        var studentIds = students.Select(s => s.Id).ToList();
        var studentsById = students.ToDictionary(s => s.Id);
        var allUnpaidMonthly = await _context.PaymentRecords
            .Include(p => p.Installments)
            .Where(p => p.AcademyId == academyId
                     && !p.IsPaid
                     && p.Type == PaymentType.MonthlyFee
                     && studentIds.Contains(p.StudentId))
            .ToListAsync();

        foreach (var record in allUnpaidMonthly)
        {
            if (!studentsById.TryGetValue(record.StudentId, out var student)) continue;
            if (record.Installments.Any()) continue;

            var effStart = (student.PaymentStartDate ?? student.EnrollmentDate).ToUniversalTime();
            var effStartMonth = new DateTime(effStart.Year, effStart.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var (ry, rm) = PeriodOf(record);
            var recordMonth = new DateTime(ry, rm, 1, 0, 0, 0, DateTimeKind.Utc);

            if (recordMonth < effStartMonth)
            {
                record.IsDeleted  = true;
                record.DeletedAt  = DateTime.UtcNow;
                cleanedCount++;
            }
        }

        // ── STEP 1.5: soft-delete unpaid records of the TARGET MONTH for students no longer eligible
        var targetMonthRecords = await WhereInMonth(_context.PaymentRecords
                .Include(p => p.Installments)
                .Where(p => p.AcademyId == academyId && p.Type == PaymentType.MonthlyFee),
                year, month)
            .ToListAsync();

        foreach (var record in targetMonthRecords.Where(r => !r.IsPaid && !r.IsDeleted))
        {
            if (record.Installments.Any()) continue;
            if (!studentsById.ContainsKey(record.StudentId))
            {
                record.IsDeleted = true;
                record.DeletedAt = DateTime.UtcNow;
                cleanedCount++;
            }
        }

        // Una consulta para todo el mes (antes era una consulta por alumno)
        var existingByStudent = targetMonthRecords
            .Where(r => !r.IsDeleted)
            .GroupBy(r => r.StudentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.IsPaid).ThenByDescending(r => r.AmountPaid).First());

        // ── STEP 2: Generate / replace for the target month ───────────────
        foreach (var student in students)
        {
            var effStart = (student.PaymentStartDate ?? student.EnrollmentDate).ToUniversalTime();
            var effStartMonth = new DateTime(effStart.Year, effStart.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            if (targetRef < effStartMonth) continue;

            existingByStudent.TryGetValue(student.Id, out var existingDebt);

            // Pagado completamente → nunca sobrescribir
            if (existingDebt != null && existingDebt.IsPaid) continue;

            var fullAmount = student.PreferentialFee ?? student.Category?.MonthlyFee ?? 0m;
            if (fullAmount <= 0) continue;

            // ── Calcular monto nuevo (con prorrateo si aplica) ────────
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

            var (amountToCharge, discountAmount) = ApplyBenefits(student, chargedAmount);
            chargedAmount = amountToCharge;

            bool hasPartialPayments = existingDebt != null && existingDebt.Installments.Any();

            if (hasPartialPayments)
            {
                // Solo se actualiza el total; lo ya abonado se conserva.
                existingDebt!.Amount              = chargedAmount;
                existingDebt.DiscountAmount       = discountAmount;
                existingDebt.DueDate              = dueDate;
                existingDebt.PeriodYear           = year;
                existingDebt.PeriodMonth          = month;
                existingDebt.IsProrated           = isProrated;
                existingDebt.ProratedStartDate    = proratedStart;
                existingDebt.ProratedTotalDays    = totalSessions;
                existingDebt.ProratedDaysCharged  = sessionsCharged;

                if (existingDebt.AmountPaid >= chargedAmount - 0.01m && chargedAmount > 0)
                {
                    existingDebt.AmountPaid = chargedAmount;
                    existingDebt.IsPaid     = true;
                    existingDebt.PaidDate   = existingDebt.PaidDate ?? DateTime.UtcNow;
                }
                else
                {
                    existingDebt.IsPaid   = chargedAmount == 0m;
                    existingDebt.PaidDate = chargedAmount == 0m ? DateTime.UtcNow : null;
                }

                replacedCount++;
            }
            else
            {
                if (existingDebt != null)
                {
                    existingDebt.IsDeleted = true;
                    existingDebt.DeletedAt = DateTime.UtcNow;
                    replacedCount++;
                }
                else
                {
                    generatedCount++;
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
                    PeriodYear          = year,
                    PeriodMonth         = month,
                    IsPaid              = chargedAmount == 0m,
                    PaidDate            = chargedAmount == 0m ? DateTime.UtcNow : null,
                    Type                = PaymentType.MonthlyFee,
                    IsProrated          = isProrated,
                    ProratedStartDate   = proratedStart,
                    ProratedTotalDays   = totalSessions,
                    ProratedDaysCharged = sessionsCharged
                });
            }
        }

        if (generatedCount > 0 || replacedCount > 0 || cleanedCount > 0)
        {
            _audit.Add(academyId, "finances.debts.generate", nameof(PaymentRecord), null,
                new { year, month, generatedCount, replacedCount, cleanedCount });
            await _context.SaveChangesAsync();
        }

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

        var nextMonth = AcademyClock.Now.AddMonths(1);
        int year      = nextMonth.Year;
        int month     = nextMonth.Month;
        var description = Culture.MonthlyFeeDescription(year, month);

        await _periods.EnsureOpenAsync(academyId, year, month, "generar el cobro");

        var exists = await _context.PaymentRecords.AnyAsync(p =>
            p.AcademyId == academyId &&
            p.StudentId == studentId &&
            p.Type == PaymentType.MonthlyFee &&
            p.PeriodYear == year && p.PeriodMonth == month);

        if (exists)
            throw new Exception($"Ya existe un cobro para {description}.");

        var fullAmount = student.PreferentialFee ?? student.Category?.MonthlyFee ?? 0m;
        if (fullAmount <= 0) throw new Exception("El alumno no tiene tarifa configurada.");

        var (amount, discountAmount) = ApplyBenefits(student, fullAmount);

        var record = new PaymentRecord
        {
            AcademyId           = academyId,
            StudentId           = studentId,
            Description         = description,
            Amount              = amount,
            AmountPaid          = 0m,
            DiscountAmount      = discountAmount,
            DueDate             = DueDateFor(year, month, config.DefaultPaymentDay),
            PeriodYear          = year,
            PeriodMonth         = month,
            IsPaid              = amount == 0m,
            PaidDate            = amount == 0m ? DateTime.UtcNow : null,
            Type                = PaymentType.MonthlyFee,
            IsProrated          = false
        };

        _context.PaymentRecords.Add(record);
        await _context.SaveChangesAsync();

        var loaded = await _context.PaymentRecords
            .Include(p => p.Student).ThenInclude(s => s.Category)
            .Include(p => p.Installments)
            .FirstAsync(p => p.Id == record.Id);

        return MapToDto(loaded);
    }

    // ─────────────────────────────────────────────────────────────
    // Query debts
    // ─────────────────────────────────────────────────────────────
    public async Task<List<PaymentRecordDto>> GetPendingDebtsAsync(Guid academyId, int? year = null, int? month = null)
    {
        var query = _context.PaymentRecords
            .Include(p => p.Student).ThenInclude(s => s.Category)
            .Include(p => p.Installments)
            .Where(p => p.AcademyId == academyId && !p.IsPaid);

        if (year.HasValue && month.HasValue)
            query = WhereInMonth(query, year.Value, month.Value);

        var debts = await query.OrderBy(p => p.DueDate).ToListAsync();
        return debts.Select(MapToDto).ToList();
    }

    public async Task<List<PaymentRecordDto>> GetAllDebtsAsync(Guid academyId, int? year = null, int? month = null)
    {
        var query = _context.PaymentRecords
            .Include(p => p.Student).ThenInclude(s => s.Category)
            .Include(p => p.Installments)
            .Where(p => p.AcademyId == academyId);

        if (year.HasValue && month.HasValue)
            query = WhereInMonth(query, year.Value, month.Value);

        var debts = await query.OrderByDescending(p => p.DueDate).ToListAsync();
        return debts.Select(MapToDto).ToList();
    }

    public async Task<List<PaymentRecordDto>> GetMyDebtsAsync(Guid academyId, Guid guardianUserId)
    {
        var debts = await _context.PaymentRecords
            .Include(p => p.Student).ThenInclude(s => s.Category)
            .Include(p => p.Installments)
            .Where(p => p.AcademyId == academyId &&
                       (p.Student.GuardianId == guardianUserId || p.Student.UserId == guardianUserId))
            .OrderBy(p => p.DueDate)
            .ToListAsync();
        return debts.Select(MapToDto).ToList();
    }

    // ─────────────────────────────────────────────────────────────
    // Registrar pago (total o parcial) con medio + voucher
    // ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Registra un abono. Si otro usuario cobra la MISMA deuda al mismo tiempo, el token de concurrencia
    /// de AmountPaid detecta el conflicto: la transacción se revierte (incluido el número de recibo)
    /// y la operación se reintenta con el saldo actualizado.
    /// </summary>
    public async Task<PaymentRecordDto> RegisterPaymentAsync(Guid academyId, RegisterPaymentDto dto)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await RegisterPaymentOnceAsync(academyId, dto);
            }
            catch (DbUpdateConcurrencyException) when (attempt < 4)
            {
                _context.ChangeTracker.Clear();
            }
            catch (DbUpdateConcurrencyException)
            {
                _context.ChangeTracker.Clear();
                throw new Exception("Otro usuario está registrando un pago sobre esta deuda. Actualiza la pantalla e inténtalo nuevamente.");
            }
        }
    }

    private Task<PaymentRecordDto> RegisterPaymentOnceAsync(Guid academyId, RegisterPaymentDto dto) =>
        _context.InTransactionAsync(async () =>
        {
            var record = await _context.PaymentRecords
                .Include(p => p.Student).ThenInclude(s => s.Category)
                .Include(p => p.Installments)
                .FirstOrDefaultAsync(p => p.Id == dto.PaymentRecordId && p.AcademyId == academyId)
                ?? throw new Exception("Registro de pago no encontrado.");

            // El dinero entra HOY: lo que debe estar abierto es el periodo del día de pago
            // (se puede cobrar una deuda antigua aunque su mes ya esté cerrado).
            await _periods.EnsureOpenAsync(academyId, AcademyClock.Today, "registrar el cobro");

            if (record.IsPaid)
                throw new Exception("Este cobro ya está completamente pagado.");
            if (dto.AmountPaid <= 0)
                throw new Exception("El monto a pagar debe ser mayor a cero.");

            var pending = record.Amount - record.AmountPaid;
            if (dto.AmountPaid > pending + 0.01m)
                throw new Exception($"El monto ingresado ({dto.AmountPaid:N2}) supera el saldo pendiente ({pending:N2}).");

            if (!Enum.TryParse<PaymentMethod>(dto.Method, true, out var method))
                method = PaymentMethod.Cash;

            var receiptNum = await ReserveReceiptNumberAsync(academyId);

            var installment = new PaymentInstallment
            {
                PaymentRecordId = record.Id,
                AmountPaid      = dto.AmountPaid,
                PaidAt          = DateTime.UtcNow,
                Method          = method,
                OperationNumber = dto.OperationNumber,
                VoucherUrl      = dto.VoucherUrl,
                Notes           = dto.Notes,
                ReceiptNumber   = receiptNum
            };
            _context.PaymentInstallments.Add(installment);

            record.AmountPaid += dto.AmountPaid;
            if (record.AmountPaid >= record.Amount - 0.01m)
            {
                record.AmountPaid = record.Amount;
                record.IsPaid     = true;
                record.PaidDate   = DateTime.UtcNow;
            }

            _audit.Add(academyId, "finances.payment.register", nameof(PaymentRecord), record.Id,
                new { dto.AmountPaid, method = method.ToString(), dto.OperationNumber, receipt = receiptNum, record.StudentId });

            await _context.SaveChangesAsync();
            return MapToDto(record);
        });

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

        var (periodYear, periodMonth) = PeriodOf(record);
        await _periods.EnsureOpenAsync(academyId, periodYear, periodMonth, "recalcular el cobro");

        if (record.IsPaid)
            throw new Exception("No se puede recalcular un pago ya completado.");
        if (dto.NewAmount.HasValue && dto.NewAmount.Value < 0)
            throw new Exception("El monto no puede ser negativo.");

        var before = new { record.Amount, record.DiscountAmount };
        var config = await GetConfigAsync(academyId);

        var daysInMonth  = DateTime.DaysInMonth(periodYear, periodMonth);
        record.DueDate   = DueDateFor(periodYear, periodMonth, config.DefaultPaymentDay);

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
            // Recalcular desde categoría + prorrateo automático por inicio en el mes
            var student = record.Student;
            DateTime? startInMonth = null;

            var enrollUtc = student.EnrollmentDate.Kind == DateTimeKind.Utc
                ? student.EnrollmentDate
                : student.EnrollmentDate.ToUniversalTime();
            if (enrollUtc.Year == periodYear && enrollUtc.Month == periodMonth && enrollUtc.Day > 1)
                startInMonth = enrollUtc;
            else if (student.PaymentStartDate.HasValue)
            {
                var psUtc = student.PaymentStartDate.Value.ToUniversalTime();
                if (psUtc.Year == periodYear && psUtc.Month == periodMonth && psUtc.Day > 1)
                    startInMonth = psUtc;
            }

            if (startInMonth.HasValue)
            {
                var charged = daysInMonth - startInMonth.Value.Day + 1;
                record.Amount              = Math.Round(fullAmount * charged / daysInMonth, 2);
                record.IsProrated          = true;
                record.ProratedStartDate   = new DateTime(periodYear, periodMonth, startInMonth.Value.Day, 0, 0, 0, DateTimeKind.Utc);
                record.ProratedTotalDays   = daysInMonth;
                record.ProratedDaysCharged = charged;
            }
            else
            {
                record.Amount              = fullAmount;
                record.IsProrated          = false;
                record.ProratedStartDate   = null;
                record.ProratedTotalDays   = null;
                record.ProratedDaysCharged = null;
            }

            var (charge, discount) = ApplyBenefits(student, record.Amount);
            record.Amount = charge;
            record.DiscountAmount = discount;
        }

        if (record.AmountPaid > record.Amount)
            record.AmountPaid = record.Amount;

        if (record.AmountPaid >= record.Amount - 0.01m && record.Amount > 0)
        {
            record.IsPaid   = true;
            record.PaidDate = DateTime.UtcNow;
        }

        _audit.Add(academyId, "finances.payment.recalculate", nameof(PaymentRecord), record.Id,
            new { before, after = new { record.Amount, record.DiscountAmount }, dto.NewAmount, dto.ProratedStartDate });

        await _context.SaveChangesAsync();
        return MapToDto(record);
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

        var (periodYear, periodMonth) = PeriodOf(record);
        await _periods.EnsureOpenAsync(academyId, periodYear, periodMonth, "exonerar el cobro");

        if (record.IsPaid)
            throw new Exception("Este cobro ya está pagado o exonerado.");

        if (!Enum.TryParse<ExclusionType>(dto.ExclusionType, true, out var excType) || excType == ExclusionType.None)
            excType = ExclusionType.FreeClass;

        // Solo se exonera el SALDO pendiente: lo que el alumno ya pagó se conserva como ingreso.
        var pendingBalance = record.Amount - record.AmountPaid;

        record.ExclusionType   = excType;
        record.ExclusionNote   = dto.ExclusionNote;
        record.DiscountAmount += pendingBalance;
        record.Amount          = record.AmountPaid;
        record.IsPaid          = true;
        record.PaidDate        = DateTime.UtcNow;

        _audit.Add(academyId, "finances.payment.exclude", nameof(PaymentRecord), record.Id,
            new { exclusion = excType.ToString(), dto.ExclusionNote, exoneratedAmount = pendingBalance, record.StudentId });

        await _context.SaveChangesAsync();
        return MapToDto(record);
    }

    // ─────────────────────────────────────────────────────────────
    // Legacy quick-pay (full amount, cash)
    // ─────────────────────────────────────────────────────────────
    public async Task<PaymentRecordDto> MarkAsPaidAsync(Guid academyId, Guid paymentId)
    {
        var r = await _context.PaymentRecords.AsNoTracking()
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
    // Mapper
    // ─────────────────────────────────────────────────────────────
    private static PaymentRecordDto MapToDto(PaymentRecord p)
    {
        var today = AcademyClock.Today;
        string status;
        int daysOverdue = 0;

        if (p.IsPaid)
            status = "Pagado";
        else if (today > p.DueDate.Date)
        {
            daysOverdue = (today - p.DueDate.Date).Days;
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
