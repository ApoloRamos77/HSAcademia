using HSAcademia.Application.DTOs.Calendar;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Reglas del torneo sobre los jugadores: suspensiones por tarjetas, autorización del apoderado,
/// documentos y resumen de resultados del equipo.
/// Suspensiones: se calculan en orden cronológico con las tarjetas registradas en cada partido jugado.
/// N amarillas acumuladas = 1 partido; roja = M partidos. Se cumplen en los siguientes partidos del equipo
/// (los postergados o suspendidos no cuentan). Al suspendido no se le convoca a ese partido, salvo que el
/// administrador lo convoque a mano (queda "levantada").
/// </summary>
public class TournamentComplianceService
{
    private readonly AppDbContext _db;
    private readonly AuditService? _audit;
    private readonly ILogger<TournamentComplianceService>? _logger;

    public TournamentComplianceService(AppDbContext db, AuditService? audit = null, ILogger<TournamentComplianceService>? logger = null)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    private static bool Finished(Event e) => e.MatchStatus is MatchStatus.Played or MatchStatus.WalkoverWon or MatchStatus.WalkoverLost;
    private static string Label(Event e) => string.IsNullOrWhiteSpace(e.Round) ? e.StartTime.ToString("dd/MM") : $"{e.Round} ({e.StartTime:dd/MM})";

    // =========================================================
    // SUSPENSIONES
    // =========================================================
    public async Task TrySyncTeamAsync(Guid teamId)
    {
        try { await SyncTeamSuspensionsAsync(teamId); }
        catch (Exception ex)
        {
            _db.ChangeTracker.Clear();
            _logger?.LogError(ex, "No se pudieron calcular las suspensiones del equipo {TeamId}", teamId);
        }
    }

    public async Task TrySyncTournamentAsync(Guid tournamentId)
    {
        var teams = await _db.TournamentTeams.AsNoTracking().Where(t => t.TournamentId == tournamentId).Select(t => t.Id).ToListAsync();
        foreach (var id in teams) await TrySyncTeamAsync(id);
    }

    public async Task TrySyncEventAsync(Guid eventId)
    {
        var teamId = await _db.Events.AsNoTracking().Where(e => e.Id == eventId).Select(e => e.TournamentTeamId).FirstOrDefaultAsync();
        if (teamId.HasValue) await TrySyncTeamAsync(teamId.Value);
    }

    private sealed record Desired(Guid StudentId, Guid EventId, Guid SourceEventId, string Reason);

