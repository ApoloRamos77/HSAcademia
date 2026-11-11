using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Cobro de alumnos que pagan por sesión o por paquete.
/// - Por sesión: se cobran las asistencias Presente/Tarde del mes × precio por sesión, con tope en la mensualidad.
///   Las sesiones cobradas en campo se descuentan de la liquidación.
/// - Paquete: el paquete se cobra al inicio del mes (motor de mensualidades); las sesiones que superan el paquete
///   se cobran aquí a precio por sesión, sin pasar en total de la mensualidad.
/// La liquidación es repetible: recalcula lo no pagado y nunca toca lo ya pagado.
/// </summary>
public class SessionBillingService
{
    private static readonly string[] Months =
        { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };
    public const string AutoLiquidationAction = "finances.sessions.liquidate.auto";

    private readonly AppDbContext _db;
    private readonly FinancialPeriodGuard _periods;
    private readonly AuditService _audit;

    public SessionBillingService(AppDbContext db, FinancialPeriodGuard periods, AuditService audit)
    {
        _db = db;
        _periods = periods;
        _audit = audit;
    }

    private static string Cap(string m) => char.ToUpperInvariant(m[0]) + m[1..];

    public static decimal SessionFeeOf(Student s) => s.PreferentialSessionFee ?? s.Category?.SessionFee ?? 0m;
    /// <summary>Tope del mes: lo que pagaría con mensualidad (0 = sin tope).</summary>
    public static decimal CapOf(Student s) => s.PreferentialFee ?? s.Category?.MonthlyFee ?? 0m;
    public static bool HasPackage(Category? c) => c?.PackageSessions is > 0 && c.PackagePrice is > 0;

    public sealed record Computation(int Attended, int Billable, decimal Gross, decimal Capped, decimal Charged, decimal Discount, string Detail, bool CapApplied);

    /// <summary>Cuánto corresponde cobrar por las sesiones del mes (antes de descontar lo cobrado en campo).</summary>
    public static Computation Compute(Student s, int attended)
    {
        var fee = SessionFeeOf(s);
        var cap = CapOf(s);
        var money = (decimal v) => $"S/ {v.ToString("0.00", CultureInfo.InvariantCulture)}";
        int billable;
        decimal room;
        string what;
        if (s.BillingPlan == StudentBillingPlan.Package && HasPackage(s.Category))
        {
            var included = s.Category!.PackageSessions!.Value;
            billable = Math.Max(0, attended - included);
            room = cap > 0 ? Math.Max(0, cap - s.Category.PackagePrice!.Value) : decimal.MaxValue;
            what = $"{billable} sesión(es) extra del paquete de {included} × {money(fee)}";
        }
        else
        {
            billable = attended;
            room = cap > 0 ? cap : decimal.MaxValue;
            what = $"{attended} sesión(es) × {money(fee)}";
        }
        var gross = billable * fee;
        var capped = Math.Min(gross, room);
        var capApplied = capped < gross;

        decimal charged = capped, discount = 0m;
        var parts = new List<string> { what };
        if (capApplied) parts.Add($"tope {money(cap)}");
        if (s.IsGuest || s.IsScholarship) { discount = charged; charged = 0; parts.Add(s.IsGuest ? "Invitado" : "Beca 100%"); }
        else if (s.ScholarshipPercentage is > 0)
        {
            discount = Math.Round(charged * Math.Min(s.ScholarshipPercentage.Value, 100m) / 100, 2);
            charged -= discount;
            parts.Add($"Beca {s.ScholarshipPercentage:0.##}%");
        }
        return new Computation(attended, billable, gross, capped, charged, discount, string.Join(" · ", parts), capApplied);
    }

