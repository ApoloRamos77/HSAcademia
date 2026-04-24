using HSAcademia.Application.DTOs.Calendar;
using HSAcademia.Application.Interfaces;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

public class CalendarService : ICalendarService
{
    private readonly AppDbContext _db;

    public CalendarService(AppDbContext db)
    {
        _db = db;
    }

    // =========================================================
    // GET EVENTS FOR MONTH  (real + virtual birthdays)
    // =========================================================
    public async Task<List<EventDto>> GetEventsForMonthAsync(
        Guid academyId, int year, int month,
        Guid? headquarterId = null, Guid? categoryId = null, int? eventType = null)
    {
        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = from.AddMonths(1);

        // 1. Real events from DB
        var query = _db.Events
            .Include(e => e.Headquarter)
            .Include(e => e.Category)
            .Include(e => e.Teacher)
            .Include(e => e.Tournament)
            .Where(e => e.AcademyId == academyId
                     && e.StartTime >= from
                     && e.StartTime < to);

        if (headquarterId.HasValue)
            query = query.Where(e => e.HeadquarterId == headquarterId);
        if (categoryId.HasValue)
            query = query.Where(e => e.CategoryId == categoryId);
        if (eventType.HasValue)
            query = query.Where(e => (int)e.Type == eventType.Value);

        var dbEvents = await query.OrderBy(e => e.StartTime).ToListAsync();
        var result = dbEvents.Select(MapToDto).ToList();

        // 2. Virtual birthday events (only when no eventType filter OR filter = Birthday)
        bool includeBirthdays = !eventType.HasValue || eventType.Value == (int)EventType.Birthday;
        if (includeBirthdays)
        {
            var birthdays = await BuildBirthdayEventsAsync(academyId, year, month);
            result.AddRange(birthdays);
        }

        return result.OrderBy(e => e.StartTime).ToList();
    }

    // =========================================================
    // GET SINGLE EVENT
    // =========================================================
    public async Task<EventDto?> GetEventByIdAsync(Guid academyId, Guid eventId)
    {
        var ev = await _db.Events
            .Include(e => e.Headquarter)
            .Include(e => e.Category)
            .Include(e => e.Teacher)
            .Include(e => e.Tournament)
            .FirstOrDefaultAsync(e => e.AcademyId == academyId && e.Id == eventId);

        return ev is null ? null : MapToDto(ev);
    }

    // =========================================================
    // CREATE EVENT  (with conflict detection)
    // =========================================================
    public async Task<EventDto> CreateEventAsync(Guid academyId, CreateEventDto dto)
    {
        if (dto.EndTime <= dto.StartTime)
            throw new InvalidOperationException("La hora de fin debe ser posterior a la hora de inicio.");

        // --- Conflict: Teacher ---
        if (dto.TeacherId.HasValue)
        {
            bool teacherConflict = await _db.Events.AnyAsync(e =>
                e.AcademyId == academyId &&
                e.TeacherId == dto.TeacherId &&
                e.StartTime < dto.EndTime &&
                e.EndTime > dto.StartTime);

            if (teacherConflict)
                throw new InvalidOperationException(
                    "El docente/entrenador ya tiene un evento asignado en ese bloque horario.");
        }

        // --- Conflict: Headquarter + Category ---
        if (dto.HeadquarterId.HasValue && dto.CategoryId.HasValue)
        {
            bool spaceConflict = await _db.Events.AnyAsync(e =>
                e.AcademyId == academyId &&
                e.HeadquarterId == dto.HeadquarterId &&
                e.CategoryId == dto.CategoryId &&
                e.StartTime < dto.EndTime &&
                e.EndTime > dto.StartTime);

            if (spaceConflict)
                throw new InvalidOperationException(
                    "La categoría ya tiene un evento programado en esa sede y bloque horario.");
        }

        var entity = new Event
        {
            AcademyId     = academyId,
            Title         = dto.Title,
            Description   = dto.Description,
            Type          = dto.Type,
            StartTime     = dto.StartTime.ToUniversalTime(),
            EndTime       = dto.EndTime.ToUniversalTime(),
            HeadquarterId = dto.HeadquarterId,
            CategoryId    = dto.CategoryId,
            TeacherId     = dto.TeacherId,
            TournamentId  = dto.TournamentId,
            OpponentTeam  = dto.OpponentTeam,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow
        };

        _db.Events.Add(entity);
        await _db.SaveChangesAsync();

        return await GetEventByIdAsync(academyId, entity.Id)
               ?? throw new Exception("Error al recuperar el evento creado.");
    }