    public async Task SyncTeamSuspensionsAsync(Guid teamId)
    {
        var team = await _db.TournamentTeams.Include(t => t.Tournament).FirstOrDefaultAsync(t => t.Id == teamId);
        if (team == null) return;
        var t = team.Tournament;
        var desired = new List<Desired>();
        var matches = (await _db.Events.AsNoTracking().Where(e => e.TournamentTeamId == teamId).ToListAsync())
            .Where(e => e.MatchStatus is not (MatchStatus.Postponed or MatchStatus.Suspended))
            .OrderBy(e => e.StartTime).ToList();

        if (t.YellowCardsForSuspension is > 0 || t.RedCardSuspensionMatches is > 0)
        {
            var ids = matches.Select(m => m.Id).ToList();
            var stats = await _db.MatchPlayerStats.AsNoTracking().Where(s => ids.Contains(s.EventId))
                .Select(s => new { s.EventId, s.StudentId, s.YellowCards, s.RedCards }).ToListAsync();
            var withStats = stats.Select(s => s.EventId).ToHashSet();
            var now = AcademyClock.WallNow;
            var yellows = new Dictionary<Guid, int>();
            var pending = new Dictionary<Guid, Queue<(Guid Source, string Reason)>>();

            foreach (var m in matches)
            {
                var played = Finished(m) || (m.StartTime <= now && withStats.Contains(m.Id));
                // El partido cumple (o es el destino de) las suspensiones pendientes.
                foreach (var (sid, q) in pending.Where(p => p.Value.Count > 0).ToList())
                {
                    var (src, reason) = q.Dequeue();
                    desired.Add(new Desired(sid, m.Id, src, reason));
                }
                if (!played) continue;
                foreach (var s in stats.Where(s => s.EventId == m.Id))
                {
                    if (t.YellowCardsForSuspension is > 0 && s.YellowCards > 0 && s.RedCards == 0)
                    {
                        var acc = yellows.GetValueOrDefault(s.StudentId) + s.YellowCards;
                        while (acc >= t.YellowCardsForSuspension.Value)
                        {
                            acc -= t.YellowCardsForSuspension.Value;
                            Enqueue(pending, s.StudentId, m.Id, $"{t.YellowCardsForSuspension} amarillas acumuladas (última en {Label(m)})");
                        }
                        yellows[s.StudentId] = acc;
                    }
                    if (t.RedCardSuspensionMatches is > 0 && s.RedCards > 0)
                        for (var i = 0; i < t.RedCardSuspensionMatches.Value; i++)
                            Enqueue(pending, s.StudentId, m.Id, $"Roja en {Label(m)}");
                }
            }
        }

        var existing = await _db.TournamentSuspensions.Where(x => x.TeamId == teamId).ToListAsync();
        var byKey = existing.ToDictionary(x => (x.EventId, x.StudentId));
        var keep = new HashSet<(Guid, Guid)>();
        var newOnes = new List<TournamentSuspension>();
        foreach (var d in desired)
        {
            keep.Add((d.EventId, d.StudentId));
            if (byKey.TryGetValue((d.EventId, d.StudentId), out var row))
            {
                row.Reason = d.Reason;
                row.SourceEventId = d.SourceEventId;
                continue;
            }
            var s = new TournamentSuspension
            {
                AcademyId = team.AcademyId, TeamId = teamId, StudentId = d.StudentId, EventId = d.EventId,
                SourceEventId = d.SourceEventId, Reason = d.Reason,
            };
            _db.TournamentSuspensions.Add(s);
            newOnes.Add(s);
        }
        var lifted = existing.Where(x => !keep.Contains((x.EventId, x.StudentId))).ToList();
        _db.TournamentSuspensions.RemoveRange(lifted);

        // No se convoca al suspendido a un partido por jugar (salvo que el administrador lo haya levantado).
        var nowWall = AcademyClock.WallNow;
        var upcoming = matches.Where(m => m.StartTime >= nowWall && !Finished(m)).ToDictionary(m => m.Id);
        var active = existing.Where(x => keep.Contains((x.EventId, x.StudentId))).Concat(newOnes)
            .Where(x => !x.Overridden && upcoming.ContainsKey(x.EventId)).ToList();
        if (active.Count > 0)
        {
            var evIds = active.Select(a => a.EventId).Distinct().ToList();
            var calls = await _db.EventCalls.Where(c => evIds.Contains(c.EventId)).ToListAsync();
            foreach (var a in active)
            {
                var call = calls.FirstOrDefault(c => c.EventId == a.EventId && c.StudentId == a.StudentId);
                if (call != null) _db.EventCalls.Remove(call);
            }
        }
        // Suspensión que ya no corresponde (se corrigieron tarjetas): vuelve a quedar convocado si sigue en el plantel.
        var toRecall = lifted.Where(x => !x.Overridden && upcoming.ContainsKey(x.EventId)).ToList();
        if (toRecall.Count > 0)
        {
            var roster = (await _db.TournamentPlayers.AsNoTracking().Where(p => p.TeamId == teamId && p.Student.IsActive).Select(p => p.StudentId).ToListAsync()).ToHashSet();
            var evIds = toRecall.Select(x => x.EventId).Distinct().ToList();
            var called = (await _db.EventCalls.AsNoTracking().Where(c => evIds.Contains(c.EventId)).Select(c => new { c.EventId, c.StudentId }).ToListAsync())
                .Select(c => (c.EventId, c.StudentId)).ToHashSet();
            foreach (var x in toRecall.Where(x => roster.Contains(x.StudentId) && !called.Contains((x.EventId, x.StudentId))))
                _db.EventCalls.Add(new EventCall { AcademyId = team.AcademyId, EventId = x.EventId, StudentId = x.StudentId, IsConfirmed = null });
        }
        foreach (var s in newOnes.Where(n => upcoming.ContainsKey(n.EventId)))
        {
            var name = await _db.Students.AsNoTracking().Where(x => x.Id == s.StudentId).Select(x => x.FirstName).FirstOrDefaultAsync() ?? "El alumno";
            var body = $"{name} no juega {Label(upcoming[s.EventId])} de {t.Name}: {s.Reason}.";
            _db.FamilyNotices.Add(new FamilyNotice
            {
                AcademyId = team.AcademyId, StudentId = s.StudentId, EventId = s.EventId, Type = FamilyNoticeType.EventCall,
                Title = $"Suspensión de {name}", Body = body.Length <= 500 ? body : body[..500],
            });
        }
        if (newOnes.Count > 0)
            _audit?.Add(team.AcademyId, "tournaments.suspensions.sync", nameof(TournamentTeam), teamId,
                new { team.Name, added = newOnes.Select(n => new { n.StudentId, n.EventId, n.Reason }) });
        await _db.SaveChangesAsync();
    }

