using HSAcademia.Application.DTOs.Calendar;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Torneos en los que participa la academia: ficha, equipos (con su responsable), plantel y partidos.
/// El plantel del equipo convoca a cada partido por jugar; cada partido se puede ajustar aparte.
/// Convención del calendario: las horas son de pared (Lima) guardadas como UTC.
/// </summary>
public class TournamentService
{
    private const int MaxGeneratedMatches = 120;

    private readonly AppDbContext _db;
    private readonly AuditService? _audit;
    private readonly TournamentBillingService? _billing;
    private readonly TournamentComplianceService? _compliance;

    public TournamentService(AppDbContext db, AuditService? audit = null, TournamentBillingService? billing = null,
        TournamentComplianceService? compliance = null)
    {
        _db = db;
        _audit = audit;
        _billing = billing;
        _compliance = compliance;
    }

    private Task SyncSuspensionsAsync(Guid teamId) => _compliance == null ? Task.CompletedTask : _compliance.TrySyncTeamAsync(teamId);

    private Task SyncChargesAsync(Guid academyId, Guid tournamentId) =>
        _billing == null ? Task.CompletedTask : _billing.TrySyncTournamentAsync(academyId, tournamentId);
    private Task SyncMatchChargesAsync(Guid academyId, Guid eventId) =>
        _billing == null ? Task.CompletedTask : _billing.TrySyncEventAsync(academyId, eventId);

    /// <summary>Tras convocar o quitar a alguien: la inscripción depende de las convocatorias.</summary>
    private async Task SyncCallChargesAsync(Guid academyId, Guid eventId)
    {
        var tournamentId = await _db.Events.AsNoTracking().Where(e => e.Id == eventId).Select(e => e.TournamentId).FirstOrDefaultAsync();
        if (tournamentId.HasValue) await SyncChargesAsync(academyId, tournamentId.Value);
    }

    private static DateTime Wall(DateTime d) => d.Kind == DateTimeKind.Utc ? d : DateTime.SpecifyKind(d, DateTimeKind.Utc);
    private static DateTime? Wall(DateTime? d) => d.HasValue ? Wall(d.Value) : null;
    private static DateTime? DateOnlyUtc(DateTime? d) => d.HasValue ? DateTime.SpecifyKind(d.Value.Date, DateTimeKind.Utc) : null;
    private static bool IsUpcoming(Event e) => e.StartTime >= AcademyClock.WallNow &&
        e.MatchStatus is not (MatchStatus.Played or MatchStatus.WalkoverWon or MatchStatus.WalkoverLost);

    // =========================================================
    // TORNEOS
    // =========================================================
    public async Task<List<TournamentDto>> ListAsync(Guid academyId)
    {
        var tournaments = await _db.Tournaments.AsNoTracking()
            .Where(t => t.AcademyId == academyId)
            .OrderByDescending(t => t.StartDate ?? t.CreatedAt)
            .ToListAsync();
        var ids = tournaments.Select(t => t.Id).ToList();

        var teams = await _db.TournamentTeams.AsNoTracking()
            .Where(t => ids.Contains(t.TournamentId))
            .GroupBy(t => t.TournamentId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        var matches = await _db.Events.AsNoTracking()
            .Where(e => e.AcademyId == academyId && e.TournamentId != null && ids.Contains(e.TournamentId.Value))
            .Select(e => new { TournamentId = e.TournamentId!.Value, e.StartTime, e.MatchStatus, e.TimePending })
            .ToListAsync();
        var now = AcademyClock.WallNow;
        var today = AcademyClock.Today;

        return tournaments.Select(t =>
        {
            var mine = matches.Where(m => m.TournamentId == t.Id).ToList();
            var dto = Map(t);
            dto.TeamsCount = teams.GetValueOrDefault(t.Id);
            dto.MatchesCount = mine.Count;
            dto.MatchesPlayed = mine.Count(m => m.MatchStatus is MatchStatus.Played or MatchStatus.WalkoverWon or MatchStatus.WalkoverLost);
            var next = mine.Where(m => (m.TimePending ? m.StartTime.Date >= today : m.StartTime >= now) && m.MatchStatus is null or MatchStatus.Scheduled)
                .OrderBy(m => m.StartTime).FirstOrDefault();
            dto.NextMatchAt = next?.StartTime;
            dto.NextMatchTimePending = next?.TimePending ?? false;
            return dto;
        }).ToList();
    }

    public async Task<TournamentDetailDto> GetAsync(Guid academyId, Guid tournamentId)
    {
        var t = await _db.Tournaments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AcademyId == academyId && x.Id == tournamentId)
            ?? throw new KeyNotFoundException("Torneo no encontrado.");

        var teams = await _db.TournamentTeams.AsNoTracking()
            .Where(x => x.AcademyId == academyId && x.TournamentId == tournamentId)
            .OrderBy(x => x.Name)
            .ToListAsync();
        var detail = new TournamentDetailDto();
        CopyTo(Map(t), detail);
        detail.Teams = await MapTeamsAsync(academyId, teams);
        detail.Matches = await LoadMatchesAsync(academyId, tournamentId, teams);
        detail.TeamsCount = teams.Count;
        detail.MatchesCount = detail.Matches.Count;
        detail.MatchesPlayed = detail.Matches.Count(m => m.MatchStatus is (int)MatchStatus.Played or (int)MatchStatus.WalkoverWon or (int)MatchStatus.WalkoverLost);
        var nextMatch = detail.Matches
            .Where(m => (m.TimePending ? m.StartTime.Date >= AcademyClock.Today : m.StartTime >= AcademyClock.WallNow) && m.MatchStatus == (int)MatchStatus.Scheduled)
            .OrderBy(m => m.StartTime).FirstOrDefault();
        detail.NextMatchAt = nextMatch?.StartTime;
        detail.NextMatchTimePending = nextMatch?.TimePending ?? false;
        return detail;
    }

