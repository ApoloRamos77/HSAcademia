using HSAcademia.Application.DTOs.Calendar;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Cobros de torneo a las familias y gastos del torneo.
/// Cada concepto se sincroniza de forma idempotente con el plantel, las convocatorias y la asistencia:
///  - Una sola vez: un cargo por jugador del plantel (activo).
///  - Por partido: se cobra a quien quedó Presente o Tarde; quien confirmó y no asistió queda "Por revisar"
///    hasta que el administrador decide. Solo partidos ya empezados, con lista tomada y no postergados/suspendidos.
///  - Costo total: se reparte entre quienes pagan (al céntimo, hacia arriba). Las deudas sin pagos se recalculan.
/// Lo que ya tiene pagos nunca se anula ni se recalcula.
/// </summary>
public class TournamentBillingService
{
    private readonly AppDbContext _db;
    private readonly FinancialPeriodGuard _periods;
    private readonly AuditService? _audit;
    private readonly ILogger<TournamentBillingService>? _logger;

    public TournamentBillingService(AppDbContext db, FinancialPeriodGuard periods, AuditService? audit = null, ILogger<TournamentBillingService>? logger = null)
    {
        _db = db;
        _periods = periods;
        _audit = audit;
        _logger = logger;
    }

    private static decimal Ceil2(decimal v) => Math.Ceiling(v * 100m) / 100m;
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static bool Paid(PaymentRecord? r) => r != null && (r.AmountPaid > 0.001m || r.IsPaid);
    /// <summary>Nota de los cargos cuya deuda se anuló en Cobranzas: nunca se vuelven a cobrar solos.</summary>
    private const string AnnulledNote = "Deuda anulada en Cobranzas";