    private static void Enqueue(Dictionary<Guid, Queue<(Guid, string)>> pending, Guid studentId, Guid source, string reason)
    {
        if (!pending.TryGetValue(studentId, out var q)) pending[studentId] = q = new();
        q.Enqueue((source, reason));
    }

    /// <summary>Convocado a mano estando suspendido: la suspensión queda levantada.</summary>
    public async Task OverrideIfSuspendedAsync(Guid eventId, Guid studentId)
    {
        var s = await _db.TournamentSuspensions.FirstOrDefaultAsync(x => x.EventId == eventId && x.StudentId == studentId);
        if (s == null || s.Overridden) return;
        s.Overridden = true;
        _audit?.Add(s.AcademyId, "tournaments.suspension.override", nameof(TournamentSuspension), s.Id, new { s.StudentId, s.EventId, s.Reason });
        await _db.SaveChangesAsync();
    }

    public async Task<List<TournamentSuspensionDto>> MatchSuspensionsAsync(Guid academyId, Guid eventId) =>
        await _db.TournamentSuspensions.AsNoTracking().Where(x => x.AcademyId == academyId && x.EventId == eventId)
            .Select(x => new TournamentSuspensionDto
            {
                StudentId = x.StudentId, StudentName = x.Student.FirstName + " " + x.Student.LastName,
                EventId = x.EventId, Reason = x.Reason, Overridden = x.Overridden,
            }).ToListAsync();

    /// <summary>Próximo partido por jugar en que cada jugador del equipo está suspendido (no levantado).</summary>
    public async Task<Dictionary<Guid, string>> NextSuspensionsAsync(Guid teamId)
    {
        var now = AcademyClock.WallNow;
        var rows = await _db.TournamentSuspensions.AsNoTracking()
            .Where(x => x.TeamId == teamId && !x.Overridden)
            .Join(_db.Events.AsNoTracking(), x => x.EventId, e => e.Id, (x, e) => new { x.StudentId, x.Reason, e.Round, e.StartTime, e.MatchStatus })
            .Where(x => x.StartTime >= now)
            .ToListAsync();
        return rows.Where(r => r.MatchStatus is not (MatchStatus.Played or MatchStatus.WalkoverWon or MatchStatus.WalkoverLost))
            .GroupBy(r => r.StudentId)
            .ToDictionary(g => g.Key, g =>
            {
                var r = g.OrderBy(x => x.StartTime).First();
                return $"{(string.IsNullOrWhiteSpace(r.Round) ? r.StartTime.ToString("dd/MM") : r.Round)}: {r.Reason}";
            });
    }

    // =========================================================
    // AUTORIZACIONES
    // =========================================================
    public async Task<Dictionary<Guid, TournamentAuthorization>> AuthorizationsAsync(Guid tournamentId) =>
        await _db.TournamentAuthorizations.AsNoTracking().Where(a => a.TournamentId == tournamentId).ToDictionaryAsync(a => a.StudentId);

