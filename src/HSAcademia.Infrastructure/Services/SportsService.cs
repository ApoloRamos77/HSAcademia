using System.Globalization;
using HSAcademia.Application.DTOs.Calendar;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>Planificación (horarios recurrentes) y competencia (resultados y estadísticas por jugador).</summary>
public class SportsService
{
    private const int MaxEventsPerSeries = 300;
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public SportsService(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    // ═════════════════════════════════════════════════════════════
    // HORARIOS RECURRENTES
    // ═════════════════════════════════════════════════════════════
    public async Task<RecurringEventsResultDto> CreateRecurringAsync(Guid academyId, CreateRecurringEventsDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) throw new Exception("El título es obligatorio.");
        if (dto.DaysOfWeek.Count == 0 || dto.DaysOfWeek.Any(d => d < 0 || d > 6))
            throw new Exception("Selecciona al menos un día de la semana válido.");
        if (!TimeSpan.TryParseExact(dto.StartTime, @"hh\:mm", CultureInfo.InvariantCulture, out var start) ||
            !TimeSpan.TryParseExact(dto.EndTime, @"hh\:mm", CultureInfo.InvariantCulture, out var end))
            throw new Exception("Las horas deben tener formato HH:mm.");
        if (end <= start) throw new Exception("La hora de fin debe ser posterior a la de inicio.");

        var from = dto.FromDate.Date;
        var to = dto.ToDate.Date;
        if (to < from) throw new Exception("La fecha final debe ser posterior a la inicial.");
        if ((to - from).TotalDays > 190) throw new Exception("El rango máximo de una serie es de 6 meses.");

        if (dto.HeadquarterId.HasValue && !await _db.Headquarters.AnyAsync(h => h.Id == dto.HeadquarterId && h.AcademyId == academyId))
            throw new Exception("La sede seleccionada no es válida.");
        var categoryIds = dto.CategoryIds.Distinct().ToList();
        if (categoryIds.Count > 0 &&
            await _db.Categories.CountAsync(c => c.AcademyId == academyId && categoryIds.Contains(c.Id)) != categoryIds.Count)
            throw new Exception("Alguna de las categorías seleccionadas no es válida.");
        if (dto.TeacherId.HasValue && !await _db.Users.AnyAsync(u => u.Id == dto.TeacherId && u.AcademyId == academyId &&
                (u.Role == UserRole.Staff || u.Role == UserRole.AcademyAdmin)))
            throw new Exception("El entrenador seleccionado no es válido.");

        // Eventos existentes del rango para validar choques en memoria (una sola consulta)
        var rangeStart = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        var rangeEnd = DateTime.SpecifyKind(to.AddDays(1), DateTimeKind.Utc);
        var existing = await _db.Events.AsNoTracking()
            .Where(e => e.AcademyId == academyId && e.StartTime < rangeEnd && e.EndTime > rangeStart)
            .Select(e => new { e.StartTime, e.EndTime, e.TeacherId, e.HeadquarterId, e.CategoryId, e.CategoryIds })
            .ToListAsync();

        var result = new RecurringEventsResultDto { SeriesId = Guid.NewGuid() };
        var toCreate = new List<Event>();
        var today = AcademyClock.Today;

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (!dto.DaysOfWeek.Contains((int)day.DayOfWeek)) continue;

            // Convención del calendario: hora de pared local guardada como UTC.
            var s = DateTime.SpecifyKind(day + start, DateTimeKind.Utc);
            var e = DateTime.SpecifyKind(day + end, DateTimeKind.Utc);

            if (day < today) { result.Skipped.Add(new() { Date = day, Reason = "Fecha pasada" }); continue; }

            var overlapping = existing.Where(x => x.StartTime < e && x.EndTime > s).ToList();
            if (dto.TeacherId.HasValue && overlapping.Any(x => x.TeacherId == dto.TeacherId))
            { result.Skipped.Add(new() { Date = day, Reason = "El entrenador ya tiene otro evento en ese horario" }); continue; }
            if (dto.HeadquarterId.HasValue && categoryIds.Count > 0 && overlapping.Any(x =>
                    x.HeadquarterId == dto.HeadquarterId &&
                    ((x.CategoryId.HasValue && categoryIds.Contains(x.CategoryId.Value)) ||
                     (x.CategoryIds != null && x.CategoryIds.Intersect(categoryIds).Any()))))
            { result.Skipped.Add(new() { Date = day, Reason = "La categoría ya tiene un evento en esa sede y horario" }); continue; }

            toCreate.Add(new Event
            {
                AcademyId = academyId,
                Title = dto.Title.Trim(),
                Description = dto.Description,
                Type = EventType.Training,
                StartTime = s,
                EndTime = e,
                HeadquarterId = dto.HeadquarterId,
                CategoryId = categoryIds.Count > 0 ? categoryIds[0] : null,
                CategoryIds = categoryIds,
                TeacherId = dto.TeacherId,
                SeriesId = result.SeriesId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            if (toCreate.Count > MaxEventsPerSeries)
                throw new Exception($"La serie generaría más de {MaxEventsPerSeries} eventos. Reduce el rango o los días.");
        }

        if (toCreate.Count > 0)
        {
            _db.Events.AddRange(toCreate);
            _audit.Add(academyId, "calendar.series.create", nameof(Event), result.SeriesId,
                new { dto.Title, created = toCreate.Count, skipped = result.Skipped.Count, from, to, dto.DaysOfWeek, dto.StartTime, dto.EndTime });
            await _db.SaveChangesAsync();
        }
        result.Created = toCreate.Count;
        return result;
    }

    /// <summary>Elimina los eventos FUTUROS de la serie que aún no tienen asistencia registrada.</summary>
    public async Task<int> DeleteFutureSeriesAsync(Guid academyId, Guid seriesId)
    {
        var now = AcademyClock.WallNow;
        var withAttendance = _db.Attendances.Where(a => a.EventId != null).Select(a => a.EventId);
        var events = await _db.Events
            .Where(e => e.AcademyId == academyId && e.SeriesId == seriesId && e.StartTime >= now && !withAttendance.Contains(e.Id))
            .ToListAsync();
        foreach (var ev in events)
        {
            ev.IsDeleted = true;
            ev.DeletedAt = DateTime.UtcNow;
        }
        if (events.Count > 0)
        {
            _audit.Add(academyId, "calendar.series.delete", nameof(Event), seriesId, new { deleted = events.Count });
            await _db.SaveChangesAsync();
        }
        return events.Count;
    }

    // ═════════════════════════════════════════════════════════════
    // PARTIDOS: resultado y estadísticas por jugador
    // ═════════════════════════════════════════════════════════════
    private async Task<Event> GetMatchAsync(Guid academyId, Guid eventId)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId && e.AcademyId == academyId)
            ?? throw new KeyNotFoundException("Evento no encontrado.");
        if (ev.Type != EventType.FriendlyMatch && ev.Type != EventType.TournamentMatch)
            throw new Exception("Solo los partidos (amistosos o de torneo) tienen resultado y estadísticas.");
        return ev;
    }

    public async Task<MatchResultDto> GetMatchResultAsync(Guid academyId, Guid eventId)
    {
        var ev = await GetMatchAsync(academyId, eventId);

        var stats = await _db.MatchPlayerStats.AsNoTracking()
            .Where(s => s.EventId == eventId).ToDictionaryAsync(s => s.StudentId);
        var calledIds = await _db.EventCalls.AsNoTracking()
            .Where(c => c.EventId == eventId && c.IsConfirmed != false).Select(c => c.StudentId).ToListAsync();

        // Plantel: convocados + quienes ya tienen estadística; si no hay convocatoria, alumnos activos de las categorías.
        var categoryIds = new List<Guid>();
        if (ev.CategoryId.HasValue) categoryIds.Add(ev.CategoryId.Value);
        if (ev.CategoryIds != null) categoryIds.AddRange(ev.CategoryIds);

        var ids = calledIds.Concat(stats.Keys).Distinct().ToList();
        var studentsQuery = _db.Students.AsNoTracking().Where(s => s.AcademyId == academyId);
        studentsQuery = ids.Count > 0
            ? studentsQuery.Where(s => ids.Contains(s.Id))
            : studentsQuery.Where(s => s.IsActive && categoryIds.Contains(s.CategoryId));

        var players = await studentsQuery
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName, Category = s.Category.Name })
            .ToListAsync();

        return new MatchResultDto
        {
            EventId = ev.Id,
            Title = ev.Title,
            Opponent = ev.OpponentTeam,
            StartTime = ev.StartTime,
            GoalsFor = ev.GoalsFor,
            GoalsAgainst = ev.GoalsAgainst,
            MatchNotes = ev.MatchNotes,
            Players = players.Select(p =>
            {
                stats.TryGetValue(p.Id, out var st);
                return new PlayerStatDto
                {
                    StudentId = p.Id,
                    StudentName = p.Name,
                    Category = p.Category,
                    Called = calledIds.Contains(p.Id) || st != null,
                    Played = st != null,
                    IsStarter = st?.IsStarter ?? false,
                    Minutes = st?.Minutes ?? 0,
                    Goals = st?.Goals ?? 0,
                    Assists = st?.Assists ?? 0,
                    YellowCards = st?.YellowCards ?? 0,
                    RedCards = st?.RedCards ?? 0,
                    Rating = st?.Rating,
                    Notes = st?.Notes
                };
            }).ToList()
        };
    }

    public async Task<MatchResultDto> SaveMatchResultAsync(Guid academyId, Guid eventId, SaveMatchResultDto dto)
    {
        var ev = await GetMatchAsync(academyId, eventId);

        if (dto.GoalsFor is < 0 || dto.GoalsAgainst is < 0) throw new Exception("El marcador no puede ser negativo.");
        foreach (var p in dto.Players)
        {
            if (p.Minutes is < 0 or > 200 || p.Goals < 0 || p.Assists < 0 || p.YellowCards is < 0 or > 2 || p.RedCards is < 0 or > 1)
                throw new Exception("Revisa las estadísticas: hay valores fuera de rango.");
            if (p.Rating is < 1 or > 10) throw new Exception("La calificación debe estar entre 1 y 10.");
        }
        var playerIds = dto.Players.Select(p => p.StudentId).Distinct().ToList();
        if (playerIds.Count != dto.Players.Count) throw new Exception("Hay jugadores repetidos.");
        if (playerIds.Count > 0 &&
            await _db.Students.CountAsync(s => s.AcademyId == academyId && playerIds.Contains(s.Id)) != playerIds.Count)
            throw new Exception("Algún jugador no pertenece a la academia.");
        if (dto.GoalsFor.HasValue && dto.Players.Sum(p => p.Goals) > dto.GoalsFor.Value)
            throw new Exception("La suma de goles de los jugadores supera los goles a favor del marcador.");

        ev.GoalsFor = dto.GoalsFor;
        ev.GoalsAgainst = dto.GoalsAgainst;
        ev.MatchNotes = dto.MatchNotes?.Trim();
        ev.UpdatedAt = DateTime.UtcNow;

        var existing = await _db.MatchPlayerStats.Where(s => s.EventId == eventId).ToListAsync();
        _db.MatchPlayerStats.RemoveRange(existing.Where(s => !playerIds.Contains(s.StudentId)));
        foreach (var p in dto.Players)
        {
            var st = existing.FirstOrDefault(s => s.StudentId == p.StudentId);
            if (st == null)
            {
                st = new MatchPlayerStat { AcademyId = academyId, EventId = eventId, StudentId = p.StudentId };
                _db.MatchPlayerStats.Add(st);
            }
            st.IsStarter = p.IsStarter;
            st.Minutes = p.Minutes;
            st.Goals = p.Goals;
            st.Assists = p.Assists;
            st.YellowCards = p.YellowCards;
            st.RedCards = p.RedCards;
            st.Rating = p.Rating;
            st.Notes = p.Notes?.Trim();
            st.UpdatedAt = DateTime.UtcNow;
        }

        _audit.Add(academyId, "calendar.match.result", nameof(Event), eventId,
            new { dto.GoalsFor, dto.GoalsAgainst, players = dto.Players.Count });
        await _db.SaveChangesAsync();
        return await GetMatchResultAsync(academyId, eventId);
    }

    public async Task<StudentSportsSummaryDto> GetStudentSummaryAsync(Guid academyId, Guid studentId)
    {
        var lines = await _db.MatchPlayerStats.AsNoTracking()
            .Where(s => s.AcademyId == academyId && s.StudentId == studentId)
            .OrderByDescending(s => s.Event.StartTime)
            .Select(s => new
            {
                s.EventId, s.Event.StartTime, s.Event.Title, s.Event.OpponentTeam, s.Event.GoalsFor, s.Event.GoalsAgainst,
                s.IsStarter, s.Minutes, s.Goals, s.Assists, s.YellowCards, s.RedCards, s.Rating
            })
            .ToListAsync();

        var rated = lines.Where(l => l.Rating.HasValue).ToList();
        return new StudentSportsSummaryDto
        {
            Matches = lines.Count,
            Starts = lines.Count(l => l.IsStarter),
            Minutes = lines.Sum(l => l.Minutes),
            Goals = lines.Sum(l => l.Goals),
            Assists = lines.Sum(l => l.Assists),
            YellowCards = lines.Sum(l => l.YellowCards),
            RedCards = lines.Sum(l => l.RedCards),
            AverageRating = rated.Count > 0 ? Math.Round((decimal)rated.Average(l => l.Rating!.Value), 1) : null,
            RecentMatches = lines.Take(10).Select(l => new StudentMatchLineDto
            {
                EventId = l.EventId,
                Date = l.StartTime,
                Title = l.Title,
                Opponent = l.OpponentTeam,
                Score = l.GoalsFor.HasValue && l.GoalsAgainst.HasValue ? $"{l.GoalsFor}-{l.GoalsAgainst}" : null,
                Minutes = l.Minutes,
                Goals = l.Goals,
                Assists = l.Assists,
                Rating = l.Rating
            }).ToList()
        };
    }
}