    // =========================================================
    // DELETE EVENT
    // =========================================================
    public async Task DeleteEventAsync(Guid academyId, Guid eventId)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.AcademyId == academyId && e.Id == eventId)
                 ?? throw new KeyNotFoundException("Evento no encontrado.");

        ev.IsDeleted  = true;
        ev.DeletedAt  = DateTime.UtcNow;
        ev.IsActive   = false;
        await _db.SaveChangesAsync();
    }

    // =========================================================
    // TOURNAMENTS
    // =========================================================
    public async Task<List<TournamentDto>> GetTournamentsAsync(Guid academyId)
    {
        return await _db.Tournaments
            .Where(t => t.AcademyId == academyId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TournamentDto
            {
                Id           = t.Id,
                Name         = t.Name,
                Organizer    = t.Organizer,
                MainLocation = t.MainLocation,
                CreatedAt    = t.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<TournamentDto> CreateTournamentAsync(Guid academyId, CreateTournamentDto dto)
    {
        var entity = new Tournament
        {
            AcademyId    = academyId,
            Name         = dto.Name,
            Organizer    = dto.Organizer,
            MainLocation = dto.MainLocation,
            CreatedAt    = DateTime.UtcNow,
            UpdatedAt    = DateTime.UtcNow
        };
        _db.Tournaments.Add(entity);
        await _db.SaveChangesAsync();

        return new TournamentDto
        {
            Id           = entity.Id,
            Name         = entity.Name,
            Organizer    = entity.Organizer,
            MainLocation = entity.MainLocation,
            CreatedAt    = entity.CreatedAt
        };
    }

    public async Task DeleteTournamentAsync(Guid academyId, Guid tournamentId)
    {
        var t = await _db.Tournaments.FirstOrDefaultAsync(t => t.AcademyId == academyId && t.Id == tournamentId)
                ?? throw new KeyNotFoundException("Torneo no encontrado.");
        t.IsDeleted = true;
        t.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // =========================================================
    // PRIVATE HELPERS
    // =========================================================
    private static EventDto MapToDto(Event e) => new()
    {
        Id             = e.Id,
        Title          = e.Title,
        Description    = e.Description,
        Type           = e.Type,
        TypeLabel      = GetTypeLabel(e.Type),
        StartTime      = e.StartTime,
        EndTime        = e.EndTime,
        HeadquarterId  = e.HeadquarterId,
        HeadquarterName = e.Headquarter?.Name,
        CategoryId     = e.CategoryId,
        CategoryName   = e.Category?.Name,
        TeacherId      = e.TeacherId,
        TeacherName    = e.Teacher is null ? null : $"{e.Teacher.FirstName} {e.Teacher.LastName}",
        TournamentId   = e.TournamentId,
        TournamentName = e.Tournament?.Name,
        OpponentTeam   = e.OpponentTeam,
        IsVirtual      = false
    };

    private static string GetTypeLabel(EventType t) => t switch
    {
        EventType.Training       => "Entrenamiento",
        EventType.FriendlyMatch  => "Amistoso",
        EventType.TournamentMatch => "Torneo",
        EventType.Birthday       => "Cumpleaños",
        _                        => "Evento"
    };

    /// <summary>
    /// Generates in-memory birthday EventDtos for students and staff whose
    /// birthday falls in the requested month (projected to the current year).
    /// </summary>
    private async Task<List<EventDto>> BuildBirthdayEventsAsync(Guid academyId, int year, int month)
    {
        var birthdays = new List<EventDto>();

        // Students
        var students = await _db.Students
            .Where(s => s.AcademyId == academyId
                     && s.DateOfBirth.Month == month)
            .Select(s => new
            {
                s.Id,
                Name = s.FirstName + " " + s.LastName,
                BirthDate = (DateTime?)s.DateOfBirth
            })
            .ToListAsync();

        foreach (var s in students)
        {
            if (s.BirthDate is null) continue;
            var day = new DateTime(year, month, s.BirthDate.Value.Day, 0, 0, 0, DateTimeKind.Utc);
            birthdays.Add(new EventDto
            {
                Id        = Guid.NewGuid(), // virtual – not persisted
                Title     = $"🎂 Cumpleaños: {s.Name}",
                Type      = EventType.Birthday,
                TypeLabel = "Cumpleaños",
                StartTime = day,
                EndTime   = day.AddHours(1),
                IsVirtual = true
            });
        }

        // Staff / Users
        var staff = await _db.Users
            .Where(u => u.AcademyId == academyId
                     && u.BirthDate != null
                     && u.BirthDate.Value.Month == month)
            .Select(u => new
            {
                u.Id,
                Name = u.FirstName + " " + u.LastName,
                u.BirthDate
            })
            .ToListAsync();

        foreach (var u in staff)
        {
            if (u.BirthDate is null) continue;
            var day = new DateTime(year, month, u.BirthDate.Value.Day, 0, 0, 0, DateTimeKind.Utc);
            birthdays.Add(new EventDto
            {
                Id        = Guid.NewGuid(),
                Title     = $"🎂 Cumpleaños: {u.Name} (Staff)",
                Type      = EventType.Birthday,
                TypeLabel = "Cumpleaños",
                StartTime = day,
                EndTime   = day.AddHours(1),
                IsVirtual = true
            });
        }

        return birthdays;
    }
}