    public async Task AuthorizeAsync(Guid academyId, Guid tournamentId, Guid studentId, string method, Guid? userId, string? userName, string? note)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.Id == tournamentId && x.AcademyId == academyId)
                ?? throw new KeyNotFoundException("Torneo no encontrado.");
        if (!t.RequiresAuthorization) throw new InvalidOperationException("Este torneo no pide autorización.");
        var inRoster = await _db.TournamentPlayers.AnyAsync(p => p.StudentId == studentId && p.Team.TournamentId == tournamentId);
        if (!inRoster) throw new InvalidOperationException("El alumno no está en el plantel de este torneo.");
        var row = await _db.TournamentAuthorizations.FirstOrDefaultAsync(a => a.TournamentId == tournamentId && a.StudentId == studentId);
        if (row != null) return; // ya autorizado
        _db.TournamentAuthorizations.Add(new TournamentAuthorization
        {
            AcademyId = academyId, TournamentId = tournamentId, StudentId = studentId, Method = method,
            AcceptedByUserId = userId, AcceptedByName = userName, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            TextSnapshot = t.AuthorizationText,
        });
        _audit?.Add(academyId, "tournaments.authorization.accept", nameof(Tournament), tournamentId, new { studentId, method });
        await _db.SaveChangesAsync();
    }

    public async Task RevokeAsync(Guid academyId, Guid tournamentId, Guid studentId)
    {
        var row = await _db.TournamentAuthorizations.FirstOrDefaultAsync(a => a.AcademyId == academyId && a.TournamentId == tournamentId && a.StudentId == studentId)
                  ?? throw new KeyNotFoundException("No hay autorización registrada.");
        _db.TournamentAuthorizations.Remove(row);
        _audit?.Add(academyId, "tournaments.authorization.revoke", nameof(Tournament), tournamentId, new { studentId, row.Method });
        await _db.SaveChangesAsync();
    }

    /// <summary>Avisa a la familia que debe autorizar (una vez por alumno y torneo).</summary>
    public async Task AddAuthorizationNoticesAsync(Tournament t, IEnumerable<(Guid StudentId, string FirstName)> students)
    {
        if (!t.RequiresAuthorization) return;
        var list = students.ToList();
        var keys = list.Select(x => $"tauth:{t.Id}:{x.StudentId}").ToList();
        var sent = (await _db.FamilyNotices.AsNoTracking().Where(n => n.DedupeKey != null && keys.Contains(n.DedupeKey)).Select(n => n.DedupeKey!).ToListAsync()).ToHashSet();
        var authorized = (await _db.TournamentAuthorizations.AsNoTracking().Where(x => x.TournamentId == t.Id).Select(x => x.StudentId).ToListAsync()).ToHashSet();
        foreach (var (sid, name) in list.Where(x => !sent.Contains($"tauth:{t.Id}:{x.StudentId}") && !authorized.Contains(x.StudentId)))
            _db.FamilyNotices.Add(new FamilyNotice
            {
                AcademyId = t.AcademyId, StudentId = sid, Type = FamilyNoticeType.EventCall,
                DedupeKey = $"tauth:{t.Id}:{sid}",
                Title = $"Autoriza a {name} para {t.Name}",
                Body = "La academia pide tu autorización para que participe en el torneo. Acéptala en la app, en Agenda.",
            });
    }

    // =========================================================
    // RESULTADOS
    // =========================================================
    public static TeamRecordDto Record(IEnumerable<Event> matches)
    {
        var r = new TeamRecordDto();
        foreach (var m in matches)
        {
            int? gf = m.GoalsFor, ga = m.GoalsAgainst;
            if (m.MatchStatus == MatchStatus.WalkoverWon) { gf ??= 3; ga ??= 0; }
            else if (m.MatchStatus == MatchStatus.WalkoverLost) { gf ??= 0; ga ??= 3; }
            else if (m.MatchStatus is MatchStatus.Postponed or MatchStatus.Suspended || gf == null || ga == null) continue;
            r.Played++;
            r.GoalsFor += gf!.Value;
            r.GoalsAgainst += ga!.Value;
            if (gf > ga) r.Won++; else if (gf == ga) r.Drawn++; else r.Lost++;
        }
        r.Points = r.Won * 3 + r.Drawn;
        return r;
    }

    // =========================================================
    // APP DE LA FAMILIA
    // =========================================================
    public async Task<List<MyTournamentDto>> MyTournamentsAsync(Guid academyId, Guid userId, Guid? studentId)
    {
        var mine = _db.Students.Where(s => s.AcademyId == academyId && (s.GuardianId == userId || s.UserId == userId));
        if (studentId.HasValue) mine = mine.Where(s => s.Id == studentId.Value);
        var players = await _db.TournamentPlayers.AsNoTracking()
            .Where(p => mine.Select(s => s.Id).Contains(p.StudentId) && p.Team.Tournament.Status != TournamentStatus.Cancelled)
            .Select(p => new
            {
                p.StudentId, StudentName = p.Student.FirstName, p.TeamId, TeamName = p.Team.Name, p.DeliveredDocuments, p.ShirtNumber,
                Tournament = p.Team.Tournament,
            }).ToListAsync();
        var cutoff = DateTime.SpecifyKind(AcademyClock.Today.AddDays(-30), DateTimeKind.Utc);
        players = players.Where(p => p.Tournament.Status != TournamentStatus.Finished || (p.Tournament.EndDate ?? cutoff) >= cutoff).ToList();

        var teamIds = players.Select(p => p.TeamId).Distinct().ToList();
        var matches = await _db.Events.AsNoTracking().Where(e => e.TournamentTeamId != null && teamIds.Contains(e.TournamentTeamId.Value)).ToListAsync();
        var tIds = players.Select(p => p.Tournament.Id).Distinct().ToList();
        var auths = await _db.TournamentAuthorizations.AsNoTracking().Where(a => tIds.Contains(a.TournamentId)).ToListAsync();
        var result = new List<MyTournamentDto>();
        var now = AcademyClock.WallNow;
        foreach (var p in players)
        {
            var teamMatches = matches.Where(m => m.TournamentTeamId == p.TeamId).OrderBy(m => m.StartTime).ToList();
            var next = teamMatches.FirstOrDefault(m => m.StartTime >= now && m.MatchStatus is null or MatchStatus.Scheduled);
            var suspensions = await NextSuspensionsAsync(p.TeamId);
            var auth = auths.FirstOrDefault(a => a.TournamentId == p.Tournament.Id && a.StudentId == p.StudentId);
            result.Add(new MyTournamentDto
            {
                TournamentId = p.Tournament.Id, Name = p.Tournament.Name, Organizer = p.Tournament.Organizer, Status = (int)p.Tournament.Status,
                StudentId = p.StudentId, StudentName = p.StudentName, TeamName = p.TeamName, ShirtNumber = p.ShirtNumber,
                RequiresAuthorization = p.Tournament.RequiresAuthorization, AuthorizationText = p.Tournament.AuthorizationText,
                Authorized = auth != null, AuthorizedAt = auth?.AcceptedAt,
                MissingDocuments = p.Tournament.RequiredDocuments.Where(d => !p.DeliveredDocuments.Contains(d)).ToList(),
                SuspendedFor = suspensions.GetValueOrDefault(p.StudentId),
                Record = Record(teamMatches),
                NextMatch = next == null ? null : $"{next.StartTime:dd/MM HH:mm}{(string.IsNullOrWhiteSpace(next.OpponentTeam) ? "" : $" vs {next.OpponentTeam}")}",
                LastResults = teamMatches.Where(m => m.GoalsFor.HasValue && m.GoalsAgainst.HasValue).OrderByDescending(m => m.StartTime).Take(5)
                    .Select(m => new MatchResultLineDto
                    {
                        Date = m.StartTime, Round = m.Round, Opponent = m.OpponentTeam, GoalsFor = m.GoalsFor!.Value, GoalsAgainst = m.GoalsAgainst!.Value,
                    }).ToList(),
            });
        }
        return result.OrderBy(r => r.Status).ThenBy(r => r.Name).ToList();
    }
}