    /// <summary>Asistencias que cuentan como sesión (Presente o Tarde), una por sesión o por día sin sesión.</summary>
    public async Task<Dictionary<Guid, int>> AttendedAsync(Guid academyId, IReadOnlyCollection<Guid> studentIds, int year, int month)
    {
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1);
        var rows = await _db.Attendances.AsNoTracking()
            .Where(a => a.AcademyId == academyId && studentIds.Contains(a.StudentId) && a.Date >= from && a.Date < to
                        && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late))
            .Select(a => new { a.StudentId, a.EventId, a.Date })
            .ToListAsync();
        return rows.GroupBy(r => r.StudentId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.EventId.HasValue ? r.EventId.Value.ToString() : r.Date.Date.ToString("yyyy-MM-dd")).Distinct().Count());
    }

    // ─────────────────────────────────────────────────────────────
    // Cobro de una sesión en campo
    // ─────────────────────────────────────────────────────────────
    public sealed record FieldCharge(Guid DebtId, decimal Amount, bool AlreadyCharged, string Description);

    public async Task<FieldCharge> ChargeSessionAsync(Guid academyId, Guid studentId, Guid? eventId, DateTime? date)
    {
        var student = await _db.Students.Include(s => s.Category)
            .FirstOrDefaultAsync(s => s.Id == studentId && s.AcademyId == academyId)
            ?? throw new KeyNotFoundException("Alumno no encontrado.");
        if (!student.IsActive) throw new Exception("El alumno está retirado.");
        if (student.BillingPlan != StudentBillingPlan.PerSession)
            throw new Exception("Solo se cobra por sesión a los alumnos con plan \"Por sesión\".");
        var fee = SessionFeeOf(student);
        if (fee <= 0) throw new Exception("Configura el precio por sesión de la categoría o del alumno.");

        DateTime sessionDate;
        if (eventId.HasValue)
        {
            var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId && e.AcademyId == academyId && !e.IsDeleted)
                ?? throw new KeyNotFoundException("Sesión no encontrada.");
            var cats = new List<Guid>(ev.CategoryIds ?? new List<Guid>());
            if (ev.CategoryId.HasValue) cats.Add(ev.CategoryId.Value);
            if (!cats.Contains(student.CategoryId)) throw new Exception("El alumno no pertenece a esta sesión.");
            sessionDate = ev.StartTime.Date;
        }
        else sessionDate = (date ?? AcademyClock.Today).Date;
        if (sessionDate > AcademyClock.Today) throw new Exception("No se cobra una sesión futura.");
        var sessionUtc = DateTime.SpecifyKind(sessionDate, DateTimeKind.Utc);

        var existing = await _db.PaymentRecords.FirstOrDefaultAsync(p =>
            p.AcademyId == academyId && p.StudentId == studentId && p.Type == PaymentType.Sessions && p.SessionDate != null &&
            (eventId.HasValue ? p.SessionEventId == eventId : p.SessionEventId == null && p.SessionDate == sessionUtc));
        if (existing != null) return new FieldCharge(existing.Id, existing.Amount, true, existing.Description);

        await _periods.EnsureOpenAsync(academyId, sessionDate.Year, sessionDate.Month, "cobrar la sesión");

        if (student.IsGuest || student.IsScholarship) throw new Exception("El alumno no paga sesiones (beca o invitado).");
        var amount = Math.Round(fee - (fee * Math.Min(student.ScholarshipPercentage ?? 0, 100m) / 100), 2);
        var discount = fee - amount;

        // Tope: lo cobrado en el mes no pasa de la mensualidad.
        var cap = CapOf(student);
        if (cap > 0)
        {
            var monthStart = new DateTime(sessionDate.Year, sessionDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var charged = await _db.PaymentRecords
                .Where(p => p.AcademyId == academyId && p.StudentId == studentId && p.Type == PaymentType.Sessions && p.SessionDate != null
                            && p.SessionDate >= monthStart && p.SessionDate < monthStart.AddMonths(1))
                .SumAsync(p => (decimal?)(p.Amount + p.DiscountAmount)) ?? 0m;
            if (charged >= cap - 0.001m)
                throw new Exception($"Ya alcanzó el tope del mes (S/ {cap:0.00}): las demás sesiones no se cobran.");
            if (charged + fee > cap)
            {
                var gross = cap - charged;
                amount = Math.Round(gross - (gross * Math.Min(student.ScholarshipPercentage ?? 0, 100m) / 100), 2);
                discount = gross - amount;
            }
        }

        var record = new PaymentRecord
        {
            AcademyId = academyId,
            StudentId = studentId,
            Description = $"Sesión {sessionDate:dd/MM/yyyy} · {student.Category?.Name}",
            Amount = amount,
            DiscountAmount = discount,
            DiscountDetail = discount > 0 ? $"Beca {student.ScholarshipPercentage:0.##}%" : null,
            DueDate = sessionUtc,
            PeriodYear = sessionDate.Year,
            PeriodMonth = sessionDate.Month,
            Type = PaymentType.Sessions,
            SessionDate = sessionUtc,
            SessionEventId = eventId,
            SessionsCount = 1,
            IsPaid = amount == 0m,
            PaidDate = amount == 0m ? DateTime.UtcNow : null
        };
        _db.PaymentRecords.Add(record);
        _audit.Add(academyId, "finances.sessions.charge", nameof(PaymentRecord), record.Id, new { studentId, eventId, sessionDate, amount });
        await _db.SaveChangesAsync();
        return new FieldCharge(record.Id, record.Amount, false, record.Description);
    }

    // ─────────────────────────────────────────────────────────────
    // Liquidación del mes
    // ─────────────────────────────────────────────────────────────
    public sealed record LiquidationResult(int Year, int Month, int Generated, int Updated, int Removed, List<string> Notes);

    public async Task<LiquidationResult> LiquidateAsync(Guid academyId, int year, int month, bool automatic = false, CancellationToken ct = default)
    {
        if (month < 1 || month > 12 || year < 2000 || year > 2100) throw new Exception("Periodo inválido.");
        var today = AcademyClock.Today;
        if (year > today.Year || (year == today.Year && month >= today.Month))
            throw new Exception("Solo se liquidan meses terminados: las asistencias del mes en curso aún pueden cambiar.");
        await _periods.EnsureOpenAsync(academyId, year, month, "liquidar sesiones");

        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var students = await _db.Students.Include(s => s.Category)
            .Where(s => s.AcademyId == academyId && s.BillingPlan != StudentBillingPlan.Monthly)
            .ToListAsync(ct);
        var ids = students.Select(s => s.Id).ToList();
        var attended = await AttendedAsync(academyId, ids, year, month);

        var periodRecords = await _db.PaymentRecords.Include(p => p.Installments)
            .Where(p => p.AcademyId == academyId && ids.Contains(p.StudentId) && p.PeriodYear == year && p.PeriodMonth == month
                        && (p.Type == PaymentType.Sessions || p.Type == PaymentType.MonthlyFee))
            .ToListAsync(ct);

        int generated = 0, updated = 0, removed = 0;
        var notes = new List<string>();
        var due = DateTime.SpecifyKind(today.AddDays(7), DateTimeKind.Utc);
        var newOnes = new List<(PaymentRecord Record, Student Student)>();

        foreach (var s in students)
        {
            var mine = periodRecords.Where(p => p.StudentId == s.Id).ToList();
            var liquidation = mine.FirstOrDefault(p => p.Type == PaymentType.Sessions && p.SessionDate == null);
            var fieldCharged = mine.Where(p => p.Type == PaymentType.Sessions && p.SessionDate != null).Sum(p => p.Amount);
            var monthly = mine.FirstOrDefault(p => p.Type == PaymentType.MonthlyFee);
            var name = $"{s.FirstName} {s.LastName}";
            attended.TryGetValue(s.Id, out var count);

            if (s.BillingPlan == StudentBillingPlan.PerSession && monthly != null)
            {
                if (monthly.IsPaid || monthly.Installments.Any())
                {
                    notes.Add($"{name}: ya tiene la mensualidad de este mes (pagada o con abonos); no se liquidan sesiones.");
                    continue;
                }
                // Pasó a pagar por sesión: la mensualidad sin pagos de ese mes deja de corresponder.
                monthly.IsDeleted = true;
                monthly.DeletedAt = DateTime.UtcNow;
                removed++;
            }
            if (s.BillingPlan == StudentBillingPlan.Package && !HasPackage(s.Category))
            {
                if (count > 0) notes.Add($"{name}: su categoría no tiene paquete configurado.");
                continue;
            }
            if (SessionFeeOf(s) <= 0)
            {
                if (count > 0) notes.Add($"{name}: falta el precio por sesión.");
                continue;
            }

            var c = Compute(s, count);
            var remaining = Math.Max(0, c.Charged - fieldCharged);
            if (liquidation != null && (liquidation.IsPaid || liquidation.ExclusionType != ExclusionType.None)) continue;

            var description = s.BillingPlan == StudentBillingPlan.Package
                ? $"Sesiones extra {Cap(Months[month - 1])} {year}"
                : $"Sesiones {Cap(Months[month - 1])} {year}";
            var detail = fieldCharged > 0 ? $"{c.Detail} · cobrado en campo S/ {fieldCharged:0.00}" : c.Detail;
            if (detail.Length > 200) detail = detail[..200];

            if (remaining <= 0)
            {
                if (liquidation != null && !liquidation.Installments.Any())
                {
                    liquidation.IsDeleted = true;
                    liquidation.DeletedAt = DateTime.UtcNow;
                    removed++;
                }
                continue;
            }

            if (liquidation == null)
            {
                var record = new PaymentRecord
                {
                    AcademyId = academyId, StudentId = s.Id, Description = description,
                    Amount = remaining, DiscountAmount = c.Discount, DiscountDetail = detail,
                    DueDate = due, PeriodYear = year, PeriodMonth = month,
                    Type = PaymentType.Sessions, SessionsCount = count
                };
                _db.PaymentRecords.Add(record);
                newOnes.Add((record, s));
                generated++;
            }
            else if (liquidation.Amount != remaining || liquidation.SessionsCount != count || liquidation.DiscountDetail != detail)
            {
                liquidation.Amount = remaining;
                liquidation.DiscountAmount = c.Discount;
                liquidation.DiscountDetail = detail;
                liquidation.SessionsCount = count;
                liquidation.Description = description;
                if (liquidation.AmountPaid >= remaining - 0.01m)
                {
                    liquidation.AmountPaid = remaining;
                    liquidation.IsPaid = true;
                    liquidation.PaidDate ??= DateTime.UtcNow;
                }
                updated++;
            }
        }

        // Aviso a la familia solo con la primera liquidación de cada alumno.
        foreach (var (record, s) in newOnes.Where(n => n.Student.GuardianId != null || n.Student.UserId != null))
        {
            _db.FamilyNotices.Add(new FamilyNotice
            {
                AcademyId = academyId,
                StudentId = s.Id,
                PaymentRecordId = record.Id,
                DedupeKey = $"sess:{record.Id}",
                Type = FamilyNoticeType.PaymentReminder,
                Title = $"Sesiones de {Months[month - 1]}",
                Body = $"{s.FirstName}: {record.SessionsCount} sesión(es) en {Months[month - 1]} · S/ {record.Amount.ToString("0.00", CultureInfo.InvariantCulture)}. Vence el {due.Day} de {Months[due.Month - 1]}."
            });
        }

        _audit.Add(academyId, automatic ? AutoLiquidationAction : "finances.sessions.liquidate", nameof(PaymentRecord), null,
            new { year, month, generated, updated, removed, notes = notes.Count });
        await _db.SaveChangesAsync(ct);
        return new LiquidationResult(year, month, generated, updated, removed, notes);
    }

    /// <summary>
    /// Liquidación automática del mes anterior, una vez por academia, desde el día 3
    /// (las listas del último día se cierran solas el día 2).
    /// </summary>
    public async Task<int> RunAutomaticAsync(DateTime localNow, CancellationToken ct = default)
    {
        if (localNow.Day < 3) return 0;
        var prev = new DateTime(localNow.Year, localNow.Month, 1).AddMonths(-1);
        var since = DateTime.SpecifyKind(new DateTime(localNow.Year, localNow.Month, 1), DateTimeKind.Utc).AddHours(5);
        var academies = await _db.Students.AsNoTracking()
            .Where(s => s.BillingPlan != StudentBillingPlan.Monthly)
            .Select(s => s.AcademyId).Distinct().ToListAsync(ct);
        var done = 0;
        foreach (var academyId in academies)
        {
            var already = await _db.AuditLogs.AsNoTracking()
                .AnyAsync(a => a.AcademyId == academyId && a.Action == AutoLiquidationAction && a.CreatedAt >= since, ct);
            if (already) continue;
            try
            {
                await LiquidateAsync(academyId, prev.Year, prev.Month, automatic: true, ct);
                done++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Mes cerrado u otro impedimento: se deja constancia y no se reintenta; administración liquida a mano.
                _db.ChangeTracker.Clear();
                _audit.Add(academyId, AutoLiquidationAction, nameof(PaymentRecord), null, new { year = prev.Year, month = prev.Month, error = ex.Message });
                await _db.SaveChangesAsync(ct);
            }
        }
        return done;
    }

    // ─────────────────────────────────────────────────────────────
    // Resumen del mes (familia y personal)
    // ─────────────────────────────────────────────────────────────
    public sealed record MonthSummary(Guid StudentId, string StudentName, string Plan, int Year, int Month, int Attended,
        decimal SessionFee, decimal Cap, int? PackageSessions, decimal? PackagePrice, decimal Accrued, decimal PaidInField, bool CapReached);

    public async Task<List<MonthSummary>> SummariesAsync(Guid academyId, IReadOnlyCollection<Guid> studentIds, int year, int month)
    {
        var students = await _db.Students.AsNoTracking().Include(s => s.Category)
            .Where(s => s.AcademyId == academyId && studentIds.Contains(s.Id) && s.BillingPlan != StudentBillingPlan.Monthly)
            .ToListAsync();
        var ids = students.Select(s => s.Id).ToList();
        var attended = await AttendedAsync(academyId, ids, year, month);
        var field = await _db.PaymentRecords.AsNoTracking()
            .Where(p => p.AcademyId == academyId && ids.Contains(p.StudentId) && p.Type == PaymentType.Sessions && p.SessionDate != null
                        && p.PeriodYear == year && p.PeriodMonth == month)
            .GroupBy(p => p.StudentId).Select(g => new { g.Key, Total = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Total);
        return students.Select(s =>
        {
            attended.TryGetValue(s.Id, out var n);
            var c = Compute(s, n);
            return new MonthSummary(s.Id, $"{s.FirstName} {s.LastName}", s.BillingPlan.ToString(), year, month, n,
                SessionFeeOf(s), CapOf(s), s.Category?.PackageSessions, s.Category?.PackagePrice, c.Charged,
                field.GetValueOrDefault(s.Id), c.CapApplied);
        }).ToList();
    }
}
