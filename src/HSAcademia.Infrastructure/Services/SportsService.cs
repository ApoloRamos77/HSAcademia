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
    private const int MaxConflictsShown = 4;
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
    private sealed record Slot(DateTime StartTime, DateTime EndTime, Guid? TeacherId, Guid? HeadquarterId, Guid? CategoryId, List<Guid>? CategoryIds);

    /// <summary>Valida los datos comunes de una serie y devuelve las horas y categorías ya interpretadas.</summary>
    private async Task<(TimeSpan Start, TimeSpan End, List<Guid> CategoryIds)> ValidateScheduleAsync(
        Guid academyId, string title, List<int> daysOfWeek, string startTime, string endTime, Guid? headquarterId, List<Guid> categories, Guid? teacherId)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new Exception("El título es obligatorio.");
        if (daysOfWeek.Count == 0 || daysOfWeek.Any(d => d < 0 || d > 6))
            throw new Exception("Selecciona al menos un día de la semana válido.");
        if (!TimeSpan.TryParseExact(startTime, @"hh\:mm", CultureInfo.InvariantCulture, out var start) ||
            !TimeSpan.TryParseExact(endTime, @"hh\:mm", CultureInfo.InvariantCulture, out var end))
            throw new Exception("Las horas deben tener formato HH:mm.");
        if (end <= start) throw new Exception("La hora de fin debe ser posterior a la de inicio.");

        if (headquarterId.HasValue && !await _db.Headquarters.AnyAsync(h => h.Id == headquarterId && h.AcademyId == academyId))
            throw new Exception("La sede seleccionada no es válida.");
        var categoryIds = categories.Distinct().ToList();
        if (categoryIds.Count > 0 &&
            await _db.Categories.CountAsync(c => c.AcademyId == academyId && categoryIds.Contains(c.Id)) != categoryIds.Count)
            throw new Exception("Alguna de las categorías seleccionadas no es válida.");
        if (teacherId.HasValue && !await _db.Users.AnyAsync(u => u.Id == teacherId && u.AcademyId == academyId &&
                (u.Role == UserRole.Staff || u.Role == UserRole.AcademyAdmin)))
            throw new Exception("El entrenador seleccionado no es válido.");
        return (start, end, categoryIds);
    }

    private Task<List<Slot>> LoadSlotsAsync(Guid academyId, DateTime from, DateTime to, IReadOnlyCollection<Guid>? excluding = null)
    {
        var rangeStart = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        var rangeEnd = DateTime.SpecifyKind(to.AddDays(1), DateTimeKind.Utc);
        var excluded = excluding ?? Array.Empty<Guid>();
        return _db.Events.AsNoTracking()
            .Where(e => e.AcademyId == academyId && e.StartTime < rangeEnd && e.EndTime > rangeStart && !excluded.Contains(e.Id))
            .Select(e => new Slot(e.StartTime, e.EndTime, e.TeacherId, e.HeadquarterId, e.CategoryId, e.CategoryIds))
            .ToListAsync();
    }

    /// <summary>Motivo por el que una sesión choca con otras; null si el horario está libre.</summary>
    private static string? ConflictReason(List<Slot> existing, DateTime start, DateTime end, Guid? teacherId, Guid? headquarterId, List<Guid> categoryIds)
    {
        var overlapping = existing.Where(x => x.StartTime < end && x.EndTime > start).ToList();
        if (teacherId.HasValue && overlapping.Any(x => x.TeacherId == teacherId))
            return "El entrenador ya tiene otro evento en ese horario";
        if (headquarterId.HasValue && categoryIds.Count > 0 && overlapping.Any(x =>
                x.HeadquarterId == headquarterId &&
                ((x.CategoryId.HasValue && categoryIds.Contains(x.CategoryId.Value)) ||
                 (x.CategoryIds != null && x.CategoryIds.Intersect(categoryIds).Any()))))
            return "La categoría ya tiene un evento en esa sede y horario";
        return null;
    }

    public async Task<RecurringEventsResultDto> CreateRecurringAsync(Guid academyId, CreateRecurringEventsDto dto, Guid? seriesId = null)
    {
        var (start, end, categoryIds) = await ValidateScheduleAsync(academyId, dto.Title, dto.DaysOfWeek, dto.StartTime, dto.EndTime, dto.HeadquarterId, dto.CategoryIds, dto.TeacherId);

        var from = dto.FromDate.Date;
        var to = dto.ToDate.Date;
        if (to < from) throw new Exception("La fecha final debe ser posterior a la inicial.");
        if ((to - from).TotalDays > 190) throw new Exception("El rango máximo de una serie es de 6 meses.");

        // Eventos existentes del rango para validar choques en memoria (una sola consulta)
        var existing = await LoadSlotsAsync(academyId, from, to);

        var result = new RecurringEventsResultDto { SeriesId = seriesId ?? Guid.NewGuid() };
        var toCreate = new List<Event>();
        var today = AcademyClock.Today;

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (!dto.DaysOfWeek.Contains((int)day.DayOfWeek)) continue;

            // Convención del calendario: hora de pared local guardada como UTC.
            var s = DateTime.SpecifyKind(day + start, DateTimeKind.Utc);
            var e = DateTime.SpecifyKind(day + end, DateTimeKind.Utc);

            if (day < today) { result.Skipped.Add(new() { Date = day, Reason = "Fecha pasada" }); continue; }

            var conflict = ConflictReason(existing, s, e, dto.TeacherId, dto.HeadquarterId, categoryIds);
            if (conflict != null) { result.Skipped.Add(new() { Date = day, Reason = conflict }); continue; }

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

    /// <summary>
    /// Aplica los cambios a las sesiones futuras sin asistencia de la serie. Si cambian los días, agrega o retira sesiones
    /// dentro del mismo rango. Si alguna fecha choca con otro evento no se aplica nada.
    /// </summary>
    public async Task<SeriesUpdateResultDto> UpdateSeriesAsync(Guid academyId, Guid seriesId, UpdateSeriesDto dto)
    {
        var scope = (dto.Scope ?? "").Trim().ToLowerInvariant();
        if (scope is not ("following" or "all")) throw new Exception("Indica a qué sesiones se aplica el cambio.");
        var (start, end, categoryIds) = await ValidateScheduleAsync(academyId, dto.Title, dto.DaysOfWeek, dto.StartTime, dto.EndTime, dto.HeadquarterId, dto.CategoryIds, dto.TeacherId);

        var series = await _db.Events.Where(e => e.AcademyId == academyId && e.SeriesId == seriesId && !e.IsDeleted)
            .OrderBy(e => e.StartTime).ToListAsync();
        if (series.Count == 0) throw new KeyNotFoundException("Serie no encontrada.");
        var fromDate = scope == "all"
            ? series[0].StartTime.Date
            : (series.FirstOrDefault(e => e.Id == dto.FromEventId) ?? throw new KeyNotFoundException("Sesión no encontrada en la serie.")).StartTime.Date;

        var seriesIds = series.Select(e => (Guid?)e.Id).ToList();
        var taken = (await _db.Attendances.Where(a => seriesIds.Contains(a.EventId)).Select(a => a.EventId)
            .Concat(_db.StaffAttendances.Where(a => seriesIds.Contains(a.EventId)).Select(a => a.EventId))
            .Distinct().ToListAsync()).ToHashSet();

        var now = AcademyClock.WallNow;
        var inRange = series.Where(e => e.StartTime.Date >= fromDate).ToList();
        var editable = inRange.Where(e => e.StartTime > now && !e.AttendanceClosed && !taken.Contains(e.Id)).ToList();
        if (editable.Count == 0) throw new InvalidOperationException("No hay sesiones futuras sin asistencia para modificar.");

        var protectedDates = inRange.Except(editable).Select(e => e.StartTime.Date).ToHashSet();
        var first = editable[0].StartTime.Date;
        var last = editable[^1].StartTime.Date;
        var desired = new List<(DateTime Start, DateTime End)>();
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            var startAt = DateTime.SpecifyKind(day + start, DateTimeKind.Utc);
            if (dto.DaysOfWeek.Contains((int)day.DayOfWeek) && !protectedDates.Contains(day) && startAt > now)
                desired.Add((startAt, DateTime.SpecifyKind(day + end, DateTimeKind.Utc)));
        }
        if (desired.Count == 0) throw new InvalidOperationException("Con esos días no queda ninguna sesión dentro de la serie.");

        var others = await LoadSlotsAsync(academyId, first, last, editable.Select(e => e.Id).ToList());
        var conflicts = desired
            .Select(d => (d.Start, Reason: ConflictReason(others, d.Start, d.End, dto.TeacherId, dto.HeadquarterId, categoryIds)))
            .Where(c => c.Reason != null)
            .ToList();
        if (conflicts.Count > 0)
        {
            var detail = string.Join("; ", conflicts.Take(MaxConflictsShown).Select(c => $"{c.Start:dd/MM}: {c.Reason}"));
            var more = conflicts.Count > MaxConflictsShown ? $" y {conflicts.Count - MaxConflictsShown} más" : "";
            throw new InvalidOperationException($"No se aplicó ningún cambio. Choques de horario: {detail}{more}.");
        }

        var result = new SeriesUpdateResultDto { Protected = inRange.Count - editable.Count };
        var utcNow = DateTime.UtcNow;
        var byDate = editable.GroupBy(e => e.StartTime.Date).ToDictionary(g => g.Key, g => g.First());
        var desiredDates = desired.Select(d => d.Start.Date).ToHashSet();
        foreach (var removed in editable.Where(e => !desiredDates.Contains(e.StartTime.Date) || byDate[e.StartTime.Date] != e))
        {
            removed.IsDeleted = true;
            removed.DeletedAt = utcNow;
            result.Removed++;
        }
        foreach (var (startAt, endAt) in desired)
        {
            if (!byDate.TryGetValue(startAt.Date, out var ev))
            {
                ev = new Event { AcademyId = academyId, Type = EventType.Training, SeriesId = seriesId, CreatedAt = utcNow };
                _db.Events.Add(ev);
                result.Created++;
            }
            else result.Updated++;
            ev.Title = dto.Title.Trim();
            ev.Description = dto.Description;
            ev.StartTime = startAt;
            ev.EndTime = endAt;
            ev.HeadquarterId = dto.HeadquarterId;
            ev.CategoryId = categoryIds.Count > 0 ? categoryIds[0] : null;
            ev.CategoryIds = categoryIds;
            ev.TeacherId = dto.TeacherId;
            ev.UpdatedAt = utcNow;
        }

        _audit.Add(academyId, "calendar.series.update", nameof(Event), seriesId,
            new { scope, dto.Title, dto.DaysOfWeek, dto.StartTime, dto.EndTime, result.Updated, result.Created, result.Removed, result.Protected });
        await _db.SaveChangesAsync();
        return result;
    }

    /// <summary>Crea, dentro de la misma serie, el mes siguiente a su última sesión con los días y datos de su última semana.</summary>
    public async Task<RecurringEventsResultDto> RepeatNextMonthAsync(Guid academyId, Guid seriesId)
    {
        var series = await _db.Events.AsNoTracking()
            .Where(e => e.AcademyId == academyId && e.SeriesId == seriesId && !e.IsDeleted)
            .OrderBy(e => e.StartTime).ToListAsync();
        if (series.Count == 0) throw new KeyNotFoundException("Serie no encontrada.");

        var last = series[^1];
        var lastMonth = new DateTime(last.StartTime.Year, last.StartTime.Month, 1);
        var nextMonth = lastMonth.AddMonths(1);
        // La última semana refleja el horario vigente: sesiones antiguas conservan días u horas ya cambiados.
        var weekStart = last.StartTime.AddDays(-7);
        var days = series.Where(e => e.StartTime > weekStart && e.StartTime.TimeOfDay == last.StartTime.TimeOfDay)
            .Select(e => (int)e.StartTime.DayOfWeek).Distinct().ToList();

        return await CreateRecurringAsync(academyId, new CreateRecurringEventsDto
        {
            Title = last.Title,
            Description = last.Description,
            HeadquarterId = last.HeadquarterId,
            CategoryIds = last.AllCategoryIds(),
            TeacherId = last.TeacherId,
            DaysOfWeek = days,
            StartTime = last.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            EndTime = last.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            FromDate = nextMonth,
            ToDate = nextMonth.AddMonths(1).AddDays(-1)
        }, seriesId);
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