    public async Task<TournamentDto> CreateAsync(Guid academyId, CreateTournamentDto dto)
    {
        var t = new Tournament { AcademyId = academyId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        Apply(t, dto, isNew: true);
        _db.Tournaments.Add(t);
        _audit?.Add(academyId, "tournaments.create", nameof(Tournament), t.Id, new { t.Name, t.Format, t.ScheduleMode, t.StartDate, t.EndDate, t.Modality, t.Level, t.InscriptionFee, t.RefereeFee });
        await _db.SaveChangesAsync();
        if (_billing != null) await _billing.SyncSystemFeesAsync(t.Id);
        return Map(t);
    }

    public async Task<TournamentDto> UpdateAsync(Guid academyId, Guid tournamentId, UpdateTournamentDto dto)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.AcademyId == academyId && x.Id == tournamentId)
                ?? throw new KeyNotFoundException("Torneo no encontrado.");
        Apply(t, dto, isNew: false);
        t.UpdatedAt = DateTime.UtcNow;
        _audit?.Add(academyId, "tournaments.update", nameof(Tournament), t.Id, new { t.Name, t.Format, t.ScheduleMode, t.Status, t.StartDate, t.EndDate });
        await _db.SaveChangesAsync();
        if (_billing != null) await _billing.SyncSystemFeesAsync(t.Id);
        await SyncChargesAsync(academyId, t.Id);
        // Si cambiaron las fechas del torneo, se ajustan los días pendientes de cada categoría.
        foreach (var teamId in await _db.TournamentTeams.Where(x => x.TournamentId == t.Id && x.AutoRoster).Select(x => x.Id).ToListAsync())
            await SyncDaysAsync(teamId, null);
        if (_compliance != null) await _compliance.TrySyncTournamentAsync(t.Id);
        return Map(t);
    }

    /// <summary>Elimina el torneo, sus equipos y los partidos por jugar sin asistencia. Lo jugado queda en el historial.</summary>
    public async Task DeleteAsync(Guid academyId, Guid tournamentId)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.AcademyId == academyId && x.Id == tournamentId)
                ?? throw new KeyNotFoundException("Torneo no encontrado.");
        var now = DateTime.UtcNow;
        t.IsDeleted = true;
        t.DeletedAt = now;

        var teams = await _db.TournamentTeams.Where(x => x.TournamentId == tournamentId).ToListAsync();
        foreach (var team in teams) { team.IsDeleted = true; team.DeletedAt = now; }

        var removed = await RemoveUpcomingMatchesAsync(academyId, e => e.TournamentId == tournamentId);
        _audit?.Add(academyId, "tournaments.delete", nameof(Tournament), t.Id, new { t.Name, upcomingMatchesRemoved = removed });
        await _db.SaveChangesAsync();
        await SyncChargesAsync(academyId, t.Id);
    }

    private static void Apply(Tournament t, CreateTournamentDto dto, bool isNew)
    {
        t.Name = dto.Name.Trim();
        t.Organizer = dto.Organizer.Trim();
        t.MainLocation = dto.MainLocation.Trim();
        if (dto.Format.HasValue) t.Format = (TournamentFormat)dto.Format.Value;
        if (dto.ScheduleMode.HasValue) t.ScheduleMode = (TournamentScheduleMode)dto.ScheduleMode.Value;
        if (dto.Status.HasValue) t.Status = (TournamentStatus)dto.Status.Value;
        if (dto.Modality.HasValue) t.Modality = (TournamentModality)dto.Modality.Value;
        if (dto.Level.HasValue) t.Level = (TournamentLevel)dto.Level.Value;
        if (dto.InscriptionFee.HasValue) t.InscriptionFee = dto.InscriptionFee > 0 ? Math.Round(dto.InscriptionFee.Value, 2) : null;
        if (dto.RefereeFee.HasValue) t.RefereeFee = dto.RefereeFee > 0 ? Math.Round(dto.RefereeFee.Value, 2) : null;
        if (dto.RefereeSameDayDiscountPercent.HasValue)
            t.RefereeSameDayDiscountPercent = dto.RefereeSameDayDiscountPercent > 0 ? Math.Round(Math.Min(dto.RefereeSameDayDiscountPercent.Value, 100m), 2) : null;
        if (dto.DefaultCoachFee.HasValue) t.DefaultCoachFee = dto.DefaultCoachFee > 0 ? Math.Round(dto.DefaultCoachFee.Value, 2) : null;
        // Clientes anteriores no mandan las fechas: se conservan.
        var touchesSchedule = isNew || dto.ScheduleMode.HasValue || dto.StartDate.HasValue || dto.EndDate.HasValue || dto.PlayDays != null;
        if (touchesSchedule)
        {
            t.StartDate = DateOnlyUtc(dto.StartDate);
            t.EndDate = DateOnlyUtc(dto.EndDate);
            if (dto.PlayDays != null) t.PlayDays = dto.PlayDays.Where(d => d is >= 0 and <= 6).Distinct().OrderBy(d => d).ToList();
        }
        if (dto.ContactName != null) t.ContactName = Clean(dto.ContactName);
        if (dto.ContactPhone != null) t.ContactPhone = Clean(dto.ContactPhone);
        if (dto.Rules != null) t.Rules = Clean(dto.Rules);
        if (dto.YellowCardsForSuspension.HasValue) t.YellowCardsForSuspension = dto.YellowCardsForSuspension > 0 ? dto.YellowCardsForSuspension : null;
        if (dto.RedCardSuspensionMatches.HasValue) t.RedCardSuspensionMatches = dto.RedCardSuspensionMatches > 0 ? dto.RedCardSuspensionMatches : null;
        if (dto.RequiresAuthorization.HasValue) t.RequiresAuthorization = dto.RequiresAuthorization.Value;
        if (dto.AuthorizationText != null) t.AuthorizationText = Clean(dto.AuthorizationText);
        if (dto.RequiredDocuments != null)
            t.RequiredDocuments = dto.RequiredDocuments.Select(d => d?.Trim()).Where(d => !string.IsNullOrEmpty(d)).Select(d => d!.Length <= 60 ? d : d[..60])
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(15).ToList();
        if (t.RequiresAuthorization && string.IsNullOrWhiteSpace(t.AuthorizationText))
            t.AuthorizationText = "Autorizo a mi hijo(a) a participar en este torneo, incluidos los traslados con la delegación de la academia.";

        if (!touchesSchedule) return;
        switch (t.ScheduleMode)
        {
            case TournamentScheduleMode.SingleDay:
                if (t.StartDate == null) throw new InvalidOperationException("Indica el día del torneo.");
                t.EndDate = t.StartDate;
                break;
            case TournamentScheduleMode.DateRange:
                if (t.StartDate == null || t.EndDate == null) throw new InvalidOperationException("Indica la fecha de inicio y de fin del torneo.");
                if (t.PlayDays.Count == 0) throw new InvalidOperationException("Marca los días de la semana en que se juega.");
                break;
            case TournamentScheduleMode.ConsecutiveDays:
                if (t.StartDate == null || t.EndDate == null) throw new InvalidOperationException("Indica la fecha de inicio y de fin del torneo.");
                if ((t.EndDate.Value - t.StartDate.Value).TotalDays > 120) throw new InvalidOperationException("Días seguidos: el rango máximo es de 120 días.");
                break;
            case TournamentScheduleMode.Continuous:
                if (t.StartDate == null) throw new InvalidOperationException("Indica desde cuándo se juega el torneo.");
                break;
        }
        if (t.EndDate.HasValue && t.StartDate.HasValue && t.EndDate < t.StartDate)
            throw new InvalidOperationException("La fecha de fin no puede ser anterior a la de inicio.");
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static TournamentDto Map(Tournament t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Organizer = t.Organizer,
        MainLocation = t.MainLocation,
        CreatedAt = t.CreatedAt,
        Format = (int)t.Format,
        ScheduleMode = (int)t.ScheduleMode,
        Status = (int)t.Status,
        Modality = (int)t.Modality,
        Level = (int)t.Level,
        InscriptionFee = t.InscriptionFee,
        RefereeFee = t.RefereeFee,
        RefereeSameDayDiscountPercent = t.RefereeSameDayDiscountPercent,
        DefaultCoachFee = t.DefaultCoachFee,
        StartDate = t.StartDate,
        EndDate = t.EndDate,
        PlayDays = t.PlayDays ?? new List<int>(),
        ContactName = t.ContactName,
        ContactPhone = t.ContactPhone,
        Rules = t.Rules,
        YellowCardsForSuspension = t.YellowCardsForSuspension,
        RedCardSuspensionMatches = t.RedCardSuspensionMatches,
        RequiresAuthorization = t.RequiresAuthorization,
        AuthorizationText = t.AuthorizationText,
        RequiredDocuments = t.RequiredDocuments ?? new List<string>(),
    };

    private static void CopyTo(TournamentDto src, TournamentDto dst)
    {
        foreach (var p in typeof(TournamentDto).GetProperties()) p.SetValue(dst, p.GetValue(src));
    }

    // =========================================================
    // EQUIPOS
    // =========================================================
    public async Task<TournamentTeamDto> CreateTeamAsync(Guid academyId, Guid tournamentId, SaveTournamentTeamDto dto)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.AcademyId == academyId && x.Id == tournamentId)
                ?? throw new KeyNotFoundException("Torneo no encontrado.");
        await ValidateTeamAsync(academyId, dto);

        var team = new TournamentTeam { AcademyId = academyId, TournamentId = t.Id };
        ApplyTeam(team, dto);
        _db.TournamentTeams.Add(team);
        _audit?.Add(academyId, "tournaments.team.create", nameof(TournamentTeam), team.Id,
            new { tournament = t.Name, team.Name, team.CategoryIds, team.HeadCoachId, team.AssistantId });
        await _db.SaveChangesAsync();
        return (await MapTeamsAsync(academyId, new() { team })).Single();
    }

    public async Task<TournamentTeamDto> UpdateTeamAsync(Guid academyId, Guid teamId, SaveTournamentTeamDto dto)
    {
        var team = await _db.TournamentTeams.FirstOrDefaultAsync(x => x.AcademyId == academyId && x.Id == teamId)
                   ?? throw new KeyNotFoundException("Equipo no encontrado.");
        await ValidateTeamAsync(academyId, dto);

        var oldCoach = team.HeadCoachId;
        var oldCategories = team.CategoryIds.ToList();
        ApplyTeam(team, dto);
        team.UpdatedAt = DateTime.UtcNow;

        var upcoming = (await _db.Events.Where(e => e.AcademyId == academyId && e.TournamentTeamId == teamId).ToListAsync())
            .Where(IsUpcoming).ToList();
        var movedMatches = 0;
        foreach (var ev in upcoming)
        {
            // Los partidos por jugar siguen al equipo; los reemplazos puntuales (otro responsable) se respetan.
            if (dto.ApplyCoachToUpcoming && oldCoach != team.HeadCoachId && ev.TeacherId == oldCoach)
            {
                ev.TeacherId = team.HeadCoachId;
                movedMatches++;
            }
            if (!oldCategories.SequenceEqual(team.CategoryIds))
            {
                ev.CategoryIds = team.CategoryIds.ToList();
                ev.CategoryId = team.CategoryIds.FirstOrDefault();
            }
            ev.UpdatedAt = DateTime.UtcNow;
        }

        if (oldCoach != team.HeadCoachId)
            _audit?.Add(academyId, "tournaments.team.coach", nameof(TournamentTeam), team.Id,
                new { team.Name, from = oldCoach, to = team.HeadCoachId, upcomingMatchesMoved = movedMatches });
        _audit?.Add(academyId, "tournaments.team.update", nameof(TournamentTeam), team.Id,
            new { team.Name, team.CategoryIds, team.HeadCoachId, team.AssistantId, team.BirthYearFrom, team.BirthYearTo, team.MaxPlayers });
        await _db.SaveChangesAsync();
        return (await MapTeamsAsync(academyId, new() { team })).Single();
    }

    public async Task DeleteTeamAsync(Guid academyId, Guid teamId)
    {
        var team = await _db.TournamentTeams.FirstOrDefaultAsync(x => x.AcademyId == academyId && x.Id == teamId)
                   ?? throw new KeyNotFoundException("Equipo no encontrado.");
        team.IsDeleted = true;
        team.DeletedAt = DateTime.UtcNow;
        var removed = await RemoveUpcomingMatchesAsync(academyId, e => e.TournamentTeamId == teamId);
        _audit?.Add(academyId, "tournaments.team.delete", nameof(TournamentTeam), team.Id, new { team.Name, upcomingMatchesRemoved = removed });
        await _db.SaveChangesAsync();
        await SyncChargesAsync(academyId, team.TournamentId);
    }

    private async Task ValidateTeamAsync(Guid academyId, SaveTournamentTeamDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new InvalidOperationException("Ponle un nombre al equipo (ej. Sub-12 A).");
        var categoryIds = dto.CategoryIds.Distinct().ToList();
        if (categoryIds.Count == 0) throw new InvalidOperationException("Elige al menos una categoría de origen del equipo.");
        var found = await _db.Categories.CountAsync(c => c.AcademyId == academyId && categoryIds.Contains(c.Id));
        if (found != categoryIds.Count) throw new InvalidOperationException("Alguna categoría no existe.");

        foreach (var staffId in new[] { dto.HeadCoachId, dto.AssistantId }.Where(x => x.HasValue).Select(x => x!.Value))
        {
            var ok = await _db.Users.AnyAsync(u => u.Id == staffId && u.AcademyId == academyId &&
                                                   (u.Role == UserRole.Staff || u.Role == UserRole.AcademyAdmin));
            if (!ok) throw new InvalidOperationException("El responsable o asistente debe ser parte del personal.");
        }
        if (dto.HeadCoachId.HasValue && dto.HeadCoachId == dto.AssistantId)
            throw new InvalidOperationException("El asistente no puede ser el mismo responsable.");
        if (dto.BirthYearFrom.HasValue && dto.BirthYearTo.HasValue && dto.BirthYearTo < dto.BirthYearFrom)
            throw new InvalidOperationException("El rango de años de nacimiento está invertido.");
    }

    private static void ApplyTeam(TournamentTeam team, SaveTournamentTeamDto dto)
    {
        team.Name = dto.Name.Trim();
        team.CategoryIds = dto.CategoryIds.Distinct().ToList();
        team.HeadCoachId = dto.HeadCoachId;
        team.AssistantId = dto.AssistantId;
        team.BirthYearFrom = dto.BirthYearFrom;
        team.BirthYearTo = dto.BirthYearTo;
        team.MaxPlayers = dto.MaxPlayers;
        team.Notes = Clean(dto.Notes);
        team.MatchFee = dto.MatchFee is > 0 ? Math.Round(dto.MatchFee.Value, 2) : null;
    }

    private static bool OutsideYears(TournamentTeam team, DateTime? birth) =>
        birth.HasValue && ((team.BirthYearFrom.HasValue && birth.Value.Year < team.BirthYearFrom) ||
                           (team.BirthYearTo.HasValue && birth.Value.Year > team.BirthYearTo));

    private async Task<List<TournamentTeamDto>> MapTeamsAsync(Guid academyId, List<TournamentTeam> teams)
    {
        var teamIds = teams.Select(t => t.Id).ToList();
        var players = await _db.TournamentPlayers.AsNoTracking()
            .Where(p => teamIds.Contains(p.TeamId))
            .Select(p => new
            {
                p.TeamId, p.StudentId, p.ShirtNumber, p.Position, p.DeliveredDocuments,
                p.Student.FirstName, p.Student.LastName, p.Student.DateOfBirth, p.Student.IsActive,
                CategoryName = p.Student.Category.Name
            })
            .ToListAsync();
        var tournamentIds = teams.Select(t => t.TournamentId).Distinct().ToList();
        var tournaments = await _db.Tournaments.IgnoreQueryFilters().AsNoTracking().Where(t => tournamentIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id);
        var auths = await _db.TournamentAuthorizations.IgnoreQueryFilters().AsNoTracking().Where(a => tournamentIds.Contains(a.TournamentId))
            .Select(a => new { a.TournamentId, a.StudentId, a.Method }).ToListAsync();
        var teamMatches = await _db.Events.AsNoTracking().Where(e => e.TournamentTeamId != null && teamIds.Contains(e.TournamentTeamId.Value)).ToListAsync();
        var matchIds = teamMatches.Select(m => m.Id).ToList();
        var stats = await _db.MatchPlayerStats.AsNoTracking().Where(s => matchIds.Contains(s.EventId))
            .Select(s => new { s.EventId, s.StudentId, s.Minutes, s.Goals, s.YellowCards, s.RedCards }).ToListAsync();
        var suspensions = new Dictionary<Guid, Dictionary<Guid, string>>();
        if (_compliance != null) foreach (var id in teamIds) suspensions[id] = await _compliance.NextSuspensionsAsync(id);
        var categoryIds = teams.SelectMany(t => t.CategoryIds).Distinct().ToList();
        var categories = await _db.Categories.AsNoTracking().Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);
        var staffIds = teams.SelectMany(t => new[] { t.HeadCoachId, t.AssistantId }).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var staff = await _db.Users.AsNoTracking().Where(u => staffIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.FirstName + " " + u.LastName).Trim());

        return teams.Select(team =>
        {
            var roster = players.Where(p => p.TeamId == team.Id)
                .OrderBy(p => p.ShirtNumber ?? 1000).ThenBy(p => p.LastName).ThenBy(p => p.FirstName)
                .Select(p => new TournamentPlayerDto
                {
                    StudentId = p.StudentId,
                    FullName = $"{p.FirstName} {p.LastName}".Trim(),
                    CategoryName = p.CategoryName,
                    BirthDate = p.DateOfBirth,
                    ShirtNumber = p.ShirtNumber,
                    Position = p.Position,
                    IsActive = p.IsActive,
                    OutsideBirthYears = OutsideYears(team, p.DateOfBirth),
                    DeliveredDocuments = p.DeliveredDocuments ?? new List<string>(),
                    MissingDocuments = (tournaments.GetValueOrDefault(team.TournamentId)?.RequiredDocuments ?? new List<string>())
                        .Where(d => !(p.DeliveredDocuments ?? new List<string>()).Contains(d)).ToList(),
                    Authorized = tournaments.GetValueOrDefault(team.TournamentId)?.RequiresAuthorization == true
                        ? auths.Any(a => a.TournamentId == team.TournamentId && a.StudentId == p.StudentId) : null,
                    AuthorizationMethod = auths.FirstOrDefault(a => a.TournamentId == team.TournamentId && a.StudentId == p.StudentId)?.Method,
                    SuspendedFor = suspensions.GetValueOrDefault(team.Id)?.GetValueOrDefault(p.StudentId),
                    Stats = PlayerStats(stats.Where(s => s.StudentId == p.StudentId && teamMatches.Any(m => m.Id == s.EventId && m.TournamentTeamId == team.Id))
                        .Select(s => (s.Minutes, s.Goals, s.YellowCards, s.RedCards))),
                }).ToList();
            foreach (var pl in roster) pl.Fit = pl.IsActive && pl.MissingDocuments.Count == 0 && pl.Authorized != false && pl.SuspendedFor == null;

            var dto = new TournamentTeamDto
            {
                Id = team.Id,
                TournamentId = team.TournamentId,
                Name = team.Name,
                CategoryIds = team.CategoryIds,
                CategoryNames = team.CategoryIds.Select(id => categories.GetValueOrDefault(id)).Where(n => n != null).Select(n => n!).ToList(),
                HeadCoachId = team.HeadCoachId,
                HeadCoachName = team.HeadCoachId.HasValue ? staff.GetValueOrDefault(team.HeadCoachId.Value) : null,
                AssistantId = team.AssistantId,
                AssistantName = team.AssistantId.HasValue ? staff.GetValueOrDefault(team.AssistantId.Value) : null,
                BirthYearFrom = team.BirthYearFrom,
                BirthYearTo = team.BirthYearTo,
                MaxPlayers = team.MaxPlayers,
                Notes = team.Notes,
                MatchFee = team.MatchFee,
                AutoRoster = team.AutoRoster,
                MatchesCount = teamMatches.Count(m => m.TournamentTeamId == team.Id),
                Record = TournamentComplianceService.Record(teamMatches.Where(m => m.TournamentTeamId == team.Id)),
                Players = roster,
            };
            var noAuth = roster.Count(p => p.Authorized == false && p.IsActive);
            if (noAuth > 0) dto.Warnings.Add($"{noAuth} jugador(es) sin autorización del apoderado.");
            var noDocs = roster.Count(p => p.MissingDocuments.Count > 0 && p.IsActive);
            if (noDocs > 0) dto.Warnings.Add($"{noDocs} jugador(es) con documentos pendientes.");
            var suspended = roster.Count(p => p.SuspendedFor != null);
            if (suspended > 0) dto.Warnings.Add($"{suspended} jugador(es) suspendido(s) para un próximo partido.");
            if (!team.HeadCoachId.HasValue) dto.Warnings.Add("Sin entrenador responsable.");
            if (roster.Count == 0) dto.Warnings.Add("Plantel vacío.");
            if (team.MaxPlayers.HasValue && roster.Count > team.MaxPlayers)
                dto.Warnings.Add($"El plantel ({roster.Count}) supera el tope del reglamento ({team.MaxPlayers}).");
            var outside = roster.Count(p => p.OutsideBirthYears);
            if (outside > 0) dto.Warnings.Add($"{outside} jugador(es) fuera de los años de nacimiento permitidos.");
            var noBirth = roster.Count(p => !p.BirthDate.HasValue);
            if (noBirth > 0 && (team.BirthYearFrom.HasValue || team.BirthYearTo.HasValue))
                dto.Warnings.Add($"{noBirth} jugador(es) sin fecha de nacimiento registrada.");
            var dupShirts = roster.Where(p => p.ShirtNumber.HasValue).GroupBy(p => p.ShirtNumber).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupShirts.Count > 0) dto.Warnings.Add($"Número(s) de camiseta repetido(s): {string.Join(", ", dupShirts)}.");
            var inactive = roster.Count(p => !p.IsActive);
            if (inactive > 0) dto.Warnings.Add($"{inactive} jugador(es) retirado(s): ya no se convocan.");
            return dto;
        }).ToList();
    }

    // =========================================================
    // PLANTEL
    // =========================================================
    public async Task<List<TournamentCandidateDto>> GetCandidatesAsync(Guid academyId, Guid teamId)
    {
        var team = await _db.TournamentTeams.AsNoTracking().FirstOrDefaultAsync(x => x.AcademyId == academyId && x.Id == teamId)
                   ?? throw new KeyNotFoundException("Equipo no encontrado.");
        var students = await _db.Students.AsNoTracking()
            .Where(s => s.AcademyId == academyId && s.IsActive)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new { s.Id, s.FirstName, s.LastName, s.CategoryId, CategoryName = s.Category.Name, s.DateOfBirth })
            .ToListAsync();
        var others = await _db.TournamentPlayers.AsNoTracking()
            .Where(p => p.Team.TournamentId == team.TournamentId && p.TeamId != teamId)
            .Select(p => new { p.StudentId, p.Team.Name })
            .ToListAsync();

        return students.Select(s => new TournamentCandidateDto
        {
            StudentId = s.Id,
            FullName = $"{s.FirstName} {s.LastName}".Trim(),
            CategoryId = s.CategoryId,
            CategoryName = s.CategoryName,
            BirthDate = s.DateOfBirth,
            InTeamCategories = team.CategoryIds.Contains(s.CategoryId),
            OutsideBirthYears = OutsideYears(team, s.DateOfBirth),
            OtherTeams = others.Where(o => o.StudentId == s.Id).Select(o => o.Name).ToList(),
        }).ToList();
    }

    /// <summary>Reemplaza el plantel. Los partidos por jugar convocan a los nuevos y retiran a quienes salieron.</summary>
    public async Task<RosterSaveResultDto> SaveRosterAsync(Guid academyId, Guid teamId, SaveTournamentRosterDto dto)
    {
        var team = await _db.TournamentTeams.Include(t => t.Tournament)
                       .FirstOrDefaultAsync(x => x.AcademyId == academyId && x.Id == teamId)
                   ?? throw new KeyNotFoundException("Equipo no encontrado.");

        var wanted = dto.Players.GroupBy(p => p.StudentId).Select(g => g.Last()).ToList();
        var wantedIds = wanted.Select(p => p.StudentId).ToList();
        var validIds = await _db.Students.Where(s => s.AcademyId == academyId && wantedIds.Contains(s.Id))
            .Select(s => new { s.Id, s.IsActive }).ToListAsync();
        var current = await _db.TournamentPlayers.Where(p => p.TeamId == teamId).ToListAsync();
        var currentIds = current.Select(p => p.StudentId).ToHashSet();

        // Un retirado ya inscrito se conserva; no se puede inscribir uno nuevo.
        var unknown = wantedIds.Except(validIds.Select(v => v.Id)).ToList();
        if (unknown.Count > 0) throw new InvalidOperationException("Algún alumno no existe.");
        var newInactive = validIds.Where(v => !v.IsActive && !currentIds.Contains(v.Id)).ToList();
        if (newInactive.Count > 0) throw new InvalidOperationException("No se puede inscribir a un alumno retirado.");

        var now = DateTime.UtcNow;
        foreach (var p in current.Where(p => !wantedIds.Contains(p.StudentId)))
            _db.TournamentPlayers.Remove(p);
        foreach (var w in wanted)
        {
            var existing = current.FirstOrDefault(p => p.StudentId == w.StudentId);
            if (existing == null)
            {
                _db.TournamentPlayers.Add(new TournamentPlayer
                {
                    AcademyId = academyId, TeamId = teamId, StudentId = w.StudentId,
                    ShirtNumber = w.ShirtNumber, Position = Clean(w.Position), CreatedAt = now, UpdatedAt = now,
                    DeliveredDocuments = CleanDocs(w.DeliveredDocuments, team.Tournament.RequiredDocuments) ?? new List<string>(),
                });
            }
            else
            {
                var docs = CleanDocs(w.DeliveredDocuments, team.Tournament.RequiredDocuments);
                if (existing.ShirtNumber != w.ShirtNumber || existing.Position != Clean(w.Position) ||
                    (docs != null && !docs.SequenceEqual(existing.DeliveredDocuments)))
                {
                    existing.ShirtNumber = w.ShirtNumber;
                    existing.Position = Clean(w.Position);
                    if (docs != null) existing.DeliveredDocuments = docs;
                    existing.UpdatedAt = now;
                }
            }
        }

        var added = wantedIds.Where(id => !currentIds.Contains(id)).ToList();
        var removed = currentIds.Where(id => !wantedIds.Contains(id)).ToList();
        var activeAdded = added.Where(id => validIds.Any(v => v.Id == id && v.IsActive)).ToList();

        var upcoming = (await _db.Events.Where(e => e.AcademyId == academyId && e.TournamentTeamId == teamId).ToListAsync())
            .Where(e => IsUpcoming(e) && !e.TimePending).OrderBy(e => e.StartTime).ToList();
        var result = new RosterSaveResultDto();
        if (upcoming.Count > 0 && (activeAdded.Count > 0 || removed.Count > 0))
        {
            var ids = upcoming.Select(e => e.Id).ToList();
            var calls = await _db.EventCalls.Where(c => ids.Contains(c.EventId)).ToListAsync();
            foreach (var c in calls.Where(c => removed.Contains(c.StudentId)))
            {
                _db.EventCalls.Remove(c);
                result.CallsRemoved++;
            }
            var newCalls = new List<EventCall>();
            foreach (var ev in upcoming)
                foreach (var sid in activeAdded.Where(sid => !calls.Any(c => c.EventId == ev.Id && c.StudentId == sid)))
                    newCalls.Add(NewCall(academyId, ev.Id, sid));
            _db.EventCalls.AddRange(newCalls);
            result.CallsAdded = newCalls.Count;
            result.MatchesUpdated = upcoming.Count;
            await AddCallNoticesAsync(academyId, team, upcoming, activeAdded);
        }

        _audit?.Add(academyId, "tournaments.team.roster", nameof(TournamentTeam), team.Id,
            new { team.Name, added, removed, players = wantedIds.Count });
        if (_compliance != null && activeAdded.Count > 0)
        {
            var names = await _db.Students.AsNoTracking().Where(s => activeAdded.Contains(s.Id)).Select(s => new { s.Id, s.FirstName }).ToListAsync();
            await _compliance.AddAuthorizationNoticesAsync(team.Tournament, names.Select(n => (n.Id, n.FirstName)));
        }
        await _db.SaveChangesAsync();
        await SyncChargesAsync(academyId, team.TournamentId);
        await SyncSuspensionsAsync(team.Id);
        result.Team = (await MapTeamsAsync(academyId, new() { team })).Single();
        return result;
    }

    /// <summary>Solo documentos que pide el torneo; null = conservar.</summary>
    private static List<string>? CleanDocs(List<string>? docs, List<string> required) =>
        docs?.Where(d => required.Contains(d)).Distinct().ToList();

    private static PlayerTournamentStatsDto PlayerStats(IEnumerable<(int Minutes, int Goals, int Yellow, int Red)> rows)
    {
        var list = rows.ToList();
        return new PlayerTournamentStatsDto
        {
            Matches = list.Count, Minutes = list.Sum(r => r.Minutes), Goals = list.Sum(r => r.Goals),
            YellowCards = list.Sum(r => r.Yellow), RedCards = list.Sum(r => r.Red),
        };
    }

    private static EventCall NewCall(Guid academyId, Guid eventId, Guid studentId) => new()
    {
        AcademyId = academyId, EventId = eventId, StudentId = studentId,
        IsConfirmed = null, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    /// <summary>Un solo aviso por alumno aunque lo convoquen a varios partidos a la vez.</summary>
    private async Task AddCallNoticesAsync(Guid academyId, TournamentTeam team, List<Event> matches, List<Guid> studentIds)
    {
        var future = matches.Where(m => m.StartTime >= AcademyClock.WallNow).OrderBy(m => m.StartTime).ToList();
        if (future.Count == 0 || studentIds.Count == 0) return;
        var first = future[0];
        var names = await _db.Students.AsNoTracking().Where(s => studentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.FirstName);
        var tournamentName = team.Tournament?.Name ?? (await _db.Tournaments.AsNoTracking()
            .Where(t => t.Id == team.TournamentId).Select(t => t.Name).FirstOrDefaultAsync()) ?? "Torneo";
        var body = future.Count == 1
            ? $"{tournamentName} · {team.Name}{Vs(first)} · {first.StartTime:dd/MM HH:mm}" +
              (first.CallTime.HasValue ? $" (citación {first.CallTime:HH:mm})" : "") + ". Confirma su asistencia en la app."
            : $"{tournamentName} · {team.Name}: {future.Count} partidos, el primero el {first.StartTime:dd/MM HH:mm}" +
              (first.CallTime.HasValue ? $" (citación {first.CallTime:HH:mm})" : "") + ". Confirma cada partido en la app.";
        if (body.Length > 500) body = body[..500];

        _db.FamilyNotices.AddRange(studentIds.Where(names.ContainsKey).Select(sid => new FamilyNotice
        {
            AcademyId = academyId,
            StudentId = sid,
            EventId = first.Id,
            Type = FamilyNoticeType.EventCall,
            Title = $"Convocatoria de {names[sid]}",
            Body = body,
        }));
    }

    private static string Vs(Event e) => string.IsNullOrWhiteSpace(e.OpponentTeam) ? "" : $" vs {e.OpponentTeam}";

    // =========================================================
    // FLUJO DESDE EL CALENDARIO: categorías que juegan el torneo
    // =========================================================
    /// <summary>
    /// Asocia categorías al torneo (una "participación" interna con su responsable) y crea los días del torneo
    /// según su configuración, con hora y rival por definir. Si ya existe para esas mismas categorías, la reutiliza.
    /// </summary>
    public async Task<ParticipationResultDto> CreateParticipationAsync(Guid academyId, Guid tournamentId, CreateParticipationDto dto)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(x => x.AcademyId == academyId && x.Id == tournamentId)
                ?? throw new KeyNotFoundException("Torneo no encontrado.");
        if (t.Status == TournamentStatus.Cancelled) throw new InvalidOperationException("El torneo está cancelado.");
        if (t.StartDate == null) throw new InvalidOperationException("Configura las fechas del torneo antes de agregar categorías.");
        var categoryIds = dto.CategoryIds.Distinct().ToList();
        if (categoryIds.Count == 0) throw new InvalidOperationException("Elige al menos una categoría.");
        var categories = await _db.Categories.AsNoTracking().Include(c => c.Headquarter)
            .Where(c => c.AcademyId == academyId && categoryIds.Contains(c.Id))
            .OrderBy(c => c.Name).ToListAsync();
        if (categories.Count != categoryIds.Count) throw new InvalidOperationException("Alguna categoría no existe.");
        if (categories.Select(c => c.HeadquarterId).Distinct().Count() > 1)
            throw new InvalidOperationException("Las categorías deben ser de una misma sede. Para otra sede, agrégalas aparte.");
        var hqName = categories[0].Headquarter?.Name;
        if (dto.HeadCoachId.HasValue && !await _db.Users.AnyAsync(u => u.Id == dto.HeadCoachId && u.AcademyId == academyId &&
                                                                       (u.Role == UserRole.Staff || u.Role == UserRole.AcademyAdmin)))
            throw new InvalidOperationException("El responsable debe ser parte del personal.");

        var sorted = categoryIds.OrderBy(x => x).ToList();
        var existing = (await _db.TournamentTeams.Where(x => x.TournamentId == tournamentId && x.AutoRoster).ToListAsync())
            .FirstOrDefault(x => x.CategoryIds.OrderBy(c => c).SequenceEqual(sorted));
        var result = new ParticipationResultDto();
        TournamentTeam team;
        if (existing == null)
        {
            var name = string.Join(" + ", categories.Select(c => c.Name)) + (string.IsNullOrWhiteSpace(hqName) ? "" : $" · {hqName}");
            team = new TournamentTeam
            {
                AcademyId = academyId, TournamentId = tournamentId, Name = name.Length <= 120 ? name : name[..120],
                CategoryIds = categoryIds, HeadCoachId = dto.HeadCoachId, AutoRoster = true,
            };
            _db.TournamentTeams.Add(team);
            _audit?.Add(academyId, "tournaments.participation.create", nameof(TournamentTeam), team.Id, new { tournament = t.Name, team.Name, team.HeadCoachId });
            await _db.SaveChangesAsync();
        }
        else
        {
            team = existing;
            var fullName = string.Join(" + ", categories.Select(c => c.Name)) + (string.IsNullOrWhiteSpace(hqName) ? "" : $" · {hqName}");
            if (team.Name != fullName && fullName.Length <= 120) team.Name = fullName;
            if (dto.HeadCoachId.HasValue && dto.HeadCoachId != team.HeadCoachId)
            {
                team.HeadCoachId = dto.HeadCoachId;
                result.Warnings.Add("Las categorías ya estaban en el torneo: se actualizó el responsable de sus días pendientes.");
            }
            else result.Warnings.Add("Estas categorías ya estaban en el torneo: se agregaron solo los días que faltaban.");
            await _db.SaveChangesAsync();
        }

        await SyncAutoRosterAsync(team.Id);
        result.DaysCreated = await SyncDaysAsync(team.Id, dto.CoachFee);
        result.TeamId = team.Id;
        result.TeamName = team.Name;
        result.Players = await _db.TournamentPlayers.CountAsync(p => p.TeamId == team.Id);
        if (result.DaysCreated == 0 && existing == null) result.Warnings.Add("No quedan días por jugar en las fechas del torneo.");
        return result;
    }

    /// <summary>Días del torneo según su configuración (solo fecha).</summary>
    public static List<DateTime> ScheduleDays(Tournament t)
    {
        var days = new List<DateTime>();
        if (t.StartDate == null) return days;
        var from = t.StartDate.Value.Date;
        var to = (t.EndDate ?? from.AddDays(56)).Date;
        if ((to - from).TotalDays > 370) to = from.AddDays(370);
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var ok = t.ScheduleMode switch
            {
                TournamentScheduleMode.SingleDay => d == from,
                TournamentScheduleMode.ConsecutiveDays => true,
                _ => t.PlayDays.Count == 0 || t.PlayDays.Contains((int)d.DayOfWeek),
            };
            if (ok) days.Add(DateTime.SpecifyKind(d, DateTimeKind.Utc));
        }
        return days;
    }

    /// <summary>
    /// Crea los días que faltan (desde hoy) con hora y rival por definir, y quita los días pendientes que ya no
    /// están en las fechas del torneo (si no tienen asistencia). Devuelve cuántos creó.
    /// </summary>
    public async Task<int> SyncDaysAsync(Guid teamId, decimal? coachFeeForNew)
    {
        var team = await _db.TournamentTeams.Include(x => x.Tournament).FirstOrDefaultAsync(x => x.Id == teamId);
        if (team == null || !team.AutoRoster) return 0;
        var t = team.Tournament;
        var today = DateTime.SpecifyKind(AcademyClock.Today, DateTimeKind.Utc);
        var schedule = t.Status == TournamentStatus.Cancelled ? new List<DateTime>() : ScheduleDays(t);
        var events = await _db.Events.Where(e => e.TournamentTeamId == teamId).ToListAsync();
        var taken = events.Select(e => e.StartTime.Date).ToHashSet();

        var marked = (await _db.Attendances.Where(a => a.EventId != null && events.Select(e => e.Id).Contains(a.EventId.Value))
            .Select(a => a.EventId!.Value).Distinct().ToListAsync()).ToHashSet();
        foreach (var ev in events.Where(e => e.TimePending && e.StartTime.Date >= today && !schedule.Contains(e.StartTime.Date) && !marked.Contains(e.Id)))
        {
            ev.IsDeleted = true; ev.DeletedAt = DateTime.UtcNow; ev.IsActive = false;
        }
        var sedes = await _db.Categories.AsNoTracking().Where(c => team.CategoryIds.Contains(c.Id)).Select(c => c.HeadquarterId).Distinct().ToListAsync();
        Guid? sedeId = sedes.Count == 1 ? sedes[0] : null;
        foreach (var ev in events.Where(e => e.TimePending && e.StartTime.Date >= today && !e.IsDeleted))
        {
            ev.TeacherId = team.HeadCoachId;
            ev.HeadquarterId ??= sedeId;
        }

        // Todos los días del torneo, también los ya pasados (el torneo se puede registrar tarde;
        // el administrador completa y toma asistencia fuera de plazo).
        var created = 0;
        foreach (var day in schedule.Where(d => !taken.Contains(d)))
        {
            var ev = NewMatch(team.AcademyId, team, day, day.AddHours(1));
            ev.TimePending = true;
            ev.HeadquarterId = sedeId;
            ev.Location = Clean(t.MainLocation);
            ev.CoachFee = coachFeeForNew is > 0 ? coachFeeForNew : t.DefaultCoachFee;
            ev.Title = MatchTitle(team, ev);
            _db.Events.Add(ev);
            events.Add(ev);
            created++;
        }
        RenumberRounds(events);
        if (created > 0)
            _audit?.Add(team.AcademyId, "tournaments.days.create", nameof(TournamentTeam), teamId, new { team.Name, created });
        await _db.SaveChangesAsync();
        return created;
    }

    /// <summary>
    /// "Fecha N" en orden cronológico, un número por día (todos los encuentros del mismo día comparten la fecha).
    /// Respeta las fases con otro nombre (ej. "Semifinal").
    /// </summary>
    private static void RenumberRounds(IEnumerable<Event> events)
    {
        static bool Auto(Event e) => string.IsNullOrWhiteSpace(e.Round) || System.Text.RegularExpressions.Regex.IsMatch(e.Round, @"^Fecha \d+$");
        var n = 0;
        foreach (var day in events.Where(e => !e.IsDeleted).GroupBy(e => e.StartTime.Date).OrderBy(g => g.Key))
        {
            var auto = day.Where(Auto).ToList();
            if (auto.Count == 0) continue;
            n++;
            foreach (var ev in auto) ev.Round = $"Fecha {n}";
        }
    }

    private async Task RenumberTeamAsync(Guid teamId)
    {
        var events = await _db.Events.Where(e => e.TournamentTeamId == teamId).ToListAsync();
        RenumberRounds(events);
    }

    /// <summary>Plantel automático = alumnos activos de las categorías (conserva camisetas y documentos).</summary>
    public async Task SyncAutoRosterAsync(Guid teamId)
    {
        var team = await _db.TournamentTeams.AsNoTracking().FirstOrDefaultAsync(x => x.Id == teamId);
        if (team == null || !team.AutoRoster) return;
        var students = await _db.Students.AsNoTracking()
            .Where(s => s.AcademyId == team.AcademyId && s.IsActive && team.CategoryIds.Contains(s.CategoryId))
            .Select(s => s.Id).ToListAsync();
        var current = await _db.TournamentPlayers.AsNoTracking().Where(p => p.TeamId == teamId).ToListAsync();
        var currentIds = current.Select(p => p.StudentId).ToHashSet();
        if (currentIds.SetEquals(students)) return;
        var players = current.Where(p => students.Contains(p.StudentId))
            .Select(p => new SaveTournamentPlayerDto { StudentId = p.StudentId, ShirtNumber = p.ShirtNumber, Position = p.Position, DeliveredDocuments = p.DeliveredDocuments })
            .Concat(students.Where(id => !currentIds.Contains(id)).Select(id => new SaveTournamentPlayerDto { StudentId = id }))
            .ToList();
        await SaveRosterAsync(team.AcademyId, teamId, new SaveTournamentRosterDto { Players = players });
    }

    /// <summary>Respaldo horario: planteles automáticos de torneos vigentes (altas y retiros de alumnos).</summary>
    public async Task SyncAutoRostersAllAsync(CancellationToken ct = default)
    {
        var ids = await _db.TournamentTeams.AsNoTracking()
            .Where(x => x.AutoRoster && x.Tournament.Status != TournamentStatus.Cancelled && x.Tournament.Status != TournamentStatus.Finished)
            .Select(x => x.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) break;
            try { await SyncAutoRosterAsync(id); }
            catch { _db.ChangeTracker.Clear(); }
        }
    }

    // =========================================================
    // PARTIDOS
    // =========================================================
    public async Task<TournamentMatchDto> CreateMatchAsync(Guid academyId, Guid teamId, SaveTournamentMatchDto dto)
    {
        var team = await _db.TournamentTeams.Include(t => t.Tournament)
                       .FirstOrDefaultAsync(x => x.AcademyId == academyId && x.Id == teamId)
                   ?? throw new KeyNotFoundException("Equipo no encontrado.");
        await ValidateMatchAsync(academyId, dto);
        var encounter = dto.StudentIds != null;
        if (encounter) await ValidateEncounterAsync(academyId, dto, Array.Empty<Guid>());

        // Encuentro en un día del torneo aún vacío: se usa ese día (no queda un "por definir" suelto).
        Event? ev = null;
        if (encounter)
        {
            var day = Wall(dto.StartTime).Date;
            var candidates = (await _db.Events.Where(e => e.TournamentTeamId == teamId && e.TimePending).ToListAsync())
                .Where(e => e.StartTime.Date == day).ToList();
            foreach (var c in candidates)
            {
                if (await _db.EventCalls.AnyAsync(x => x.EventId == c.Id) || await _db.Attendances.AnyAsync(a => a.EventId == c.Id)) continue;
                ev = c;
                break;
            }
        }
        var reused = ev != null;
        if (ev == null)
        {
            ev = NewMatch(academyId, team, Wall(dto.StartTime), Wall(dto.EndTime));
            _db.Events.Add(ev);
        }
        else
        {
            ev.StartTime = Wall(dto.StartTime);
            ev.EndTime = Wall(dto.EndTime);
            ev.UpdatedAt = DateTime.UtcNow;
        }
        ev.TimePending = false;
        var keepRound = ev.Round;
        ApplyMatch(ev, team, dto);
        if (ev.Round == null && encounter)
        {
            // Mismo día = misma fecha del torneo.
            var evDay = ev.StartTime.Date;
            var evId = ev.Id;
            ev.Round = keepRound ?? (await _db.Events.AsNoTracking()
                .Where(e => e.TournamentTeamId == teamId && e.Id != evId && e.Round != null)
                .Select(e => new { e.StartTime, e.Round }).ToListAsync())
                .Where(e => e.StartTime.Date == evDay).Select(e => e.Round).FirstOrDefault();
        }
        ev.CoachFee = dto.CoachFee is > 0 ? Math.Round(dto.CoachFee.Value, 2) : null;
        if (encounter) ev.ReferenceCategoryId = dto.ReferenceCategoryId;

        var called = encounter ? dto.StudentIds!.Distinct().ToList() : await ActiveRosterAsync(teamId);
        var newEventId = ev.Id;
        _db.EventCalls.AddRange(called.Select(sid => NewCall(academyId, newEventId, sid)));
        if (IsUpcoming(ev) && AttendanceWindow.PendingMatchReason(ev) == null) await AddCallNoticesAsync(academyId, team, new() { ev }, called);

        _audit?.Add(academyId, encounter ? "tournaments.encounter.create" : "tournaments.match.create", nameof(Event), ev.Id,
            new { team = team.Name, ev.StartTime, ev.OpponentTeam, ev.Round, ev.ReferenceCategoryId, called = called.Count, reusedDay = reused });
        await _db.SaveChangesAsync();
        if (team.AutoRoster) { await RenumberTeamAsync(team.Id); await _db.SaveChangesAsync(); }
        await SyncChargesAsync(academyId, team.TournamentId);
        await SyncSuspensionsAsync(team.Id);
        return (await LoadMatchesAsync(academyId, team.TournamentId, new() { team }, ev.Id)).Single();
    }

    public async Task<TournamentMatchDto> UpdateMatchAsync(Guid academyId, Guid eventId, SaveTournamentMatchDto dto)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.AcademyId == academyId && e.Id == eventId && e.TournamentId != null)
                 ?? throw new KeyNotFoundException("Partido no encontrado.");
        await ValidateMatchAsync(academyId, dto);
        var team = ev.TournamentTeamId.HasValue
            ? await _db.TournamentTeams.Include(t => t.Tournament).FirstOrDefaultAsync(t => t.Id == ev.TournamentTeamId.Value)
            : null;

        var currentCalls = await _db.EventCalls.Where(c => c.EventId == ev.Id).ToListAsync();
        if (dto.StudentIds != null) await ValidateEncounterAsync(academyId, dto, currentCalls.Select(c => c.StudentId));

        var oldStart = ev.StartTime;
        var oldTeacher = ev.TeacherId;
        var wasPending = AttendanceWindow.PendingMatchReason(ev) != null;
        ev.StartTime = Wall(dto.StartTime);
        ev.EndTime = Wall(dto.EndTime);
        ev.TimePending = false;
        ApplyMatch(ev, team, dto);
        ev.CoachFee = dto.CoachFee is > 0 ? Math.Round(dto.CoachFee.Value, 2) : null;
        if (dto.ReferenceCategoryId.HasValue) ev.ReferenceCategoryId = dto.ReferenceCategoryId;
        ev.UpdatedAt = DateTime.UtcNow;
        var ready = AttendanceWindow.PendingMatchReason(ev) == null && ev.MatchStatus is not (MatchStatus.Postponed or MatchStatus.Suspended);

        if (dto.StudentIds != null)
        {
            // Encuentro: los convocados son exactamente los elegidos (la asistencia ya marcada se conserva).
            var wanted = dto.StudentIds.Distinct().ToList();
            var removedCalls = currentCalls.Where(c => !wanted.Contains(c.StudentId)).ToList();
            _db.EventCalls.RemoveRange(removedCalls);
            var fresh = wanted.Where(id => currentCalls.All(c => c.StudentId != id)).ToList();
            _db.EventCalls.AddRange(fresh.Select(sid => NewCall(academyId, ev.Id, sid)));
            // Se avisa recién cuando el encuentro tiene hora y rival (al completarlo, a todos los convocados).
            var notify = wasPending ? wanted : fresh;
            if (team != null && ready && IsUpcoming(ev) && notify.Count > 0) await AddCallNoticesAsync(academyId, team, new() { ev }, notify);
            if (removedCalls.Count > 0 || fresh.Count > 0)
                _audit?.Add(academyId, "tournaments.encounter.calls", nameof(Event), ev.Id,
                    new { added = fresh, removed = removedCalls.Select(c => c.StudentId).ToList() });
        }
        // Recién con hora y rival se convoca al plantel y se avisa a las familias.
        // (Aunque el partido ya haya empezado: la lista de asistencia son los convocados.)
        else if (team != null && wasPending && ready)
        {
            var roster = await ActiveRosterAsync(team.Id);
            var already = (await _db.EventCalls.Where(c => c.EventId == ev.Id).Select(c => c.StudentId).ToListAsync()).ToHashSet();
            var fresh = roster.Where(id => !already.Contains(id)).ToList();
            _db.EventCalls.AddRange(fresh.Select(sid => NewCall(academyId, ev.Id, sid)));
            if (IsUpcoming(ev)) await AddCallNoticesAsync(academyId, team, new() { ev }, fresh);
        }

        _audit?.Add(academyId, "tournaments.match.update", nameof(Event), ev.Id, new
        {
            ev.StartTime, previousStart = oldStart != ev.StartTime ? oldStart : (DateTime?)null,
            ev.MatchStatus, ev.GoalsFor, ev.GoalsAgainst,
            teacherFrom = oldTeacher != ev.TeacherId ? oldTeacher : null, teacherTo = oldTeacher != ev.TeacherId ? ev.TeacherId : null
        });
        await _db.SaveChangesAsync();
        // Convocar cambia la inscripción (una sola vez) además del arbitraje: se recalcula el torneo.
        await SyncChargesAsync(academyId, ev.TournamentId!.Value);
        if (ev.TournamentTeamId.HasValue) await SyncSuspensionsAsync(ev.TournamentTeamId.Value);
        return (await LoadMatchesAsync(academyId, ev.TournamentId!.Value, team != null ? new() { team } : new(), ev.Id)).Single();
    }

    /// <summary>Convocados de un encuentro: alumnos activos de la academia (los ya convocados se conservan aunque se hayan retirado).</summary>
    private async Task ValidateEncounterAsync(Guid academyId, SaveTournamentMatchDto dto, IEnumerable<Guid> alreadyCalled)
    {
        if (dto.ReferenceCategoryId.HasValue && !await _db.Categories.AnyAsync(c => c.AcademyId == academyId && c.Id == dto.ReferenceCategoryId))
            throw new InvalidOperationException("Categoría de referencia no encontrada.");
        var ids = dto.StudentIds!.Distinct().Except(alreadyCalled).ToList();
        if (ids.Count == 0) return;
        var ok = await _db.Students.CountAsync(s => s.AcademyId == academyId && s.IsActive && ids.Contains(s.Id));
        if (ok != ids.Count) throw new InvalidOperationException("Algún alumno no existe o está retirado.");
    }

    // ─────────────── Encuentros: candidatos y arbitraje ───────────────
    /// <summary>Fecha de nacimiento más antigua que admite la categoría (corte para "mayores").</summary>
    private static DateTime? OldestBirth(DateTime? start, DateTime? end) =>
        start.HasValue && end.HasValue ? (start < end ? start : end) : start ?? end;

    /// <summary>Mayor que la categoría de referencia: nació antes de su corte (sin fecha de nacimiento, se usa su categoría).</summary>
    private static bool IsSuperior(DateTime? birth, DateTime? ownCategoryOldest, DateTime? referenceOldest)
    {
        if (!referenceOldest.HasValue) return false;
        var d = birth ?? ownCategoryOldest;
        return d.HasValue && d.Value.Date < referenceOldest.Value.Date;
    }

    public async Task<List<EncounterCandidateDto>> GetEncounterCandidatesAsync(Guid academyId, Guid tournamentId, Guid? referenceCategoryId, DateTime? date, Guid? eventId)
    {
        if (!await _db.Tournaments.AnyAsync(t => t.AcademyId == academyId && t.Id == tournamentId))
            throw new KeyNotFoundException("Torneo no encontrado.");
        var reference = referenceCategoryId.HasValue
            ? await _db.Categories.AsNoTracking().Where(c => c.AcademyId == academyId && c.Id == referenceCategoryId)
                .Select(c => new { StartDateOfBirth = c.IsFree ? null : c.StartDateOfBirth, EndDateOfBirth = c.IsFree ? null : c.EndDateOfBirth }).FirstOrDefaultAsync()
            : null;
        var refOldest = reference != null ? OldestBirth(reference.StartDateOfBirth, reference.EndDateOfBirth) : null;

        var students = await _db.Students.AsNoTracking()
            .Where(s => s.AcademyId == academyId && s.IsActive)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new
            {
                s.Id, s.FirstName, s.LastName, s.CategoryId, CategoryName = s.Category.Name, s.Category.HeadquarterId, s.DateOfBirth,
                CatStart = s.Category.IsFree ? null : s.Category.StartDateOfBirth, CatEnd = s.Category.IsFree ? null : s.Category.EndDateOfBirth
            })
            .ToListAsync();

        var sameDay = new List<(Guid StudentId, string Label)>();
        if (date.HasValue)
        {
            var from = DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc);
            var to = from.AddDays(1);
            var matches = await _db.Events.AsNoTracking()
                .Where(e => e.AcademyId == academyId && e.TournamentId == tournamentId && e.StartTime >= from && e.StartTime < to && e.Id != eventId)
                .Select(e => new { e.Id, e.StartTime, e.TimePending, e.OpponentTeam, Ref = e.ReferenceCategoryId })
                .ToListAsync();
            var refIds = matches.Where(m => m.Ref.HasValue).Select(m => m.Ref!.Value).Distinct().ToList();
            var refNames = await _db.Categories.AsNoTracking().Where(c => refIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);
            var ids = matches.Select(m => m.Id).ToList();
            var calls = await _db.EventCalls.AsNoTracking().Where(c => ids.Contains(c.EventId)).Select(c => new { c.EventId, c.StudentId }).ToListAsync();
            foreach (var c in calls)
            {
                var m = matches.First(x => x.Id == c.EventId);
                var label = (m.TimePending ? "hora por definir" : m.StartTime.ToString("HH:mm")) +
                            (m.Ref.HasValue && refNames.TryGetValue(m.Ref.Value, out var rn) ? $" · {rn}" : "") +
                            (string.IsNullOrWhiteSpace(m.OpponentTeam) ? "" : $" vs {m.OpponentTeam}");
                sameDay.Add((c.StudentId, label));
            }
        }

        return students.Select(s => new EncounterCandidateDto
        {
            StudentId = s.Id,
            FullName = $"{s.FirstName} {s.LastName}".Trim(),
            CategoryId = s.CategoryId,
            CategoryName = s.CategoryName,
            HeadquarterId = s.HeadquarterId,
            BirthDate = s.DateOfBirth,
            SameCategory = s.CategoryId == referenceCategoryId,
            Superior = s.CategoryId != referenceCategoryId && IsSuperior(s.DateOfBirth, OldestBirth(s.CatStart, s.CatEnd), refOldest),
            OtherMatches = sameDay.Where(x => x.StudentId == s.Id).Select(x => x.Label).ToList(),
        }).ToList();
    }

    /// <summary>Exonera (o vuelve a cobrar) el arbitraje de todo un encuentro. Decisión del administrador.</summary>
    public async Task<TournamentMatchDto> SetRefereeWaiverAsync(Guid academyId, Guid eventId, RefereeWaiverDto dto)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.AcademyId == academyId && e.Id == eventId && e.TournamentId != null)
                 ?? throw new KeyNotFoundException("Encuentro no encontrado.");
        if (dto.Waived && string.IsNullOrWhiteSpace(dto.Note)) throw new InvalidOperationException("Indica el motivo de la exoneración.");
        ev.RefereeWaived = dto.Waived;
        ev.RefereeWaivedNote = dto.Waived ? Clean(dto.Note) : null;
        ev.UpdatedAt = DateTime.UtcNow;
        _audit?.Add(academyId, "tournaments.encounter.referee_waiver", nameof(Event), ev.Id, new { dto.Waived, ev.RefereeWaivedNote });
        await _db.SaveChangesAsync();
        await SyncMatchChargesAsync(academyId, ev.Id);
        var teams = await _db.TournamentTeams.AsNoTracking().Where(t => t.TournamentId == ev.TournamentId).ToListAsync();
        return (await LoadMatchesAsync(academyId, ev.TournamentId!.Value, teams, ev.Id)).Single();
    }

    public async Task<GenerateTournamentMatchesResultDto> GenerateMatchesAsync(Guid academyId, Guid teamId, GenerateTournamentMatchesDto dto)
    {
        var team = await _db.TournamentTeams.Include(t => t.Tournament)
                       .FirstOrDefaultAsync(x => x.AcademyId == academyId && x.Id == teamId)
                   ?? throw new KeyNotFoundException("Equipo no encontrado.");
        if (!TimeSpan.TryParse(dto.StartTime, out var start) || !TimeSpan.TryParse(dto.EndTime, out var end))
            throw new InvalidOperationException("Hora inválida (usa HH:mm).");
        if (end <= start) throw new InvalidOperationException("La hora de fin debe ser posterior a la de inicio.");
        var days = dto.DaysOfWeek.Where(d => d is >= 0 and <= 6).Distinct().ToList();
        if (days.Count == 0) throw new InvalidOperationException("Elige al menos un día de juego.");
        var from = dto.FromDate.Date;
        var to = dto.ToDate.Date;
        if (to < from) throw new InvalidOperationException("La fecha final debe ser posterior a la inicial.");
        if ((to - from).TotalDays > 370) throw new InvalidOperationException("El rango máximo es de un año.");
        if (dto.HeadquarterId.HasValue && !await _db.Headquarters.AnyAsync(h => h.Id == dto.HeadquarterId && h.AcademyId == academyId))
            throw new InvalidOperationException("Sede no encontrada.");

        var existingDays = (await _db.Events.AsNoTracking()
                .Where(e => e.AcademyId == academyId && e.TournamentTeamId == teamId &&
                            e.StartTime >= DateTime.SpecifyKind(from, DateTimeKind.Utc) &&
                            e.StartTime < DateTime.SpecifyKind(to.AddDays(1), DateTimeKind.Utc))
                .Select(e => e.StartTime).ToListAsync())
            .Select(d => d.Date).ToHashSet();
        var numbered = await _db.Events.CountAsync(e => e.AcademyId == academyId && e.TournamentTeamId == teamId);

        var result = new GenerateTournamentMatchesResultDto();
        var created = new List<Event>();
        var now = AcademyClock.WallNow;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (!days.Contains((int)day.DayOfWeek)) continue;
            var s = DateTime.SpecifyKind(day + start, DateTimeKind.Utc);
            if (s < now) { result.Skipped.Add(new() { Date = day, Reason = "Fecha pasada" }); continue; }
            if (existingDays.Contains(day)) { result.Skipped.Add(new() { Date = day, Reason = "El equipo ya tiene un partido ese día" }); continue; }

            var ev = NewMatch(academyId, team, s, DateTime.SpecifyKind(day + end, DateTimeKind.Utc));
            ev.CallTime = dto.CallMinutesBefore > 0 ? s.AddMinutes(-dto.CallMinutesBefore) : null;
            ev.Location = Clean(dto.Location) ?? Clean(team.Tournament.MainLocation);
            ev.HeadquarterId = dto.HeadquarterId;
            var prefix = Clean(dto.RoundPrefix);
            if (prefix != null) ev.Round = $"{prefix} {numbered + created.Count + 1}";
            ev.Title = MatchTitle(team, ev);
            created.Add(ev);
            if (created.Count > MaxGeneratedMatches)
                throw new InvalidOperationException($"Se generarían más de {MaxGeneratedMatches} partidos. Reduce el rango o los días.");
        }

        if (created.Count > 0)
        {
            _db.Events.AddRange(created);
            var roster = await ActiveRosterAsync(teamId);
            _db.EventCalls.AddRange(created.SelectMany(ev => roster.Select(sid => NewCall(academyId, ev.Id, sid))));
            await AddCallNoticesAsync(academyId, team, created, roster);
            _audit?.Add(academyId, "tournaments.match.generate", nameof(TournamentTeam), team.Id,
                new { team.Name, created = created.Count, skipped = result.Skipped.Count, from, to, days, dto.StartTime, dto.EndTime });
            await _db.SaveChangesAsync();
            await SyncSuspensionsAsync(team.Id);
        }
        result.Created = created.Count;
        return result;
    }

    private Event NewMatch(Guid academyId, TournamentTeam team, DateTime start, DateTime end) => new()
    {
        AcademyId = academyId,
        Type = EventType.TournamentMatch,
        TournamentId = team.TournamentId,
        TournamentTeamId = team.Id,
        StartTime = start,
        EndTime = end,
        CategoryId = team.CategoryIds.FirstOrDefault(),
        CategoryIds = team.CategoryIds.ToList(),
        TeacherId = team.HeadCoachId,
        MatchStatus = MatchStatus.Scheduled,
        Title = team.Tournament?.Name ?? "Partido de torneo",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static void ApplyMatch(Event ev, TournamentTeam? team, SaveTournamentMatchDto dto)
    {
        ev.CallTime = Wall(dto.CallTime);
        ev.Round = Clean(dto.Round);
        ev.OpponentTeam = Clean(dto.OpponentTeam);
        ev.Location = Clean(dto.Location);
        ev.HeadquarterId = dto.HeadquarterId;
        ev.TeacherId = dto.TeacherId ?? team?.HeadCoachId ?? ev.TeacherId;
        ev.Description = Clean(dto.Description);
        if (dto.MatchStatus.HasValue) ev.MatchStatus = (MatchStatus)dto.MatchStatus.Value;
        ev.GoalsFor = dto.GoalsFor;
        ev.GoalsAgainst = dto.GoalsAgainst;
        if (ev.GoalsFor.HasValue && ev.GoalsAgainst.HasValue && ev.MatchStatus is null or MatchStatus.Scheduled)
            ev.MatchStatus = MatchStatus.Played;
        if (team != null) ev.Title = MatchTitle(team, ev);
    }

    private static string MatchTitle(TournamentTeam team, Event ev)
    {
        var title = $"{team.Tournament?.Name ?? ev.Title} · {team.Name}{Vs(ev)}";
        return title.Length <= 200 ? title : title[..200];
    }

    private async Task ValidateMatchAsync(Guid academyId, SaveTournamentMatchDto dto)
    {
        if (dto.EndTime <= dto.StartTime) throw new InvalidOperationException("La hora de fin debe ser posterior a la de inicio.");
        if (dto.CallTime.HasValue && dto.CallTime > dto.StartTime) throw new InvalidOperationException("La citación debe ser antes del partido.");
        if (dto.HeadquarterId.HasValue && !await _db.Headquarters.AnyAsync(h => h.Id == dto.HeadquarterId && h.AcademyId == academyId))
            throw new InvalidOperationException("Sede no encontrada.");
        if (dto.TeacherId.HasValue && !await _db.Users.AnyAsync(u => u.Id == dto.TeacherId && u.AcademyId == academyId &&
                                                                     (u.Role == UserRole.Staff || u.Role == UserRole.AcademyAdmin)))
            throw new InvalidOperationException("El responsable del partido debe ser parte del personal.");
    }

    private Task<List<Guid>> ActiveRosterAsync(Guid teamId) =>
        _db.TournamentPlayers.AsNoTracking()
            .Where(p => p.TeamId == teamId && p.Student.IsActive)
            .Select(p => p.StudentId).ToListAsync();

    /// <summary>Convoca a un alumno a un partido puntual (refuerzo de otra categoría, por ejemplo).</summary>
    public async Task AddCallAsync(Guid academyId, Guid eventId, Guid studentId)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.AcademyId == academyId && e.Id == eventId)
                 ?? throw new KeyNotFoundException("Partido no encontrado.");
        if (ev.Type is not (EventType.TournamentMatch or EventType.FriendlyMatch))
            throw new InvalidOperationException("Solo se convoca a partidos.");
        var student = await _db.Students.FirstOrDefaultAsync(s => s.AcademyId == academyId && s.Id == studentId && s.IsActive)
                      ?? throw new InvalidOperationException("Alumno no encontrado o retirado.");
        if (await _db.EventCalls.AnyAsync(c => c.EventId == eventId && c.StudentId == studentId)) return;

        _db.EventCalls.Add(NewCall(academyId, eventId, studentId));
        if (ev.StartTime >= AcademyClock.WallNow)
        {
            var team = ev.TournamentTeamId.HasValue
                ? await _db.TournamentTeams.Include(t => t.Tournament).FirstOrDefaultAsync(t => t.Id == ev.TournamentTeamId.Value)
                : null;
            if (team != null) await AddCallNoticesAsync(academyId, team, new() { ev }, new() { studentId });
            else
                _db.FamilyNotices.Add(new FamilyNotice
                {
                    AcademyId = academyId, StudentId = studentId, EventId = eventId, Type = FamilyNoticeType.EventCall,
                    Title = $"Convocatoria de {student.FirstName}",
                    Body = $"{(ev.Title.Length <= 200 ? ev.Title : ev.Title[..200])} · {ev.StartTime:dd/MM HH:mm}. Confirma su asistencia en la app."
                });
        }
        _audit?.Add(academyId, "tournaments.match.call.add", nameof(Event), eventId, new { studentId });
        await _db.SaveChangesAsync();
        if (_compliance != null) await _compliance.OverrideIfSuspendedAsync(eventId, studentId);
        await SyncCallChargesAsync(academyId, eventId);
    }

    /// <summary>Quita la convocatoria de un partido. Si ya tiene asistencia marcada, se conserva el registro de asistencia.</summary>
    public async Task RemoveCallAsync(Guid academyId, Guid eventId, Guid studentId)
    {
        var call = await _db.EventCalls.FirstOrDefaultAsync(c => c.AcademyId == academyId && c.EventId == eventId && c.StudentId == studentId)
                   ?? throw new KeyNotFoundException("Convocatoria no encontrada.");
        _db.EventCalls.Remove(call);
        _audit?.Add(academyId, "tournaments.match.call.remove", nameof(Event), eventId, new { studentId });
        await _db.SaveChangesAsync();
        await SyncCallChargesAsync(academyId, eventId);
    }

    private async Task<int> RemoveUpcomingMatchesAsync(Guid academyId, System.Linq.Expressions.Expression<Func<Event, bool>> filter)
    {
        var matches = (await _db.Events.Where(e => e.AcademyId == academyId).Where(filter).ToListAsync()).Where(IsUpcoming).ToList();
        var ids = matches.Select(e => e.Id).ToList();
        var withMarks = (await _db.Attendances.Where(a => a.EventId != null && ids.Contains(a.EventId.Value))
            .Select(a => a.EventId!.Value).Distinct().ToListAsync()).ToHashSet();
        var now = DateTime.UtcNow;
        var removed = 0;
        foreach (var ev in matches.Where(e => !withMarks.Contains(e.Id)))
        {
            ev.IsDeleted = true;
            ev.DeletedAt = now;
            ev.IsActive = false;
            removed++;
        }
        return removed;
    }

    private async Task<List<TournamentMatchDto>> LoadMatchesAsync(Guid academyId, Guid tournamentId, List<TournamentTeam> teams, Guid? onlyEventId = null)
    {
        var q = _db.Events.AsNoTracking().Where(e => e.AcademyId == academyId && e.TournamentId == tournamentId);
        if (onlyEventId.HasValue) q = q.Where(e => e.Id == onlyEventId.Value);
        var events = await q.OrderBy(e => e.StartTime)
            .Select(e => new
            {
                e.Id, e.Title, e.StartTime, e.EndTime, e.CallTime, e.Round, e.OpponentTeam, e.Location,
                e.HeadquarterId, HeadquarterName = e.Headquarter != null ? e.Headquarter.Name : null,
                e.TeacherId, TeacherName = e.Teacher != null ? e.Teacher.FirstName + " " + e.Teacher.LastName : null,
                e.TournamentTeamId, e.MatchStatus, e.GoalsFor, e.GoalsAgainst, e.TimePending, e.CoachFee,
                e.ReferenceCategoryId, e.RefereeWaived, e.RefereeWaivedNote
            })
            .ToListAsync();
        var ids = events.Select(e => e.Id).ToList();
        var calls = await _db.EventCalls.AsNoTracking().Where(c => ids.Contains(c.EventId))
            .GroupBy(c => c.EventId)
            .Select(g => new { g.Key, Total = g.Count(), Yes = g.Count(c => c.IsConfirmed == true), No = g.Count(c => c.IsConfirmed == false) })
            .ToDictionaryAsync(x => x.Key);
        var teamMap = teams.ToDictionary(t => t.Id);
        var refIds = events.Where(e => e.ReferenceCategoryId.HasValue).Select(e => e.ReferenceCategoryId!.Value).Distinct().ToList();
        var refs = await _db.Categories.AsNoTracking().Where(c => refIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, StartDateOfBirth = c.IsFree ? null : c.StartDateOfBirth, EndDateOfBirth = c.IsFree ? null : c.EndDateOfBirth }).ToDictionaryAsync(c => c.Id);
        var withRef = events.Where(e => e.ReferenceCategoryId.HasValue).Select(e => e.Id).ToList();
        var refCalls = await _db.EventCalls.AsNoTracking().Where(c => withRef.Contains(c.EventId))
            .Select(c => new { c.EventId, c.Student.CategoryId, c.Student.DateOfBirth, StartDateOfBirth = c.Student.Category.IsFree ? null : c.Student.Category.StartDateOfBirth, EndDateOfBirth = c.Student.Category.IsFree ? null : c.Student.Category.EndDateOfBirth })
            .ToListAsync();

        return events.Select(e =>
        {
            var team = e.TournamentTeamId.HasValue ? teamMap.GetValueOrDefault(e.TournamentTeamId.Value) : null;
            var c = calls.GetValueOrDefault(e.Id);
            return new TournamentMatchDto
            {
                Id = e.Id,
                TeamId = e.TournamentTeamId,
                TeamName = team?.Name,
                Title = e.Title,
                StartTime = e.StartTime,
                EndTime = e.EndTime,
                CallTime = e.CallTime,
                Round = e.Round,
                OpponentTeam = e.OpponentTeam,
                Location = e.Location,
                HeadquarterId = e.HeadquarterId,
                HeadquarterName = e.HeadquarterName,
                TeacherId = e.TeacherId,
                TeacherName = e.TeacherName?.Trim(),
                IsReplacement = team != null && e.TeacherId.HasValue && e.TeacherId != team.HeadCoachId,
                MatchStatus = (int)(e.MatchStatus ?? MatchStatus.Scheduled),
                GoalsFor = e.GoalsFor,
                GoalsAgainst = e.GoalsAgainst,
                CalledCount = c?.Total ?? 0,
                ConfirmedCount = c?.Yes ?? 0,
                DeclinedCount = c?.No ?? 0,
                TimePending = e.TimePending,
                CoachFee = e.CoachFee,
                NeedsDetails = e.TimePending || (e.TournamentTeamId != null && string.IsNullOrWhiteSpace(e.OpponentTeam)),
                ReferenceCategoryId = e.ReferenceCategoryId,
                ReferenceCategoryName = e.ReferenceCategoryId.HasValue ? refs.GetValueOrDefault(e.ReferenceCategoryId.Value)?.Name : null,
                SuperiorCount = e.ReferenceCategoryId.HasValue && refs.TryGetValue(e.ReferenceCategoryId.Value, out var rc)
                    ? refCalls.Count(x => x.EventId == e.Id && x.CategoryId != rc.Id &&
                                          IsSuperior(x.DateOfBirth, OldestBirth(x.StartDateOfBirth, x.EndDateOfBirth), OldestBirth(rc.StartDateOfBirth, rc.EndDateOfBirth)))
                    : 0,
                RefereeWaived = e.RefereeWaived,
                RefereeWaivedNote = e.RefereeWaivedNote,
            };
        }).ToList();
    }
}