    // =========================================================
    // CONCEPTOS
    // =========================================================
    public async Task<TournamentFeeDto> CreateFeeAsync(Guid academyId, Guid tournamentId, SaveTournamentFeeDto dto)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.AcademyId == academyId && x.Id == tournamentId)
                ?? throw new KeyNotFoundException("Torneo no encontrado.");
        await ValidateFeeAsync(academyId, tournamentId, dto);
        var fee = new TournamentFee { AcademyId = academyId, TournamentId = t.Id };
        Apply(fee, dto);
        _db.TournamentFees.Add(fee);
        _audit?.Add(academyId, "tournaments.fee.create", nameof(TournamentFee), fee.Id,
            new { tournament = t.Name, fee.Name, fee.TeamId, fee.Frequency, fee.AmountMode, fee.Amount, fee.ExemptScholarship });
        await _db.SaveChangesAsync();
        await SyncFeeAsync(fee.Id);
        return (await FeeStatsAsync(academyId, tournamentId, fee.Id)).Single();
    }

    public async Task<TournamentFeeDto> UpdateFeeAsync(Guid academyId, Guid feeId, SaveTournamentFeeDto dto)
    {
        var fee = await _db.TournamentFees.FirstOrDefaultAsync(x => x.AcademyId == academyId && x.Id == feeId)
                  ?? throw new KeyNotFoundException("Concepto no encontrado.");
        await ValidateFeeAsync(academyId, fee.TournamentId, dto);
        if ((int)fee.Frequency != dto.Frequency && await _db.TournamentCharges.AnyAsync(c => c.FeeId == feeId))
            throw new InvalidOperationException("No se puede cambiar la frecuencia de un concepto que ya generó cobros. Crea otro concepto.");
        var before = new { fee.Name, fee.TeamId, fee.AmountMode, fee.Amount, fee.IsActive, fee.ExemptScholarship };
        Apply(fee, dto);
        fee.UpdatedAt = DateTime.UtcNow;
        _audit?.Add(academyId, "tournaments.fee.update", nameof(TournamentFee), fee.Id,
            new { before, after = new { fee.Name, fee.TeamId, fee.AmountMode, fee.Amount, fee.IsActive, fee.ExemptScholarship } });
        await _db.SaveChangesAsync();
        await SyncFeeAsync(fee.Id);
        return (await FeeStatsAsync(academyId, fee.TournamentId, fee.Id)).Single();
    }

    /// <summary>Elimina el concepto y anula sus deudas sin pagos. Si alguna tiene pagos, hay que desactivarlo.</summary>
    public async Task DeleteFeeAsync(Guid academyId, Guid feeId)
    {
        var fee = await _db.TournamentFees.FirstOrDefaultAsync(x => x.AcademyId == academyId && x.Id == feeId)
                  ?? throw new KeyNotFoundException("Concepto no encontrado.");
        var charges = await _db.TournamentCharges.Include(c => c.PaymentRecord).Where(c => c.FeeId == feeId).ToListAsync();
        if (charges.Any(c => Paid(c.PaymentRecord)))
            throw new InvalidOperationException("Este concepto ya tiene pagos: desactívalo en lugar de eliminarlo (se anulan solo las deudas sin pagos).");
        foreach (var c in charges) VoidRecord(c);
        fee.IsDeleted = true;
        fee.DeletedAt = DateTime.UtcNow;
        _audit?.Add(academyId, "tournaments.fee.delete", nameof(TournamentFee), fee.Id, new { fee.Name, voided = charges.Count(c => c.Status == TournamentChargeStatus.Charged) });
        await _db.SaveChangesAsync();
    }

    private async Task ValidateFeeAsync(Guid academyId, Guid tournamentId, SaveTournamentFeeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new InvalidOperationException("Ponle un nombre al concepto (ej. Arbitraje).");
        if (dto.Amount <= 0) throw new InvalidOperationException("El monto debe ser mayor a cero.");
        if (dto.TeamId.HasValue && !await _db.TournamentTeams.AnyAsync(x => x.Id == dto.TeamId && x.TournamentId == tournamentId && x.AcademyId == academyId))
            throw new InvalidOperationException("El equipo no pertenece a este torneo.");
    }

    private static void Apply(TournamentFee fee, SaveTournamentFeeDto dto)
    {
        fee.Name = dto.Name.Trim();
        fee.TeamId = dto.TeamId;
        fee.Frequency = (TournamentFeeFrequency)dto.Frequency;
        fee.AmountMode = (TournamentFeeAmountMode)dto.AmountMode;
        fee.Amount = Math.Round(dto.Amount, 2);
        fee.DueDate = dto.DueDate.HasValue ? DateTime.SpecifyKind(dto.DueDate.Value.Date, DateTimeKind.Utc) : null;
        fee.ExemptScholarship = dto.ExemptScholarship;
        fee.IsActive = dto.IsActive;
        fee.Notes = Clean(dto.Notes);
    }

    // =========================================================
    // DECISIONES
    // =========================================================
    public async Task<TournamentChargeDto> DecideAsync(Guid academyId, Guid chargeId, DecideTournamentChargeDto dto, string? actorName)
    {
        var charge = await _db.TournamentCharges.Include(c => c.Fee)
                         .FirstOrDefaultAsync(c => c.AcademyId == academyId && c.Id == chargeId)
                     ?? throw new KeyNotFoundException("Cobro no encontrado.");
        if (charge.Status is not (TournamentChargeStatus.Review or TournamentChargeStatus.Waived))
            throw new InvalidOperationException("Solo se decide sobre quienes confirmaron y no asistieron.");
        charge.Status = dto.Charge ? TournamentChargeStatus.Charged : TournamentChargeStatus.Waived;
        charge.DecidedAt = DateTime.UtcNow;
        charge.DecidedByName = actorName;
        charge.Note = Clean(dto.Note);
        charge.UpdatedAt = DateTime.UtcNow;
        _audit?.Add(academyId, "tournaments.charge.decide", nameof(TournamentCharge), charge.Id,
            new { fee = charge.Fee.Name, charge.StudentId, charge.EventId, charge = dto.Charge, charge.Note });
        await _db.SaveChangesAsync();
        await SyncFeeAsync(charge.FeeId);
        return (await ChargesAsync(academyId, charge.FeeId, chargeId)).Single();
    }

    // =========================================================
    // SINCRONIZACIÓN
    // =========================================================
    /// <summary>Respaldo horario: todos los conceptos de torneos no eliminados.</summary>
    public async Task<int> SyncAllAsync(CancellationToken ct = default)
    {
        var ids = await _db.TournamentFees.AsNoTracking().Select(f => f.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) break;
            await TrySyncFeeAsync(id);
        }
        return ids.Count;
    }

    public async Task<TournamentSyncResultDto> SyncTournamentAsync(Guid academyId, Guid tournamentId)
    {
        var ids = await _db.TournamentFees.AsNoTracking().Where(f => f.AcademyId == academyId && f.TournamentId == tournamentId).Select(f => f.Id).ToListAsync();
        var total = new TournamentSyncResultDto();
        foreach (var id in ids)
        {
            var r = await SyncFeeAsync(id);
            total.Created += r.Created; total.Updated += r.Updated; total.Voided += r.Voided; total.Review += r.Review;
            total.Warnings.AddRange(r.Warnings);
        }
        return total;
    }

    /// <summary>Tras tomar lista o cambiar un partido de equipo: no interrumpe la operación si falla.</summary>
    public async Task TrySyncEventAsync(Guid academyId, Guid eventId)
    {
        try
        {
            var ev = await _db.Events.IgnoreQueryFilters().AsNoTracking()
                .Where(e => e.Id == eventId && e.AcademyId == academyId)
                .Select(e => new { e.TournamentId, e.TournamentTeamId }).FirstOrDefaultAsync();
            if (ev?.TournamentId == null) return;
            var ids = await _db.TournamentFees.AsNoTracking()
                .Where(f => f.TournamentId == ev.TournamentId && f.Frequency == TournamentFeeFrequency.PerMatch &&
                            (f.TeamId == null || f.TeamId == ev.TournamentTeamId))
                .Select(f => f.Id).ToListAsync();
            foreach (var id in ids) await TrySyncFeeAsync(id);
        }
        catch (Exception ex) { _logger?.LogError(ex, "No se pudieron sincronizar los cobros del partido {EventId}", eventId); }
    }

    /// <summary>Tras cambiar el plantel, retirar un equipo o borrar el torneo.</summary>
    public async Task TrySyncTournamentAsync(Guid academyId, Guid tournamentId)
    {
        try
        {
            var ids = await _db.TournamentFees.IgnoreQueryFilters().AsNoTracking()
                .Where(f => f.AcademyId == academyId && f.TournamentId == tournamentId && !f.IsDeleted).Select(f => f.Id).ToListAsync();
            foreach (var id in ids) await TrySyncFeeAsync(id);
        }
        catch (Exception ex) { _logger?.LogError(ex, "No se pudieron sincronizar los cobros del torneo {TournamentId}", tournamentId); }
    }

    private async Task TrySyncFeeAsync(Guid feeId)
    {
        try { await SyncFeeAsync(feeId); }
        catch (Exception ex)
        {
            _db.ChangeTracker.Clear();
            _logger?.LogError(ex, "No se pudo sincronizar el concepto de torneo {FeeId}", feeId);
        }
    }

    private enum Want { Charge, Review }
    private sealed record Desired(Want Want, bool Exempt);

    public async Task<TournamentSyncResultDto> SyncFeeAsync(Guid feeId)
    {
        var result = new TournamentSyncResultDto();
        var fee = await _db.TournamentFees.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.Id == feeId);
        if (fee == null) return result;
        var tournament = await _db.Tournaments.IgnoreQueryFilters().AsNoTracking().FirstAsync(t => t.Id == fee.TournamentId);

        var teams = await _db.TournamentTeams.AsNoTracking()
            .Where(x => x.TournamentId == fee.TournamentId && (fee.TeamId == null || x.Id == fee.TeamId))
            .ToListAsync();
        var teamIds = teams.Select(x => x.Id).ToList();
        var active = fee.IsActive && !fee.IsDeleted && !tournament.IsDeleted && tournament.Status != TournamentStatus.Cancelled;

        // ── Lo que corresponde hoy ──
        var desired = new Dictionary<(Guid? Ev, Guid St), Desired>();
        var matchInfo = new Dictionary<Guid, Event>();
        if (active && teamIds.Count > 0)
        {
            if (fee.Frequency == TournamentFeeFrequency.Once)
            {
                var roster = await _db.TournamentPlayers.AsNoTracking()
                    .Where(p => teamIds.Contains(p.TeamId) && p.Student.IsActive)
                    .Select(p => new { p.StudentId, p.Student.IsScholarship, p.Student.IsGuest })
                    .ToListAsync();
                foreach (var p in roster.DistinctBy(p => p.StudentId))
                    desired[(null, p.StudentId)] = new(Want.Charge, fee.ExemptScholarship && (p.IsScholarship || p.IsGuest));
            }
            else
            {
                var now = AcademyClock.WallNow;
                var matches = (await _db.Events.AsNoTracking()
                        .Where(e => e.AcademyId == fee.AcademyId && e.TournamentTeamId != null && teamIds.Contains(e.TournamentTeamId.Value) && e.StartTime <= now)
                        .ToListAsync())
                    .Where(e => e.MatchStatus is not (MatchStatus.Postponed or MatchStatus.Suspended))
                    .ToList();
                var ids = matches.Select(e => e.Id).ToList();
                var marks = await _db.Attendances.AsNoTracking().Where(a => a.EventId != null && ids.Contains(a.EventId.Value))
                    .Select(a => new { EventId = a.EventId!.Value, a.StudentId, a.Status }).ToListAsync();
                var calls = await _db.EventCalls.AsNoTracking().Where(c => ids.Contains(c.EventId))
                    .Select(c => new { c.EventId, c.StudentId, c.IsConfirmed }).ToListAsync();
                var studentIds = marks.Select(m => m.StudentId).Concat(calls.Select(c => c.StudentId)).Distinct().ToList();
                var flags = await _db.Students.AsNoTracking().Where(s => studentIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.IsScholarship || s.IsGuest);

                foreach (var ev in matches)
                {
                    var evMarks = marks.Where(m => m.EventId == ev.Id).ToList();
                    if (evMarks.Count == 0 && !ev.AttendanceClosed) continue; // lista aún no tomada
                    matchInfo[ev.Id] = ev;
                    foreach (var sid in evMarks.Select(m => m.StudentId).Concat(calls.Where(c => c.EventId == ev.Id).Select(c => c.StudentId)).Distinct())
                    {
                        var exempt = fee.ExemptScholarship && flags.GetValueOrDefault(sid);
                        var mark = evMarks.FirstOrDefault(m => m.StudentId == sid);
                        var present = mark != null && mark.Status is AttendanceStatus.Present or AttendanceStatus.Late;
                        var confirmed = calls.Any(c => c.EventId == ev.Id && c.StudentId == sid && c.IsConfirmed == true);
                        if (present) desired[(ev.Id, sid)] = new(Want.Charge, exempt);
                        else if (confirmed && !exempt) desired[(ev.Id, sid)] = new(Want.Review, false);
                    }
                }
            }
        }

        // ── Lo que hay ──
        var existing = await _db.TournamentCharges.Include(c => c.PaymentRecord).Where(c => c.FeeId == fee.Id).ToListAsync();
        var now2 = DateTime.UtcNow;
        foreach (var c in existing)
        {
            // Deuda anulada desde Cobranzas: se respeta como "no cobrar".
            if (c.Status == TournamentChargeStatus.Charged && c.PaymentRecordId != null && c.PaymentRecord == null)
            {
                c.Status = TournamentChargeStatus.Waived;
                c.PaymentRecordId = null;
                c.Note = AnnulledNote;
                c.DecidedAt ??= now2;
                c.UpdatedAt = now2;
            }

            var has = desired.TryGetValue((c.EventId, c.StudentId), out var d);
            desired.Remove((c.EventId, c.StudentId));
            if (!has)
            {
                // Ya no corresponde (salió del plantel, partido borrado o postergado, concepto inactivo…).
                if (c.Status == TournamentChargeStatus.Charged && Paid(c.PaymentRecord))
                {
                    result.Warnings.Add($"{await NameAsync(c.StudentId)} ya pagó \"{fee.Name}\" que ya no le corresponde: revisa si hay que devolver.");
                    continue;
                }
                if (VoidRecord(c)) result.Voided++;
                _db.TournamentCharges.Remove(c);
                continue;
            }

            if (d!.Exempt)
            {
                if (c.Status == TournamentChargeStatus.Charged && Paid(c.PaymentRecord)) continue;
                if (VoidRecord(c)) result.Voided++;
                c.Status = TournamentChargeStatus.Exempt;
                c.UpdatedAt = now2;
            }
            else if (d.Want == Want.Charge)
            {
                if (c.Status == TournamentChargeStatus.Waived && c.Note == AnnulledNote)
                {
                    // Anulada en Cobranzas: se respeta.
                }
                else if (c.Status != TournamentChargeStatus.Charged)
                {
                    c.Status = TournamentChargeStatus.Charged; // asistió: se cobra (aunque antes se hubiera decidido no cobrar la falta)
                    c.DecidedAt = null; c.DecidedByName = null;
                    c.UpdatedAt = now2;
                }
            }
            else // Review: confirmó y no asistió
            {
                if (c.Status == TournamentChargeStatus.Charged && c.DecidedAt == null)
                {
                    if (Paid(c.PaymentRecord)) continue;
                    if (VoidRecord(c)) result.Voided++;
                    c.Status = TournamentChargeStatus.Review;
                    c.UpdatedAt = now2;
                }
                else if (c.Status == TournamentChargeStatus.Exempt)
                {
                    c.Status = TournamentChargeStatus.Review;
                    c.UpdatedAt = now2;
                }
                // Review pendiente o ya decidido (Charged/Waived con decisión): se respeta.
            }
        }
        foreach (var ((ev, st), d) in desired)
        {
            var c = new TournamentCharge
            {
                AcademyId = fee.AcademyId, FeeId = fee.Id, EventId = ev, StudentId = st,
                Status = d.Exempt ? TournamentChargeStatus.Exempt : d.Want == Want.Charge ? TournamentChargeStatus.Charged : TournamentChargeStatus.Review,
            };
            _db.TournamentCharges.Add(c);
            existing.Add(c);
        }

        // ── Montos y deudas ──
        var live = existing.Where(c => _db.Entry(c).State != EntityState.Deleted && _db.Entry(c).State != EntityState.Detached).ToList();
        result.Review = live.Count(c => c.Status == TournamentChargeStatus.Review);
        var newByStudent = new Dictionary<Guid, List<string>>();
        foreach (var group in live.Where(c => c.Status == TournamentChargeStatus.Charged).GroupBy(c => c.EventId))
        {
            var count = group.Count();
            var amount = fee.AmountMode == TournamentFeeAmountMode.PerPlayer ? fee.Amount : Ceil2(fee.Amount / count);
            Event? ev = group.Key.HasValue ? matchInfo.GetValueOrDefault(group.Key.Value) : null;
            if (group.Key.HasValue && ev == null)
                ev = await _db.Events.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(e => e.Id == group.Key.Value);
            var description = Describe(tournament, fee, ev);

            foreach (var c in group)
            {
                if (c.PaymentRecord == null)
                {
                    var due = await DueDateAsync(fee, ev, result);
                    if (due == null) continue;
                    var rec = new PaymentRecord
                    {
                        AcademyId = fee.AcademyId,
                        StudentId = c.StudentId,
                        Description = description,
                        Amount = amount,
                        DueDate = due.Value,
                        PeriodYear = due.Value.Year,
                        PeriodMonth = due.Value.Month,
                        Type = PaymentType.Tournament,
                    };
                    _db.PaymentRecords.Add(rec);
                    c.PaymentRecord = rec;
                    c.PaymentRecordId = rec.Id;
                    c.Amount = amount;
                    c.UpdatedAt = now2;
                    result.Created++;
                    (newByStudent.TryGetValue(c.StudentId, out var l) ? l : newByStudent[c.StudentId] = new()).Add($"{Short(fee, ev)} S/ {amount:0.00}");
                }
                else if (!Paid(c.PaymentRecord) && c.PaymentRecord.ExclusionType == ExclusionType.None &&
                         (c.PaymentRecord.Amount != amount || c.PaymentRecord.Description != description))
                {
                    c.PaymentRecord.Amount = amount;
                    c.PaymentRecord.Description = description;
                    c.PaymentRecord.UpdatedAt = now2;
                    c.Amount = amount;
                    c.UpdatedAt = now2;
                    result.Updated++;
                }
            }
        }

        foreach (var (sid, items) in newByStudent)
        {
            var name = await _db.Students.AsNoTracking().Where(s => s.Id == sid).Select(s => s.FirstName).FirstOrDefaultAsync();
            var body = $"{tournament.Name}: {string.Join(", ", items)}. Puedes pagarlo en la academia o enviar tu comprobante desde la app.";
            _db.FamilyNotices.Add(new FamilyNotice
            {
                AcademyId = fee.AcademyId, StudentId = sid, Type = FamilyNoticeType.PaymentReminder,
                Title = $"Cobro del torneo{(name != null ? $" · {name}" : "")}",
                Body = body.Length <= 500 ? body : body[..500],
            });
        }

        if (result.Created + result.Updated + result.Voided > 0)
            _audit?.Add(fee.AcademyId, "tournaments.fee.sync", nameof(TournamentFee), fee.Id,
                new { fee.Name, result.Created, result.Updated, result.Voided, result.Review });
        await _db.SaveChangesAsync();
        return result;
    }

    private async Task<string> NameAsync(Guid studentId) =>
        await _db.Students.AsNoTracking().Where(s => s.Id == studentId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefaultAsync() ?? "Un alumno";

    /// <summary>Anula la deuda sin pagos del cargo. Devuelve true si anuló una.</summary>
    private bool VoidRecord(TournamentCharge c)
    {
        if (c.PaymentRecord == null || Paid(c.PaymentRecord)) { if (c.PaymentRecord == null) c.PaymentRecordId = null; return false; }
        c.PaymentRecord.IsDeleted = true;
        c.PaymentRecord.DeletedAt = DateTime.UtcNow;
        c.PaymentRecord.UpdatedAt = DateTime.UtcNow;
        c.PaymentRecord = null;
        c.PaymentRecordId = null;
        c.Amount = 0;
        return true;
    }

    /// <summary>Vencimiento: el del concepto o la fecha del partido; si ese mes está cerrado, hoy.</summary>
    private async Task<DateTime?> DueDateAsync(TournamentFee fee, Event? ev, TournamentSyncResultDto result)
    {
        var today = DateTime.SpecifyKind(AcademyClock.Today, DateTimeKind.Utc);
        var due = ev != null ? DateTime.SpecifyKind(ev.StartTime.Date, DateTimeKind.Utc) : fee.DueDate ?? today;
        if (!await _periods.IsClosedAsync(fee.AcademyId, due.Year, due.Month)) return due;
        if (!await _periods.IsClosedAsync(fee.AcademyId, today.Year, today.Month)) return today;
        var w = $"\"{fee.Name}\": el mes está cerrado, no se generaron cobros.";
        if (!result.Warnings.Contains(w)) result.Warnings.Add(w);
        return null;
    }

    private static string Short(TournamentFee fee, Event? ev) =>
        ev == null ? fee.Name : $"{fee.Name} {(string.IsNullOrWhiteSpace(ev.Round) ? ev.StartTime.ToString("dd/MM") : ev.Round)}";

    private static string Describe(Tournament t, TournamentFee fee, Event? ev)
    {
        var d = ev == null
            ? $"{t.Name} · {fee.Name}"
            : $"{t.Name} · {fee.Name} · {(string.IsNullOrWhiteSpace(ev.Round) ? "" : ev.Round + " ")}({ev.StartTime:dd/MM})";
        return d.Length <= 200 ? d : d[..200];
    }

    // =========================================================
    // CONSULTAS
    // =========================================================
    public async Task<TournamentFinanceDto> GetFinanceAsync(Guid academyId, Guid tournamentId)
    {
        if (!await _db.Tournaments.AnyAsync(t => t.AcademyId == academyId && t.Id == tournamentId))
            throw new KeyNotFoundException("Torneo no encontrado.");
        var dto = new TournamentFinanceDto { Fees = await FeeStatsAsync(academyId, tournamentId) };
        dto.Charged = dto.Fees.Sum(f => f.Charged);
        dto.Collected = dto.Fees.Sum(f => f.Collected);
        dto.Pending = dto.Fees.Sum(f => f.Pending);

        var feeIds = dto.Fees.Select(f => f.Id).ToList();
        dto.Reviews = (await ChargesQueryAsync(academyId, c => feeIds.Contains(c.FeeId) && c.Status == TournamentChargeStatus.Review))
            .OrderBy(c => c.MatchDate).ThenBy(c => c.StudentName).ToList();

        var expenses = await _db.Expenses.AsNoTracking().Where(e => e.AcademyId == academyId && e.TournamentId == tournamentId)
            .OrderByDescending(e => e.Date).ToListAsync();
        var evIds = expenses.Where(e => e.TournamentEventId.HasValue).Select(e => e.TournamentEventId!.Value).Distinct().ToList();
        var evs = await _db.Events.IgnoreQueryFilters().AsNoTracking().Where(e => evIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => MatchLabel(e.Round, e.StartTime));
        dto.ExpenseItems = expenses.Select(e => new TournamentExpenseDto
        {
            Id = e.Id, Date = e.Date, Amount = e.Amount, Description = e.Description, Supplier = e.Supplier,
            EventId = e.TournamentEventId, MatchLabel = e.TournamentEventId.HasValue ? evs.GetValueOrDefault(e.TournamentEventId.Value) : null,
        }).ToList();
        dto.Expenses = expenses.Sum(e => e.Amount);
        dto.Result = dto.Collected - dto.Expenses;
        dto.Projected = dto.Charged - dto.Expenses;
        if (dto.Reviews.Count > 0) dto.Warnings.Add($"{dto.Reviews.Count} cobro(s) por revisar: confirmaron y no asistieron.");
        return dto;
    }

    public async Task<List<TournamentChargeDto>> ChargesAsync(Guid academyId, Guid feeId, Guid? onlyId = null) =>
        (await ChargesQueryAsync(academyId, c => c.FeeId == feeId && (onlyId == null || c.Id == onlyId)))
            .OrderBy(c => c.MatchDate).ThenBy(c => c.StudentName).ToList();

    private static string MatchLabel(string? round, DateTime start) =>
        string.IsNullOrWhiteSpace(round) ? start.ToString("dd/MM") : $"{round} · {start:dd/MM}";

    private async Task<List<TournamentChargeDto>> ChargesQueryAsync(Guid academyId, System.Linq.Expressions.Expression<Func<TournamentCharge, bool>> filter)
    {
        var rows = await _db.TournamentCharges.AsNoTracking().Where(c => c.AcademyId == academyId).Where(filter)
            .Select(c => new
            {
                c.Id, c.FeeId, FeeName = c.Fee.Name, c.EventId, c.StudentId,
                StudentName = c.Student.FirstName + " " + c.Student.LastName,
                c.Status, c.Amount, c.DecidedByName, c.Note,
                RecAmount = c.PaymentRecord != null ? c.PaymentRecord.Amount : (decimal?)null,
                RecPaid = c.PaymentRecord != null ? c.PaymentRecord.AmountPaid : 0m,
                RecIsPaid = c.PaymentRecord != null && c.PaymentRecord.IsPaid,
            }).ToListAsync();
        var evIds = rows.Where(r => r.EventId.HasValue).Select(r => r.EventId!.Value).Distinct().ToList();
        var evs = await _db.Events.IgnoreQueryFilters().AsNoTracking().Where(e => evIds.Contains(e.Id))
            .Select(e => new { e.Id, e.Round, e.StartTime }).ToDictionaryAsync(e => e.Id);
        var calls = await _db.EventCalls.AsNoTracking().Where(c => evIds.Contains(c.EventId))
            .Select(c => new { c.EventId, c.StudentId, c.IsConfirmed }).ToListAsync();
        return rows.Select(r =>
        {
            var ev = r.EventId.HasValue ? evs.GetValueOrDefault(r.EventId.Value) : null;
            return new TournamentChargeDto
            {
                Id = r.Id, FeeId = r.FeeId, FeeName = r.FeeName, EventId = r.EventId,
                MatchLabel = ev != null ? MatchLabel(ev.Round, ev.StartTime) : null,
                MatchDate = ev?.StartTime,
                StudentId = r.StudentId, StudentName = r.StudentName.Trim(),
                Status = (int)r.Status,
                Amount = r.RecAmount ?? r.Amount,
                AmountPaid = r.RecPaid, IsPaid = r.RecIsPaid,
                Confirmed = r.EventId.HasValue ? calls.FirstOrDefault(c => c.EventId == r.EventId && c.StudentId == r.StudentId)?.IsConfirmed : null,
                DecidedByName = r.DecidedByName, Note = r.Note,
            };
        }).ToList();
    }

    private async Task<List<TournamentFeeDto>> FeeStatsAsync(Guid academyId, Guid tournamentId, Guid? onlyFeeId = null)
    {
        var fees = await _db.TournamentFees.AsNoTracking()
            .Where(f => f.AcademyId == academyId && f.TournamentId == tournamentId && (onlyFeeId == null || f.Id == onlyFeeId))
            .OrderBy(f => f.CreatedAt)
            .Select(f => new { f, TeamName = f.Team != null ? f.Team.Name : null })
            .ToListAsync();
        var ids = fees.Select(x => x.f.Id).ToList();
        var stats = await _db.TournamentCharges.AsNoTracking().Where(c => ids.Contains(c.FeeId))
            .Select(c => new
            {
                c.FeeId, c.Status,
                Amount = c.PaymentRecord != null ? c.PaymentRecord.Amount : 0m,
                Paid = c.PaymentRecord != null ? c.PaymentRecord.AmountPaid : 0m,
                Excluded = c.PaymentRecord != null && c.PaymentRecord.ExclusionType != ExclusionType.None,
            }).ToListAsync();
        return fees.Select(x =>
        {
            var mine = stats.Where(s => s.FeeId == x.f.Id).ToList();
            var charged = mine.Where(s => s.Status == TournamentChargeStatus.Charged && !s.Excluded).ToList();
            return new TournamentFeeDto
            {
                Id = x.f.Id, TournamentId = x.f.TournamentId, TeamId = x.f.TeamId, TeamName = x.TeamName,
                Name = x.f.Name, Frequency = (int)x.f.Frequency, AmountMode = (int)x.f.AmountMode, Amount = x.f.Amount,
                DueDate = x.f.DueDate, ExemptScholarship = x.f.ExemptScholarship, IsActive = x.f.IsActive, Notes = x.f.Notes,
                Charged = charged.Sum(s => s.Amount),
                Collected = charged.Sum(s => s.Paid),
                Pending = charged.Sum(s => Math.Max(0, s.Amount - s.Paid)),
                ChargedCount = charged.Count,
                ReviewCount = mine.Count(s => s.Status == TournamentChargeStatus.Review),
                WaivedCount = mine.Count(s => s.Status == TournamentChargeStatus.Waived) + mine.Count(s => s.Excluded),
                ExemptCount = mine.Count(s => s.Status == TournamentChargeStatus.Exempt),
            };
        }).ToList();
    }

    // =========================================================
    // GASTOS DEL TORNEO
    // =========================================================
    public async Task<TournamentExpenseDto> CreateExpenseAsync(Guid academyId, Guid tournamentId, Guid userId, SaveTournamentExpenseDto dto)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.AcademyId == academyId && x.Id == tournamentId)
                ?? throw new KeyNotFoundException("Torneo no encontrado.");
        if (dto.Amount <= 0) throw new InvalidOperationException("El monto debe ser mayor a cero.");
        if (string.IsNullOrWhiteSpace(dto.Description)) throw new InvalidOperationException("Describe el gasto.");
        Event? ev = null;
        if (dto.EventId.HasValue)
            ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == dto.EventId && e.TournamentId == tournamentId && e.AcademyId == academyId)
                 ?? throw new InvalidOperationException("El partido no pertenece a este torneo.");
        var date = DateTime.SpecifyKind(dto.Date.Date, DateTimeKind.Utc);
        try { await _periods.EnsureOpenAsync(academyId, date.Year, date.Month, "registrar el gasto"); }
        catch (Exception ex) { throw new InvalidOperationException(ex.Message); }

        var description = $"{t.Name} · {dto.Description.Trim()}";
        var e = new Expense
        {
            Id = Guid.NewGuid(),
            AcademyId = academyId,
            Type = ExpenseType.Tournament,
            Amount = Math.Round(dto.Amount, 2),
            Date = date,
            Description = description.Length <= 500 ? description : description[..500],
            Supplier = Clean(dto.Supplier),
            RegisteredById = userId == Guid.Empty ? null : userId,
            TournamentId = tournamentId,
            TournamentEventId = ev?.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.Expenses.Add(e);
        _audit?.Add(academyId, "tournaments.expense.create", nameof(Expense), e.Id, new { tournament = t.Name, e.Amount, e.Description, e.TournamentEventId });
        await _db.SaveChangesAsync();
        return new TournamentExpenseDto
        {
            Id = e.Id, Date = e.Date, Amount = e.Amount, Description = e.Description, Supplier = e.Supplier,
            EventId = e.TournamentEventId, MatchLabel = ev != null ? MatchLabel(ev.Round, ev.StartTime) : null,
        };
    }

    public async Task DeleteExpenseAsync(Guid academyId, Guid tournamentId, Guid expenseId)
    {
        var e = await _db.Expenses.FirstOrDefaultAsync(x => x.AcademyId == academyId && x.Id == expenseId && x.TournamentId == tournamentId)
                ?? throw new KeyNotFoundException("Gasto no encontrado.");
        try { await _periods.EnsureOpenAsync(academyId, e.Date.Year, e.Date.Month, "eliminar el gasto"); }
        catch (Exception ex) { throw new InvalidOperationException(ex.Message); }
        e.IsDeleted = true;
        e.DeletedAt = DateTime.UtcNow;
        _audit?.Add(academyId, "tournaments.expense.delete", nameof(Expense), e.Id, new { e.Amount, e.Description });
        await _db.SaveChangesAsync();
    }
}
