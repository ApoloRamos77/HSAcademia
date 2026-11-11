using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Planilla del personal: sueldo fijo, por hora, por sesión o mixto, con frecuencia diaria, semanal,
/// quincenal o mensual. Las horas salen de la asistencia de entrenadores (el reemplazo cobra la sesión que dictó)
/// y se pagan con el tarifario por tramos del mismo día. Flujo: Borrador → Aprobada → Pagada.
/// </summary>
public class PayrollService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] MethodsAllowed = { "Cash", "BankTransfer", "Yape", "Plin", "Other" };

    private readonly AppDbContext _db;
    private readonly FinancialPeriodGuard _periods;
    private readonly AuditService _audit;

    public PayrollService(AppDbContext db, FinancialPeriodGuard periods, AuditService audit)
    {
        _db = db;
        _periods = periods;
        _audit = audit;
    }

    // ─────────────────────────────────────────────────────────────
    // Periodos
    // ─────────────────────────────────────────────────────────────
    public sealed record Period(DateTime Start, DateTime End);

    /// <summary>Periodo de la frecuencia que contiene la fecha (fechas locales, ambos extremos inclusive).</summary>
    public static Period PeriodContaining(PayFrequency f, DateTime date)
    {
        var d = date.Date;
        switch (f)
        {
            case PayFrequency.Daily: return new Period(d, d);
            case PayFrequency.Weekly:
                var monday = d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
                return new Period(monday, monday.AddDays(6));
            case PayFrequency.Biweekly:
                return d.Day <= 15
                    ? new Period(new DateTime(d.Year, d.Month, 1), new DateTime(d.Year, d.Month, 15))
                    : new Period(new DateTime(d.Year, d.Month, 16), new DateTime(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month)));
            default:
                return new Period(new DateTime(d.Year, d.Month, 1), new DateTime(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month)));
        }
    }

    /// <summary>Parte fija del sueldo mensual según la frecuencia.</summary>
    public static decimal FixedFor(PayFrequency f, decimal monthly) => Math.Round(f switch
    {
        PayFrequency.Biweekly => monthly / 2m,
        PayFrequency.Weekly => monthly * 12m / 52m,
        PayFrequency.Daily => monthly / 30m,
        _ => monthly
    }, 2);

    // ─────────────────────────────────────────────────────────────
    // Tarifarios
    // ─────────────────────────────────────────────────────────────
    public sealed class ScaleInput
    {
        public string Name { get; set; } = string.Empty;
        public PayUnit Unit { get; set; }
        public List<PayTier> Tiers { get; set; } = new();
        public bool IsDefault { get; set; }
    }

    public Task<List<PayScale>> ScalesAsync(Guid academyId) =>
        _db.PayScales.AsNoTracking().Where(s => s.AcademyId == academyId).OrderBy(s => s.Unit).ThenByDescending(s => s.IsDefault).ThenBy(s => s.Name).ToListAsync();

    public async Task<PayScale> SaveScaleAsync(Guid academyId, Guid? id, ScaleInput input)
    {
        var name = (input.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > 100) throw new Exception("Indica un nombre para el tarifario (hasta 100 caracteres).");
        if (!Enum.IsDefined(input.Unit)) throw new Exception("Unidad no válida.");
        var tiers = (input.Tiers ?? new()).OrderBy(t => t.From).ToList();
        if (tiers.Count == 0) throw new Exception("El tarifario necesita al menos una tarifa.");
        if (tiers[0].From != 1) throw new Exception("La primera tarifa debe empezar en la 1.ª hora o sesión del día.");
        if (tiers.Select(t => t.From).Distinct().Count() != tiers.Count) throw new Exception("Hay dos tramos que empiezan en la misma posición.");
        if (tiers.Any(t => t.From < 1 || t.From > 24 || t.Rate < 0 || t.Rate > 100000)) throw new Exception("Revisa los tramos: posición de 1 a 24 y tarifa no negativa.");

        PayScale scale;
        if (id.HasValue)
        {
            scale = await _db.PayScales.FirstOrDefaultAsync(s => s.Id == id && s.AcademyId == academyId)
                ?? throw new KeyNotFoundException("Tarifario no encontrado.");
        }
        else
        {
            scale = new PayScale { AcademyId = academyId };
            _db.PayScales.Add(scale);
        }
        if (await _db.PayScales.AnyAsync(s => s.AcademyId == academyId && s.Id != scale.Id && s.Name.ToLower() == name.ToLower()))
            throw new Exception("Ya existe un tarifario con ese nombre.");
        scale.Name = name;
        scale.Unit = input.Unit;
        scale.Tiers = tiers.Select(t => new PayTier { From = t.From, Rate = Math.Round(t.Rate, 2) }).ToList();
        scale.UpdatedAt = DateTime.UtcNow;
        // Solo un predeterminado por unidad.
        if (input.IsDefault)
        {
            var others = await _db.PayScales.Where(s => s.AcademyId == academyId && s.Unit == input.Unit && s.Id != scale.Id && s.IsDefault).ToListAsync();
            others.ForEach(o => o.IsDefault = false);
        }
        scale.IsDefault = input.IsDefault;
        _audit.Add(academyId, id.HasValue ? "payroll.scale.updated" : "payroll.scale.created", nameof(PayScale), scale.Id, new { scale.Name, scale.Unit, scale.Tiers, scale.IsDefault });
        await _db.SaveChangesAsync();
        return scale;
    }

    public async Task DeleteScaleAsync(Guid academyId, Guid id)
    {
        var scale = await _db.PayScales.FirstOrDefaultAsync(s => s.Id == id && s.AcademyId == academyId)
            ?? throw new KeyNotFoundException("Tarifario no encontrado.");
        if (await _db.Users.AnyAsync(u => u.AcademyId == academyId && u.PayScaleId == id && !u.IsDeleted))
            throw new Exception("Hay personal con este tarifario asignado. Cámbialo antes de eliminarlo.");
        scale.IsDeleted = true;
        scale.DeletedAt = DateTime.UtcNow;
        _audit.Add(academyId, "payroll.scale.deleted", nameof(PayScale), id, new { scale.Name });
        await _db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Cálculo
    // ─────────────────────────────────────────────────────────────
    public sealed record SessionLine(Guid EventId, string Title, string Time, decimal Hours, string Role, bool Late, decimal Amount, string Rate);
    public sealed record DayLine(DateTime Date, decimal Hours, int Sessions, decimal Amount, List<SessionLine> Items);
    public sealed record AdvanceLine(Guid Id, DateTime Date, string Concept, decimal Amount);
    public sealed record Calculation(
        Guid StaffId, string StaffName, string Scheme, string Frequency, DateTime PeriodStart, DateTime PeriodEnd,
        string RateLabel, decimal BaseAmount, decimal UnitsAmount, decimal Hours, int Sessions,
        List<DayLine> Days, List<AdvanceLine> PendingAdvances, List<string> Warnings);

    private async Task<User> StaffAsync(Guid academyId, Guid staffId) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Id == staffId && u.AcademyId == academyId && !u.IsDeleted
            && (u.Role == UserRole.Staff || u.Role == UserRole.AcademyAdmin))
        ?? throw new KeyNotFoundException("Colaborador no encontrado.");

    private async Task<PayScale?> ScaleForAsync(User staff)
    {
        if (staff.PaymentType == StaffPaymentType.Monthly) return null;
        var unit = staff.PaymentType == StaffPaymentType.PerSession ? PayUnit.Session : PayUnit.Hour;
        if (staff.PayScaleId.HasValue)
        {
            var own = await _db.PayScales.AsNoTracking().FirstOrDefaultAsync(s => s.Id == staff.PayScaleId && s.AcademyId == staff.AcademyId);
            if (own != null && own.Unit == unit) return own;
        }
        return await _db.PayScales.AsNoTracking().FirstOrDefaultAsync(s => s.AcademyId == staff.AcademyId && s.Unit == unit && s.IsDefault);
    }

    public static string SchemeLabel(StaffPaymentType t) => t switch
    {
        StaffPaymentType.PerSession => "Por sesión",
        StaffPaymentType.PerHour => "Por hora",
        StaffPaymentType.Mixed => "Sueldo fijo + horas",
        _ => "Sueldo fijo"
    };

    public static string FrequencyLabel(PayFrequency f) => f switch
    {
        PayFrequency.Biweekly => "Quincenal",
        PayFrequency.Weekly => "Semanal",
        PayFrequency.Daily => "Diario",
        _ => "Mensual"
    };

    private static string Money(decimal v) => $"S/ {v:0.00}";

    public async Task<Calculation> CalculateAsync(Guid academyId, Guid staffId, DateTime start, DateTime end, Guid? excludePaymentId = null)
    {
        start = start.Date; end = end.Date;
        if (end < start) throw new Exception("El fin del periodo no puede ser anterior al inicio.");
        if ((end - start).TotalDays > 31) throw new Exception("El periodo no puede pasar de 31 días.");
        var staff = await StaffAsync(academyId, staffId);
        var warnings = new List<string>();
        var scheme = staff.PaymentType;

        var baseAmount = scheme is StaffPaymentType.Monthly or StaffPaymentType.Mixed ? FixedFor(staff.PayFrequency, staff.PaymentRate) : 0m;
        if (scheme is StaffPaymentType.Monthly or StaffPaymentType.Mixed && staff.PaymentRate <= 0) warnings.Add("La ficha laboral no tiene sueldo fijo.");
        if (scheme is StaffPaymentType.Monthly or StaffPaymentType.Mixed)
        {
            var natural = PeriodContaining(staff.PayFrequency, start);
            if (natural.Start != start || natural.End != end)
                warnings.Add($"El periodo no coincide con su frecuencia ({FrequencyLabel(staff.PayFrequency).ToLower()}): el sueldo fijo se calcula igual por periodo completo.");
        }

        var days = new List<DayLine>();
        decimal unitsAmount = 0, hours = 0;
        int sessions = 0;
        string rateLabel = scheme == StaffPaymentType.Monthly ? $"{Money(staff.PaymentRate)} al mes" : string.Empty;

        if (scheme != StaffPaymentType.Monthly)
        {
            var scale = await ScaleForAsync(staff);
            var byHour = scheme != StaffPaymentType.PerSession;
            Func<int, decimal> rateFor;
            if (scale != null)
            {
                rateFor = scale.RateFor;
                rateLabel = $"{scale.Name}: " + string.Join(" · ", scale.Tiers.OrderBy(t => t.From)
                    .Select(t => $"{(t.From == 1 ? "1.ª" : $"desde la {t.From}.ª")} {(byHour ? "hora" : "sesión")} {Money(t.Rate)}"));
            }
            else if (scheme != StaffPaymentType.Mixed && staff.PaymentRate > 0)
            {
                rateFor = _ => staff.PaymentRate;
                rateLabel = $"{Money(staff.PaymentRate)} por {(byHour ? "hora" : "sesión")}";
            }
            else
            {
                rateFor = _ => 0m;
                rateLabel = "Sin tarifa";
                warnings.Add(byHour ? "No hay tarifario por hora (ni propio ni predeterminado)." : "No hay tarifa por sesión configurada.");
            }

            // Las horas del calendario se guardan como hora local marcada UTC (igual que el resto del sistema).
            var from = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            var to = DateTime.SpecifyKind(end.AddDays(1), DateTimeKind.Utc);
            var events = (await _db.Events.AsNoTracking()
                    .Where(e => e.AcademyId == academyId && !e.IsDeleted && e.StartTime >= from && e.StartTime < to)
                    .ToListAsync())
                .Where(e => e.Type is EventType.Training or EventType.FriendlyMatch or EventType.TournamentMatch)
                .ToDictionary(e => e.Id);
            var ids = events.Keys.ToList();
            var marks = await _db.StaffAttendances.AsNoTracking()
                .Where(a => a.AcademyId == academyId && a.EventId != null && ids.Contains(a.EventId.Value))
                .ToListAsync();
            var markedEvents = marks.Select(m => m.EventId!.Value).ToHashSet();

            var unmarked = events.Values.Count(e => e.TeacherId == staffId && !markedEvents.Contains(e.Id) && e.StartTime <= AcademyClock.WallNow);
            if (unmarked > 0) warnings.Add($"{unmarked} sesión(es) suya(s) sin asistencia del entrenador: no se pagan hasta marcarlas.");

            var mine = marks.Where(m =>
                    (m.StaffId == staffId && m.SubstituteStaffId == null && m.Status is AttendanceStatus.Present or AttendanceStatus.Late)
                    || m.SubstituteStaffId == staffId)
                .ToList();
            var paidElsewhere = mine.Count(m => m.StaffPaymentId != null && m.StaffPaymentId != excludePaymentId);
            if (paidElsewhere > 0) warnings.Add($"{paidElsewhere} sesión(es) ya pagada(s) en otra liquidación: no se vuelven a pagar.");
            mine = mine.Where(m => m.StaffPaymentId == null || m.StaffPaymentId == excludePaymentId).ToList();

            foreach (var day in mine.Select(m => (Mark: m, Event: events[m.EventId!.Value]))
                         .GroupBy(x => x.Event.StartTime.Date).OrderBy(g => g.Key))
            {
                var items = new List<SessionLine>();
                decimal cumulative = 0;
                var position = 0;
                foreach (var (mark, ev) in day.OrderBy(x => x.Event.StartTime))
                {
                    var h = Math.Max(0m, (decimal)(ev.EndTime - ev.StartTime).TotalHours);
                    h = Math.Round(h, 2);
                    if (h == 0 && byHour) warnings.Add($"\"{ev.Title}\" del {ev.StartTime:dd/MM} no tiene duración: revisa su hora de fin.");
                    decimal amount = 0;
                    string rateText;
                    if (byHour)
                    {
                        var remaining = h;
                        var parts = new List<(decimal Hours, decimal Rate)>();
                        while (remaining > 0)
                        {
                            var pos = (int)Math.Floor(cumulative) + 1;
                            var chunk = Math.Min(remaining, pos - cumulative);
                            var rate = rateFor(pos);
                            amount += chunk * rate;
                            if (parts.Count > 0 && parts[^1].Rate == rate) parts[^1] = (parts[^1].Hours + chunk, rate);
                            else parts.Add((chunk, rate));
                            cumulative += chunk;
                            remaining -= chunk;
                        }
                        rateText = string.Join(" + ", parts.Select(x => $"{x.Hours:0.##} h × {Money(x.Rate)}"));
                    }
                    else
                    {
                        position++;
                        var rate = rateFor(position);
                        amount = rate;
                        rateText = $"{position}.ª sesión del día × {Money(rate)}";
                    }
                    amount = Math.Round(amount, 2);
                    items.Add(new SessionLine(ev.Id, ev.Title, $"{ev.StartTime:HH:mm}–{ev.EndTime:HH:mm}", h,
                        mark.SubstituteStaffId == staffId ? "Reemplazo" : "Titular", mark.Status == AttendanceStatus.Late && mark.SubstituteStaffId != staffId, amount, rateText));
                }
                var dayHours = items.Sum(i => i.Hours);
                var dayAmount = items.Sum(i => i.Amount);
                days.Add(new DayLine(day.Key, dayHours, items.Count, dayAmount, items));
                hours += dayHours;
                sessions += items.Count;
                unitsAmount += dayAmount;
            }
        }

        var advances = await _db.StaffAdvances.AsNoTracking()
            .Where(a => a.AcademyId == academyId && a.StaffId == staffId && a.StaffPaymentId == null && a.Date <= end)
            .OrderBy(a => a.Date)
            .Select(a => new AdvanceLine(a.Id, a.Date, a.Concept, a.Amount))
            .ToListAsync();

        return new Calculation(staffId, $"{staff.FirstName} {staff.LastName}", SchemeLabel(scheme), FrequencyLabel(staff.PayFrequency),
            start, end, rateLabel, baseAmount, Math.Round(unitsAmount, 2), hours, sessions, days, advances, warnings);
    }

    public async Task<Period> SuggestPeriodAsync(Guid academyId, Guid staffId)
    {
        var staff = await StaffAsync(academyId, staffId);
        var lastEnd = await _db.StaffPayments.Where(p => p.AcademyId == academyId && p.StaffId == staffId)
            .OrderByDescending(p => p.PeriodEnd).Select(p => (DateTime?)p.PeriodEnd).FirstOrDefaultAsync();
        var today = AcademyClock.Today;
        // Lo siguiente a la última liquidación; sin historial, el último periodo ya terminado (se paga lo trabajado).
        if (lastEnd.HasValue) return PeriodContaining(staff.PayFrequency, lastEnd.Value.Date.AddDays(1));
        return PeriodContaining(staff.PayFrequency, PeriodContaining(staff.PayFrequency, today).Start.AddDays(-1));
    }

    // ─────────────────────────────────────────────────────────────
    // Liquidaciones
    // ─────────────────────────────────────────────────────────────
    public sealed class AdjustmentInput
    {
        public StaffPaymentLineKind Kind { get; set; }
        public string Concept { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public sealed class PaymentInput
    {
        public Guid StaffId { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public List<AdjustmentInput> Adjustments { get; set; } = new();
        public string? Notes { get; set; }
    }

    public sealed record PaymentDto(
        Guid Id, Guid StaffId, string StaffName, DateTime PeriodStart, DateTime PeriodEnd, string Frequency, string Status,
        decimal BaseAmount, decimal UnitsAmount, decimal? Hours, int? Sessions, decimal Bonuses, decimal Deductions, decimal Total,
        List<LineDto> Lines, List<DayLine>? Days, string? Notes, string? PaymentMethod, DateTime? PaidAt, DateTime? ApprovedAt, string? ApprovedByName,
        int PeriodMonth, int PeriodYear);
    public sealed record LineDto(Guid Id, string Kind, string Concept, decimal Amount, Guid? StaffAdvanceId);

    private static PaymentDto ToDto(StaffPayment p, bool withDetail) => new(
        p.Id, p.StaffId, p.Staff != null ? $"{p.Staff.FirstName} {p.Staff.LastName}" : "",
        p.PeriodStart, p.PeriodEnd, FrequencyLabel(p.Frequency),
        p.Status switch { StaffPaymentStatus.Draft => "Draft", StaffPaymentStatus.Paid => "Paid", _ => "Approved" },
        p.BaseAmount, p.UnitsAmount, p.Hours, p.SessionsCount, p.Bonuses, p.Deductions, p.TotalPaid,
        p.Lines.Select(l => new LineDto(l.Id, l.Kind.ToString(), l.Concept, l.Amount, l.StaffAdvanceId)).ToList(),
        withDetail && p.Detail != null ? JsonSerializer.Deserialize<List<DayLine>>(p.Detail, Json) : null,
        p.Notes, p.PaymentMethod, p.PaidAt, p.ApprovedAt, p.ApprovedByName, p.PeriodMonth, p.PeriodYear);

    public async Task<List<PaymentDto>> ListAsync(Guid academyId, DateTime from, DateTime to, Guid? staffId = null, bool includeDrafts = true)
    {
        var q = _db.StaffPayments.AsNoTracking().Include(p => p.Staff).Include(p => p.Lines)
            .Where(p => p.AcademyId == academyId && p.PeriodEnd >= from && p.PeriodStart <= to);
        if (staffId.HasValue) q = q.Where(p => p.StaffId == staffId);
        if (!includeDrafts) q = q.Where(p => p.Status != StaffPaymentStatus.Draft);
        return (await q.OrderByDescending(p => p.PeriodStart).ThenBy(p => p.Staff.FirstName).ToListAsync()).Select(p => ToDto(p, false)).ToList();
    }

    public async Task<PaymentDto> GetAsync(Guid academyId, Guid id) =>
        ToDto(await LoadAsync(academyId, id), true);

    private async Task<StaffPayment> LoadAsync(Guid academyId, Guid id) =>
        await _db.StaffPayments.Include(p => p.Staff).Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id && p.AcademyId == academyId)
        ?? throw new KeyNotFoundException("Liquidación no encontrada.");

    private static List<StaffPaymentLine> ManualLines(List<AdjustmentInput>? adjustments)
    {
        var lines = new List<StaffPaymentLine>();
        foreach (var a in adjustments ?? new())
        {
            if (a.Kind == StaffPaymentLineKind.Advance) throw new Exception("Los adelantos se registran aparte y se descuentan solos.");
            if (!Enum.IsDefined(a.Kind)) throw new Exception("Tipo de ajuste no válido.");
            var concept = (a.Concept ?? string.Empty).Trim();
            if (concept.Length == 0 || concept.Length > 150) throw new Exception("Cada bono o descuento necesita un concepto (hasta 150 caracteres).");
            if (a.Amount <= 0 || a.Amount > 100000) throw new Exception($"El monto de \"{concept}\" debe ser mayor a cero.");
            lines.Add(new StaffPaymentLine { Kind = a.Kind, Concept = concept, Amount = Math.Round(a.Amount, 2) });
        }
        return lines;
    }

    /// <summary>Recalcula horas, sueldo, adelantos y totales a partir de la asistencia actual.</summary>
    private async Task ApplyCalculationAsync(StaffPayment p, User staff, List<StaffPaymentLine> manual)
    {
        var calc = await CalculateAsync(p.AcademyId, p.StaffId, p.PeriodStart, p.PeriodEnd, p.Id);
        p.Frequency = staff.PayFrequency;
        p.BaseAmount = calc.BaseAmount;
        p.UnitsAmount = calc.UnitsAmount;
        p.Hours = staff.PaymentType == StaffPaymentType.Monthly ? null : calc.Hours;
        p.SessionsCount = staff.PaymentType == StaffPaymentType.Monthly ? null : calc.Sessions;
        p.Detail = JsonSerializer.Serialize(calc.Days, Json);

        _db.StaffPaymentLines.RemoveRange(p.Lines);
        p.Lines = manual.Concat(calc.PendingAdvances.Select(a => new StaffPaymentLine
        {
            Kind = StaffPaymentLineKind.Advance, Concept = $"Adelanto {a.Date:dd/MM} · {a.Concept}", Amount = a.Amount, StaffAdvanceId = a.Id
        })).Select(l => { l.Id = Guid.NewGuid(); l.StaffPaymentId = p.Id; return l; }).ToList();
        _db.StaffPaymentLines.AddRange(p.Lines);

        p.Bonuses = p.Lines.Where(l => l.Kind == StaffPaymentLineKind.Bonus).Sum(l => l.Amount);
        p.Deductions = p.Lines.Where(l => l.Kind != StaffPaymentLineKind.Bonus).Sum(l => l.Amount);
        p.TotalPaid = p.BaseAmount + p.UnitsAmount + p.Bonuses - p.Deductions;
        if (p.TotalPaid < 0) throw new Exception("Los descuentos y adelantos superan lo que le corresponde en el periodo.");
        p.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<PaymentDto> CreateAsync(Guid academyId, PaymentInput input)
    {
        var staff = await StaffAsync(academyId, input.StaffId);
        var start = input.PeriodStart.Date; var end = input.PeriodEnd.Date;
        await _periods.EnsureOpenAsync(academyId, end.Year, end.Month, "registrar la planilla");
        if (await _db.StaffPayments.AnyAsync(p => p.AcademyId == academyId && p.StaffId == staff.Id && p.PeriodStart <= end && p.PeriodEnd >= start))
            throw new Exception("Ya hay una liquidación de esta persona que se cruza con ese periodo.");

        var p = new StaffPayment
        {
            Id = Guid.NewGuid(), AcademyId = academyId, StaffId = staff.Id, PeriodStart = start, PeriodEnd = end,
            PeriodMonth = end.Month, PeriodYear = end.Year, Status = StaffPaymentStatus.Draft,
            Notes = Trim(input.Notes, 500), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.StaffPayments.Add(p);
        await ApplyCalculationAsync(p, staff, ManualLines(input.Adjustments));
        _audit.Add(academyId, "payroll.created", nameof(StaffPayment), p.Id, new { p.StaffId, start, end, p.TotalPaid });
        await _db.SaveChangesAsync();
        return await GetAsync(academyId, p.Id);
    }

    public async Task<PaymentDto> UpdateDraftAsync(Guid academyId, Guid id, PaymentInput input)
    {
        var p = await LoadAsync(academyId, id);
        if (p.Status != StaffPaymentStatus.Draft) throw new Exception("Solo se modifica un borrador. Vuelve la liquidación a borrador primero.");
        var staff = await StaffAsync(academyId, p.StaffId);
        p.Notes = Trim(input.Notes, 500);
        await ApplyCalculationAsync(p, staff, ManualLines(input.Adjustments));
        _audit.Add(academyId, "payroll.updated", nameof(StaffPayment), p.Id, new { p.TotalPaid });
        await _db.SaveChangesAsync();
        return await GetAsync(academyId, p.Id);
    }

    /// <summary>Aprobar recalcula con la asistencia del momento y fija los montos.</summary>
    public async Task<PaymentDto> ApproveAsync(Guid academyId, Guid id, string actorName)
    {
        var p = await LoadAsync(academyId, id);
        if (p.Status != StaffPaymentStatus.Draft) throw new Exception("Solo se aprueba un borrador.");
        var staff = await StaffAsync(academyId, p.StaffId);
        var manual = p.Lines.Where(l => l.Kind != StaffPaymentLineKind.Advance)
            .Select(l => new StaffPaymentLine { Kind = l.Kind, Concept = l.Concept, Amount = l.Amount }).ToList();
        await ApplyCalculationAsync(p, staff, manual);
        p.Status = StaffPaymentStatus.Pending;
        p.ApprovedAt = DateTime.UtcNow;
        p.ApprovedByName = actorName;
        _audit.Add(academyId, "payroll.approved", nameof(StaffPayment), p.Id, new { p.TotalPaid });
        await _db.SaveChangesAsync();
        return await GetAsync(academyId, p.Id);
    }

    public async Task<PaymentDto> ReopenAsync(Guid academyId, Guid id)
    {
        var p = await LoadAsync(academyId, id);
        if (p.Status != StaffPaymentStatus.Pending) throw new Exception("Solo una liquidación aprobada vuelve a borrador.");
        p.Status = StaffPaymentStatus.Draft;
        p.ApprovedAt = null;
        p.ApprovedByName = null;
        _audit.Add(academyId, "payroll.reopened", nameof(StaffPayment), p.Id);
        await _db.SaveChangesAsync();
        return await GetAsync(academyId, p.Id);
    }

    /// <summary>Pagar: marca sesiones y adelantos como pagados para que no se vuelvan a contar.</summary>
    public async Task<PaymentDto> PayAsync(Guid academyId, Guid id, string method, DateTime? paidOn)
    {
        var p = await LoadAsync(academyId, id);
        if (p.Status == StaffPaymentStatus.Draft) throw new Exception("Aprueba la liquidación antes de pagarla.");
        if (p.Status == StaffPaymentStatus.Paid) throw new Exception("Esta liquidación ya fue pagada.");
        if (!MethodsAllowed.Contains(method)) throw new Exception("Medio de pago no válido.");
        await _periods.EnsureOpenAsync(academyId, p.PeriodEnd.Year, p.PeriodEnd.Month, "pagar la planilla");

        var advanceIds = p.Lines.Where(l => l.StaffAdvanceId != null).Select(l => l.StaffAdvanceId!.Value).ToList();
        var advances = await _db.StaffAdvances.Where(a => advanceIds.Contains(a.Id)).ToListAsync();
        if (advances.Count != advanceIds.Count || advances.Any(a => a.StaffPaymentId != null && a.StaffPaymentId != p.Id))
            throw new Exception("Un adelanto cambió o ya se descontó en otra liquidación: vuelve a borrador y recalcula.");

        var eventIds = (p.Detail != null ? JsonSerializer.Deserialize<List<DayLine>>(p.Detail, Json) ?? new() : new())
            .SelectMany(d => d.Items).Select(i => i.EventId).ToList();
        var marks = await _db.StaffAttendances.Where(a => a.AcademyId == academyId && a.EventId != null && eventIds.Contains(a.EventId.Value)).ToListAsync();
        if (marks.Any(m => m.StaffPaymentId != null && m.StaffPaymentId != p.Id))
            throw new Exception("Alguna sesión ya se pagó en otra liquidación: vuelve a borrador y recalcula.");

        marks.ForEach(m => m.StaffPaymentId = p.Id);
        advances.ForEach(a => a.StaffPaymentId = p.Id);
        p.Status = StaffPaymentStatus.Paid;
        p.PaymentMethod = method;
        p.PaidAt = paidOn.HasValue ? DateTime.SpecifyKind(paidOn.Value.Date.AddHours(17), DateTimeKind.Utc) : DateTime.UtcNow;
        p.UpdatedAt = DateTime.UtcNow;
        _audit.Add(academyId, "finances.staffpayment.paid", nameof(StaffPayment), p.Id, new { p.StaffId, p.PeriodStart, p.PeriodEnd, p.TotalPaid, method });
        await _db.SaveChangesAsync();
        return await GetAsync(academyId, p.Id);
    }

    public async Task DeleteAsync(Guid academyId, Guid id)
    {
        var p = await LoadAsync(academyId, id);
        if (p.Status == StaffPaymentStatus.Paid) throw new Exception("Una liquidación pagada no se elimina.");
        p.IsDeleted = true;
        p.DeletedAt = DateTime.UtcNow;
        _audit.Add(academyId, "payroll.deleted", nameof(StaffPayment), p.Id, new { p.StaffId, p.PeriodStart, p.PeriodEnd, p.TotalPaid });
        await _db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Adelantos
    // ─────────────────────────────────────────────────────────────
    public sealed class AdvanceInput
    {
        public Guid StaffId { get; set; }
        public DateTime? Date { get; set; }
        public decimal Amount { get; set; }
        public string Concept { get; set; } = string.Empty;
        public string? Method { get; set; }
    }

    public sealed record AdvanceDto(Guid Id, Guid StaffId, string StaffName, DateTime Date, decimal Amount, string Concept, string? Method, bool Discounted, string? CreatedByName);

    public async Task<List<AdvanceDto>> AdvancesAsync(Guid academyId, Guid? staffId, bool onlyPending)
    {
        var q = _db.StaffAdvances.AsNoTracking().Where(a => a.AcademyId == academyId);
        if (staffId.HasValue) q = q.Where(a => a.StaffId == staffId);
        if (onlyPending) q = q.Where(a => a.StaffPaymentId == null);
        var list = await q.OrderByDescending(a => a.Date).Take(200).ToListAsync();
        var names = await _db.Users.AsNoTracking().Where(u => list.Select(a => a.StaffId).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName);
        return list.Select(a => new AdvanceDto(a.Id, a.StaffId, names.GetValueOrDefault(a.StaffId, ""), a.Date, a.Amount, a.Concept, a.Method, a.StaffPaymentId != null, a.CreatedByName)).ToList();
    }

    public async Task<AdvanceDto> AddAdvanceAsync(Guid academyId, AdvanceInput input, string actorName)
    {
        var staff = await StaffAsync(academyId, input.StaffId);
        var concept = (input.Concept ?? string.Empty).Trim();
        if (concept.Length == 0 || concept.Length > 150) throw new Exception("Indica el concepto del adelanto.");
        if (input.Amount <= 0 || input.Amount > 100000) throw new Exception("El monto del adelanto debe ser mayor a cero.");
        var date = (input.Date ?? AcademyClock.Today).Date;
        if (date > AcademyClock.Today) throw new Exception("La fecha del adelanto no puede ser futura.");
        if (input.Method != null && !MethodsAllowed.Contains(input.Method)) throw new Exception("Medio de pago no válido.");
        await _periods.EnsureOpenAsync(academyId, date.Year, date.Month, "registrar el adelanto");
        var a = new StaffAdvance
        {
            AcademyId = academyId, StaffId = staff.Id, Date = date, Amount = Math.Round(input.Amount, 2),
            Concept = concept, Method = input.Method, CreatedByName = actorName
        };
        _db.StaffAdvances.Add(a);
        _audit.Add(academyId, "payroll.advance.created", nameof(StaffAdvance), a.Id, new { a.StaffId, a.Date, a.Amount, a.Concept });
        await _db.SaveChangesAsync();
        return new AdvanceDto(a.Id, a.StaffId, $"{staff.FirstName} {staff.LastName}", a.Date, a.Amount, a.Concept, a.Method, false, a.CreatedByName);
    }

    public async Task DeleteAdvanceAsync(Guid academyId, Guid id)
    {
        var a = await _db.StaffAdvances.FirstOrDefaultAsync(x => x.Id == id && x.AcademyId == academyId)
            ?? throw new KeyNotFoundException("Adelanto no encontrado.");
        if (a.StaffPaymentId != null) throw new Exception("El adelanto ya se descontó en una liquidación pagada.");
        if (await _db.StaffPaymentLines.AnyAsync(l => l.StaffAdvanceId == id && _db.StaffPayments.Any(p => p.Id == l.StaffPaymentId)))
            throw new Exception("El adelanto está en una liquidación sin pagar: elimínala o recalcúlala primero.");
        a.IsDeleted = true;
        a.DeletedAt = DateTime.UtcNow;
        _audit.Add(academyId, "payroll.advance.deleted", nameof(StaffAdvance), id, new { a.StaffId, a.Amount });
        await _db.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Para el propio colaborador (app)
    // ─────────────────────────────────────────────────────────────
    public sealed record MyPayroll(string Scheme, string Frequency, Calculation? Current, List<PaymentDto> Payments, List<AdvanceDto> PendingAdvances);

    public async Task<MyPayroll> MineAsync(Guid academyId, Guid userId)
    {
        var staff = await StaffAsync(academyId, userId);
        var period = PeriodContaining(staff.PayFrequency, AcademyClock.Today);
        var current = await CalculateAsync(academyId, userId, period.Start, period.End);
        var payments = await ListAsync(academyId, AcademyClock.Today.AddMonths(-6), AcademyClock.Today.AddMonths(1), userId, includeDrafts: false);
        var advances = await AdvancesAsync(academyId, userId, onlyPending: true);
        return new MyPayroll(SchemeLabel(staff.PaymentType), FrequencyLabel(staff.PayFrequency), current, payments, advances);
    }

    private static string? Trim(string? value, int max)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v.Length > max ? v[..max] : v;
    }
}
