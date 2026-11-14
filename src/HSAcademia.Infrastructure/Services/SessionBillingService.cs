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
    private readonly ReferralService? _referrals;

    public SessionBillingService(AppDbContext db, FinancialPeriodGuard periods, AuditService audit, ReferralService? referrals = null)
    {
        _referrals = referrals;
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

    /// <summary>Los partidos (amistosos y de torneo) no se cobran como sesión: sus costos van aparte.</summary>
    public static bool IsMatch(EventType type) => type is EventType.FriendlyMatch or EventType.TournamentMatch;
    public const string MatchNotSessionMessage = "Los partidos no se cobran como sesión: sus costos (arbitraje, cancha, pasaje) se cobran aparte.";

    /// <summary>
    /// Asistencias que cuentan como sesión (Presente o Tarde), una por sesión o por día sin sesión.
    /// Las de partidos no cuentan.
    /// </summary>
    public async Task<Dictionary<Guid, int>> AttendedAsync(Guid academyId, IReadOnlyCollection<Guid> studentIds, int year, int month)
    {
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1);
        var matches = _db.Events.IgnoreQueryFilters()
            .Where(e => e.AcademyId == academyId && (e.Type == EventType.FriendlyMatch || e.Type == EventType.TournamentMatch))
            .Select(e => e.Id);
        var rows = await _db.Attendances.AsNoTracking()
            .Where(a => a.AcademyId == academyId && studentIds.Contains(a.StudentId) && a.Date >= from && a.Date < to
                        && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late)
                        && (a.EventId == null || !matches.Contains(a.EventId.Value)))
            .Select(a => new { a.StudentId, a.EventId, a.Date })
            .ToListAsync();
        return rows.GroupBy(r => r.StudentId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.EventId.HasValue ? r.EventId.Value.ToString() : r.Date.Date.ToString("yyyy-MM-dd")).Distinct().Count());
    }

    // ─────────────────────────────────────────────────────────────
    // Cobro de una sesión en campo
    // ─────────────────────────────────────────────────────────────
    public sealed record FieldCharge(Guid DebtId, decimal Amount, bool AlreadyCharged, string Description);

    public async Task<FieldCharge> ChargeSessionAsync(Guid academyId, Guid studentId, Guid? eventId, DateTime? date,
        Guid? actorId = null, string? actorName = null)
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
            if (IsMatch(ev.Type)) throw new Exception(MatchNotSessionMessage);
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
        if (existing != null)
        {
            // Cobrar confirma que asistió: si estaba con falta, vuelve a Presente (y deja de ser saldo a favor).
            if (await MarkPresentAsync(academyId, studentId, eventId, sessionDate, actorId, actorName))
            {
                await _db.SaveChangesAsync();
                await ReconcileAsync(academyId, new[] { studentId });
            }
            return new FieldCharge(existing.Id, existing.Amount, true, existing.Description);
        }

        await _periods.EnsureOpenAsync(academyId, sessionDate.Year, sessionDate.Month, "cobrar la sesión");

        if (student.IsGuest || student.IsScholarship) throw new Exception("El alumno no paga sesiones (beca o invitado).");
        var amount = Math.Round(fee - (fee * Math.Min(student.ScholarshipPercentage ?? 0, 100m) / 100), 2);
        var discount = fee - amount;

        // Tope: lo cobrado en el mes no pasa de la mensualidad (las sesiones con falta, que son saldo a favor, no cuentan).
        var cap = CapOf(student);
        if (cap > 0)
        {
            var monthStart = new DateTime(sessionDate.Year, sessionDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var charged = await _db.PaymentRecords
                .Where(p => p.AcademyId == academyId && p.StudentId == studentId && p.Type == PaymentType.Sessions && p.SessionDate != null
                            && p.SessionCreditAmount == 0
                            && p.SessionDate >= monthStart && p.SessionDate < monthStart.AddMonths(1))
                .SumAsync(p => (decimal?)(p.Amount + p.DiscountAmount + p.SessionCreditApplied)) ?? 0m;
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
            SessionsCount = 1
        };
        // Saldo a favor de una sesión pagada a la que faltó: cubre esta.
        await ApplyCreditsAsync(record);
        if (record.Amount <= 0.001m)
        {
            record.IsPaid = true;
            record.PaidDate = DateTime.UtcNow;
        }
        _db.PaymentRecords.Add(record);
        await MarkPresentAsync(academyId, studentId, eventId, sessionDate, actorId, actorName);
        _audit.Add(academyId, "finances.sessions.charge", nameof(PaymentRecord), record.Id,
            new { studentId, eventId, sessionDate, amount = record.Amount, credit = record.SessionCreditApplied });
        await _db.SaveChangesAsync();
        if (_referrals != null)
        {
            // Alumno nuevo referido: su bienvenida se descuenta en esta primera sesión.
            await _referrals.TrySyncAsync(academyId, new[] { studentId });
            await _db.Entry(record).ReloadAsync();
        }
        return new FieldCharge(record.Id, record.Amount, false, record.Description);
    }

    /// <summary>Lo que se cobraría hoy por esta sesión (sin registrar nada): para mostrarlo antes de cobrar.</summary>
    public sealed record SessionQuote(decimal Fee, decimal Amount, bool AlreadyCharged, bool Paid, decimal PendingAmount,
        decimal CreditAvailable, bool CapReached, string? Note);

    public async Task<SessionQuote> QuoteAsync(Guid academyId, Guid studentId, Guid? eventId, DateTime? date)
    {
        var student = await _db.Students.AsNoTracking().Include(s => s.Category)
            .FirstOrDefaultAsync(s => s.Id == studentId && s.AcademyId == academyId)
            ?? throw new KeyNotFoundException("Alumno no encontrado.");
        var fee = SessionFeeOf(student);
        var credit = await _db.PaymentRecords.AsNoTracking()
            .Where(p => p.AcademyId == academyId && p.StudentId == studentId && p.SessionCreditAmount > 0 && p.SessionCreditUsedById == null)
            .SumAsync(p => (decimal?)p.SessionCreditAmount) ?? 0m;

        DateTime sessionDate = (date ?? AcademyClock.Today).Date;
        if (eventId.HasValue)
        {
            var ev = await _db.Events.AsNoTracking().Where(e => e.Id == eventId && e.AcademyId == academyId)
                         .Select(e => new { e.StartTime, e.Type }).FirstOrDefaultAsync()
                     ?? throw new KeyNotFoundException("Sesión no encontrada.");
            if (IsMatch(ev.Type)) throw new Exception(MatchNotSessionMessage);
            sessionDate = ev.StartTime.Date;
        }
        var sessionUtc = DateTime.SpecifyKind(sessionDate, DateTimeKind.Utc);
        var existing = await _db.PaymentRecords.AsNoTracking().FirstOrDefaultAsync(p =>
            p.AcademyId == academyId && p.StudentId == studentId && p.Type == PaymentType.Sessions && p.SessionDate != null &&
            (eventId.HasValue ? p.SessionEventId == eventId : p.SessionEventId == null && p.SessionDate == sessionUtc));
        if (existing != null)
            return new SessionQuote(fee, existing.Amount, true, existing.IsPaid, Math.Max(0, existing.Amount - existing.AmountPaid), credit, false,
                existing.IsPaid ? "Esta sesión ya está pagada." : "Esta sesión ya está cargada a su cuenta.");

        if (student.IsGuest || student.IsScholarship) return new SessionQuote(fee, 0, false, false, 0, credit, false, "No paga sesiones (beca o invitado).");
        var amount = Math.Round(fee - (fee * Math.Min(student.ScholarshipPercentage ?? 0, 100m) / 100), 2);
        var cap = CapOf(student);
        var capReached = false;
        string? note = null;
        if (cap > 0)
        {
            var monthStart = new DateTime(sessionDate.Year, sessionDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var charged = await _db.PaymentRecords.AsNoTracking()
                .Where(p => p.AcademyId == academyId && p.StudentId == studentId && p.Type == PaymentType.Sessions && p.SessionDate != null
                            && p.SessionCreditAmount == 0 && p.SessionDate >= monthStart && p.SessionDate < monthStart.AddMonths(1))
                .SumAsync(p => (decimal?)(p.Amount + p.DiscountAmount + p.SessionCreditApplied)) ?? 0m;
            if (charged >= cap - 0.001m) { capReached = true; amount = 0; note = $"Ya alcanzó el tope del mes (S/ {cap:0.00})."; }
            else if (charged + fee > cap)
            {
                var gross = cap - charged;
                amount = Math.Round(gross - (gross * Math.Min(student.ScholarshipPercentage ?? 0, 100m) / 100), 2);
                note = "Llega al tope del mes.";
            }
        }
        var usable = await _db.PaymentRecords.AsNoTracking()
            .Where(p => p.AcademyId == academyId && p.StudentId == studentId && p.SessionCreditAmount > 0 && p.SessionCreditUsedById == null
                        && p.SessionCreditAmount <= amount + 0.001m)
            .OrderBy(p => p.SessionDate).Select(p => (decimal?)p.SessionCreditAmount).FirstOrDefaultAsync() ?? 0m;
        if (usable > 0 && !capReached)
        {
            amount -= usable;
            note = $"Se usa su saldo a favor de S/ {usable:0.00}.";
        }
        return new SessionQuote(fee, Math.Max(0, amount), false, false, Math.Max(0, amount), credit, capReached, note);
    }

    // ─────────────────────────────────────────────────────────────
    // Asistencia ↔ cobro en campo
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Cobrar una sesión confirma que el alumno asistió: si no tenía marca, queda Presente;
    /// si tenía Falta o Justificada, pasa a Presente. No guarda. Devuelve si cambió algo.
    /// </summary>
    private async Task<bool> MarkPresentAsync(Guid academyId, Guid studentId, Guid? eventId, DateTime sessionDate, Guid? actorId, string? actorName)
    {
        var day = sessionDate.Date;
        var rows = await _db.Attendances
            .Where(a => a.AcademyId == academyId && a.StudentId == studentId &&
                        (eventId.HasValue ? a.EventId == eventId || (a.EventId == null && a.Date == day) : a.EventId == null && a.Date == day))
            .ToListAsync();
        var row = rows.OrderByDescending(a => a.EventId == eventId).FirstOrDefault();
        var now = DateTime.UtcNow;
        var who = string.IsNullOrWhiteSpace(actorName) ? "Cobro de sesión" : $"{actorName} (cobro)";
        if (row == null)
        {
            _db.Attendances.Add(new Attendance
            {
                AcademyId = academyId, StudentId = studentId, EventId = eventId, Date = day,
                Status = AttendanceStatus.Present, Source = AttendanceSource.List,
                MarkedByUserId = actorId, MarkedByName = who, MarkedAt = now,
                Notes = "Marcado al cobrar la sesión", CreatedAt = now, UpdatedAt = now
            });
            return true;
        }
        if (row.Status is AttendanceStatus.Present or AttendanceStatus.Late) return false;
        _audit.Add(academyId, "attendance.charge.present", nameof(Attendance), row.Id, new { studentId, eventId, from = row.Status.ToString() });
        row.Status = AttendanceStatus.Present;
        row.Source = AttendanceSource.List;
        row.MarkedByUserId = actorId;
        row.MarkedByName = who;
        row.MarkedAt = now;
        row.UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// Concilia las sesiones cobradas en campo con la asistencia (últimos 2 meses):
    /// - Falta/Justificada en una sesión no pagada: la deuda se anula (no se cobra lo que no asistió).
    /// - Falta/Justificada en una sesión pagada: lo pagado queda como saldo a favor para su próxima sesión o liquidación.
    /// - Si vuelve a Presente/Tarde: el saldo a favor se retira (y si ya se usó, la deuda que lo usó vuelve a cobrarse).
    /// Idempotente; guarda.
    /// </summary>
    public async Task ReconcileAsync(Guid academyId, IEnumerable<Guid>? studentIds = null, CancellationToken ct = default)
    {
        var ids = studentIds?.Distinct().ToList();
        if (ids is { Count: 0 }) return;
        var since = DateTime.SpecifyKind(AcademyClock.Today.AddDays(-62), DateTimeKind.Utc);
        var q = _db.PaymentRecords.Where(p => p.AcademyId == academyId && p.Type == PaymentType.Sessions && p.SessionDate != null && p.SessionDate >= since);
        if (ids != null) q = q.Where(p => ids.Contains(p.StudentId));
        var records = await q.ToListAsync(ct);
        if (records.Count == 0) return;

        var studentSet = records.Select(r => r.StudentId).Distinct().ToList();
        var sinceDay = since.Date;
        var marks = await _db.Attendances.AsNoTracking()
            .Where(a => a.AcademyId == academyId && studentSet.Contains(a.StudentId) && a.Date >= sinceDay)
            .Select(a => new { a.StudentId, a.EventId, a.Date, a.Status })
            .ToListAsync(ct);
        var changed = false;

        foreach (var rec in records)
        {
            var day = rec.SessionDate!.Value.Date;
            var mine = marks.Where(a => a.StudentId == rec.StudentId &&
                    (rec.SessionEventId.HasValue ? a.EventId == rec.SessionEventId || (a.EventId == null && a.Date == day) : a.EventId == null && a.Date == day))
                .OrderByDescending(a => a.EventId == rec.SessionEventId).FirstOrDefault();
            var absent = mine != null && mine.Status is AttendanceStatus.Absent or AttendanceStatus.Excused;

            if (absent)
            {
                if (rec.SessionCreditAmount > 0) continue; // ya es saldo a favor
                if (rec.AmountPaid <= 0.001m)
                {
                    // Sin pagos: no se cobra lo que no asistió. Lo cubierto con saldo a favor se libera.
                    await ReleaseCreditsUsedByAsync(rec);
                    rec.IsDeleted = true;
                    rec.DeletedAt = DateTime.UtcNow;
                    rec.UpdatedAt = DateTime.UtcNow;
                    _audit.Add(academyId, "finances.sessions.absent.cancel", nameof(PaymentRecord), rec.Id, new { rec.StudentId, rec.SessionEventId, day });
                }
                else
                {
                    if (rec.AmountPaid < rec.Amount - 0.01m)
                    {
                        // Pagó una parte: el resto deja de cobrarse.
                        rec.DiscountAmount += rec.Amount - rec.AmountPaid;
                        rec.Amount = rec.AmountPaid;
                    }
                    rec.IsPaid = true;
                    rec.PaidDate ??= DateTime.UtcNow;
                    rec.SessionCreditAmount = rec.AmountPaid + rec.SessionCreditApplied;
                    rec.DiscountDetail = Truncate(JoinLabel(rec.DiscountDetail, "No asistió: saldo a favor"), 200);
                    rec.UpdatedAt = DateTime.UtcNow;
                    _audit.Add(academyId, "finances.sessions.absent.credit", nameof(PaymentRecord), rec.Id, new { rec.StudentId, credit = rec.SessionCreditAmount });
                }
                changed = true;
            }
            else if (rec.SessionCreditAmount > 0)
            {
                // Volvió a Presente: deja de ser saldo a favor.
                if (rec.SessionCreditUsedById.HasValue)
                {
                    var user = await _db.PaymentRecords.FirstOrDefaultAsync(p => p.Id == rec.SessionCreditUsedById, ct);
                    if (user != null) Unapply(user, rec.SessionCreditAmount);
                }
                rec.SessionCreditAmount = 0;
                rec.SessionCreditUsedById = null;
                rec.DiscountDetail = RemoveLabel(rec.DiscountDetail, "No asistió: saldo a favor");
                rec.UpdatedAt = DateTime.UtcNow;
                _audit.Add(academyId, "finances.sessions.absent.revert", nameof(PaymentRecord), rec.Id, new { rec.StudentId });
                changed = true;
            }
        }
        if (changed) await _db.SaveChangesAsync(ct);
    }

    /// <summary>Conciliación de respaldo de todas las academias con cobros de sesión recientes.</summary>
    public async Task<int> ReconcileAllAsync(CancellationToken ct = default)
    {
        var since = DateTime.SpecifyKind(AcademyClock.Today.AddDays(-62), DateTimeKind.Utc);
        var academies = await _db.PaymentRecords.AsNoTracking()
            .Where(p => p.Type == PaymentType.Sessions && p.SessionDate != null && p.SessionDate >= since)
            .Select(p => p.AcademyId).Distinct().ToListAsync(ct);
        foreach (var a in academies)
        {
            await TryReconcileAsync(a, null);
            _db.ChangeTracker.Clear();
        }
        return academies.Count;
    }

    /// <summary>Igual que <see cref="ReconcileAsync"/>, pero un fallo nunca interrumpe la operación que lo llamó.</summary>
    public async Task TryReconcileAsync(Guid academyId, IEnumerable<Guid>? studentIds)
    {
        try { await ReconcileAsync(academyId, studentIds); }
        catch (Exception) { _db.ChangeTracker.Clear(); }
    }

    /// <summary>Usa los saldos a favor disponibles del alumno en esta deuda (cada uno completo, si cabe). No guarda.</summary>
    private async Task ApplyCreditsAsync(PaymentRecord record)
    {
        var credits = await _db.PaymentRecords
            .Where(p => p.AcademyId == record.AcademyId && p.StudentId == record.StudentId && p.Id != record.Id
                        && p.SessionCreditAmount > 0 && p.SessionCreditUsedById == null)
            .OrderBy(p => p.SessionDate).ToListAsync();
        foreach (var credit in credits)
        {
            var pending = record.Amount - record.AmountPaid;
            if (credit.SessionCreditAmount > pending + 0.001m) continue;
            record.Amount -= credit.SessionCreditAmount;
            record.SessionCreditApplied += credit.SessionCreditAmount;
            record.DiscountDetail = Truncate(JoinLabel(record.DiscountDetail, $"Saldo a favor sesión {credit.SessionDate:dd/MM}"), 200);
            credit.SessionCreditUsedById = record.Id;
            credit.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>Libera los saldos a favor que usaba esta deuda (vuelve a cobrarse esa parte). No guarda.</summary>
    private async Task ReleaseCreditsUsedByAsync(PaymentRecord record)
    {
        if (record.SessionCreditApplied <= 0) return;
        var credits = await _db.PaymentRecords.Where(p => p.SessionCreditUsedById == record.Id).ToListAsync();
        foreach (var c in credits)
        {
            c.SessionCreditUsedById = null;
            c.UpdatedAt = DateTime.UtcNow;
            record.DiscountDetail = RemoveLabel(record.DiscountDetail, $"Saldo a favor sesión {c.SessionDate:dd/MM}");
        }
        record.Amount += record.SessionCreditApplied;
        record.SessionCreditApplied = 0;
        if (record.AmountPaid < record.Amount - 0.01m) { record.IsPaid = false; record.PaidDate = null; }
    }

    private static void Unapply(PaymentRecord user, decimal amount)
    {
        var take = Math.Min(amount, user.SessionCreditApplied);
        user.SessionCreditApplied -= take;
        user.Amount += take;
        if (user.AmountPaid < user.Amount - 0.01m) { user.IsPaid = false; user.PaidDate = null; }
        user.UpdatedAt = DateTime.UtcNow;
    }

    private static string JoinLabel(string? a, string b) => string.IsNullOrWhiteSpace(a) ? b : a.Contains(b) ? a : $"{a} · {b}";
    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
    private static string? RemoveLabel(string? detail, string label)
    {
        if (string.IsNullOrEmpty(detail)) return detail;
        var parts = detail.Split(" · ").Where(p => p != label).ToList();
        return parts.Count == 0 ? null : string.Join(" · ", parts);
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
            // Lo cobrado en campo cuenta sin restar beneficios de referidos (esos los asume la academia).
            // Las sesiones con falta (saldo a favor) no cuentan; las cubiertas con saldo a favor cuentan completas.
            var fieldCharged = mine.Where(p => p.Type == PaymentType.Sessions && p.SessionDate != null && p.SessionCreditAmount == 0)
                .Sum(p => p.Amount + p.ReferralDiscount + p.SessionCreditApplied);
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
                    await ReleaseCreditsUsedByAsync(liquidation);
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
                await ApplyCreditsAsync(record);
                if (record.Amount <= 0.001m) { record.IsPaid = true; record.PaidDate = DateTime.UtcNow; }
                _db.PaymentRecords.Add(record);
                newOnes.Add((record, s));
                generated++;
            }
            else if (liquidation.Amount + liquidation.ReferralDiscount + liquidation.SessionCreditApplied != remaining || liquidation.SessionsCount != count
                     || !(liquidation.DiscountDetail ?? "").StartsWith(detail))
            {
                await ReleaseCreditsUsedByAsync(liquidation);
                liquidation.Amount = remaining;
                liquidation.DiscountAmount = c.Discount;
                liquidation.DiscountDetail = detail;
                liquidation.SessionsCount = count;
                liquidation.Description = description;
                if (_referrals != null) await _referrals.ReapplyAsync(liquidation);
                await ApplyCreditsAsync(liquidation);
                if (liquidation.AmountPaid >= liquidation.Amount - 0.01m)
                {
                    liquidation.AmountPaid = liquidation.Amount;
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
        if (_referrals != null) await _referrals.TrySyncAsync(academyId, ids);
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
        decimal SessionFee, decimal Cap, int? PackageSessions, decimal? PackagePrice, decimal Accrued, decimal PaidInField, bool CapReached,
        decimal CreditAvailable = 0);

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
            .Where(p => p.SessionCreditAmount == 0)
            .GroupBy(p => p.StudentId).Select(g => new { g.Key, Total = g.Sum(p => p.Amount + p.SessionCreditApplied) })
            .ToDictionaryAsync(x => x.Key, x => x.Total);
        var credits = await _db.PaymentRecords.AsNoTracking()
            .Where(p => p.AcademyId == academyId && ids.Contains(p.StudentId) && p.SessionCreditAmount > 0 && p.SessionCreditUsedById == null)
            .GroupBy(p => p.StudentId).Select(g => new { g.Key, Total = g.Sum(p => p.SessionCreditAmount) })
            .ToDictionaryAsync(x => x.Key, x => x.Total);
        return students.Select(s =>
        {
            attended.TryGetValue(s.Id, out var n);
            var c = Compute(s, n);
            return new MonthSummary(s.Id, $"{s.FirstName} {s.LastName}", s.BillingPlan.ToString(), year, month, n,
                SessionFeeOf(s), CapOf(s), s.Category?.PackageSessions, s.Category?.PackagePrice, c.Charged,
                field.GetValueOrDefault(s.Id), c.CapApplied, credits.GetValueOrDefault(s.Id));
        }).ToList();
    }
}
