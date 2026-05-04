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
        Guid? headquarterId = null, Guid? categoryId = null, int? eventType = null,
        Guid? userId = null, string userRole = "")
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
        if (eventType.HasValue)
            query = query.Where(e => (int)e.Type == eventType.Value);

        List<Guid>? staffCategoryIds = null;
        if (userRole == "Staff" && userId.HasValue)
        {
            var userWithCategories = await _db.Users
                .Include(u => u.AssignedCategories)
                .FirstOrDefaultAsync(u => u.Id == userId.Value);

            if (userWithCategories != null && userWithCategories.AssignedCategories.Any())
            {
                staffCategoryIds = userWithCategories.AssignedCategories.Select(c => c.Id).ToList();
            }
            else
            {
                // Si es staff pero no tiene categorias asignadas, no ve ningun evento
                return new List<EventDto>();
            }
        }

        var dbEvents = await query.OrderBy(e => e.StartTime).ToListAsync();

        // In-memory filter for Category (CategoryIds cannot be translated to SQL due to ValueConverter)
        if (categoryId.HasValue)
        {
            dbEvents = dbEvents.Where(e => 
                e.CategoryId == categoryId.Value || 
                (e.CategoryIds != null && e.CategoryIds.Contains(categoryId.Value))
            ).ToList();
        }

        if (staffCategoryIds != null)
        {
            dbEvents = dbEvents.Where(e => 
                (e.CategoryId.HasValue && staffCategoryIds.Contains(e.CategoryId.Value)) ||
                (e.CategoryIds != null && e.CategoryIds.Any(c => staffCategoryIds.Contains(c)))
            ).ToList();
        }

        var result = dbEvents.Select(MapToDto).ToList();

        // 2. Virtual birthday events (only when no eventType filter OR filter = Birthday)
        bool includeBirthdays = !eventType.HasValue || eventType.Value == (int)EventType.Birthday;
        if (includeBirthdays)
        {
            List<Guid>? assignedCategoryIds = null;
            if (userRole == "Staff" && userId.HasValue)
            {
                var userWithCategories = await _db.Users.Include(u => u.AssignedCategories).FirstOrDefaultAsync(u => u.Id == userId.Value);
                if (userWithCategories != null) assignedCategoryIds = userWithCategories.AssignedCategories.Select(c => c.Id).ToList();
            }

            var birthdays = await BuildBirthdayEventsAsync(academyId, year, month, assignedCategoryIds);
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
            CategoryIds   = dto.CategoryIds ?? new List<Guid>(),
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
    // UPDATE EVENT
    // =========================================================
    public async Task<EventDto> UpdateEventAsync(Guid academyId, Guid eventId, UpdateEventDto dto)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.AcademyId == academyId && e.Id == eventId && !e.IsDeleted)
                 ?? throw new KeyNotFoundException("Evento no encontrado.");

        if (dto.EndTime <= dto.StartTime)
            throw new InvalidOperationException("La hora de fin debe ser posterior a la hora de inicio.");

        ev.Title         = dto.Title;
        ev.Description   = dto.Description;
        ev.Type          = dto.Type;
        ev.StartTime     = dto.StartTime.ToUniversalTime();
        ev.EndTime       = dto.EndTime.ToUniversalTime();
        ev.HeadquarterId = dto.HeadquarterId;
        ev.CategoryId    = dto.CategoryId;
        ev.CategoryIds   = dto.CategoryIds ?? new List<Guid>();
        ev.TeacherId     = dto.TeacherId;
        ev.TournamentId  = dto.TournamentId;
        ev.OpponentTeam  = dto.OpponentTeam;
        ev.UpdatedAt     = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return await GetEventByIdAsync(academyId, eventId)
               ?? throw new Exception("Error al recuperar el evento actualizado.");
    }

    // =========================================================
    // BULK SHIFT DAYS (timezone correction)
    // =========================================================
    public async Task<int> BulkShiftDaysAsync(Guid academyId, int days)
    {
        var events = await _db.Events
            .Where(e => e.AcademyId == academyId && !e.IsDeleted)
            .ToListAsync();

        foreach (var ev in events)
        {
            ev.StartTime = ev.StartTime.AddDays(days);
            ev.EndTime   = ev.EndTime.AddDays(days);
            ev.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return events.Count;
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
        CategoryIds    = e.CategoryIds ?? new List<Guid>(),
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
    private async Task<List<EventDto>> BuildBirthdayEventsAsync(Guid academyId, int year, int month, List<Guid>? assignedCategoryIds = null)
    {
        var birthdays = new List<EventDto>();

        // Students
        var studentsQuery = _db.Students
            .Where(s => s.AcademyId == academyId
                     && s.DateOfBirth.Month == month);

        if (assignedCategoryIds != null)
        {
            studentsQuery = studentsQuery.Where(s => assignedCategoryIds.Contains(s.CategoryId));
        }

        var students = await studentsQuery
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

    // =========================================================
    // Phase 3 — Mobile App: GetMobileEventsAsync
    // =========================================================

    /// <summary>
    /// Returns upcoming events for the mobile calendar (current month + next month),
    /// mapped to MobileEventDto (YYYY-MM-DD dates, HH:mm times, type-key strings).
    /// </summary>
    public async Task<List<MobileEventDto>> GetMobileEventsAsync(
        Guid academyId,
        Guid? categoryId = null,
        Guid? headquarterId = null,
        Guid? userId = null,
        string userRole = "")
    {
        var now  = DateTime.UtcNow;
        var from = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-6);
        var to   = from.AddMonths(12); // cover -6 to +6 months

        var query = _db.Events
            .Include(e => e.Category)
            .Include(e => e.Headquarter)
            .Where(e =>
                e.AcademyId == academyId &&
                !e.IsDeleted &&
                e.StartTime >= from &&
                e.StartTime < to);

        List<Guid>? birthdayCategoryFilter = null;

        if (userRole == "Student" && categoryId.HasValue)
        {
            // Student: solo ve eventos de su categoría (o sin categoría asignada)
            query = query.Where(e => e.CategoryId == categoryId || e.CategoryId == null);
            birthdayCategoryFilter = new List<Guid> { categoryId.Value };
        }
        else if (userRole == "Staff" && userId.HasValue)
        {
            // Staff: ve solo eventos de las categorías que tiene asignadas,
            // o donde él es el entrenador (TeacherId)
            var userWithCategories = await _db.Users
                .Include(u => u.AssignedCategories)
                .FirstOrDefaultAsync(u => u.Id == userId.Value);

            if (userWithCategories != null && userWithCategories.AssignedCategories.Any())
            {
                var assignedCategoryIds = userWithCategories.AssignedCategories.Select(c => c.Id).ToList();
                query = query.Where(e =>
                    (e.CategoryId.HasValue && assignedCategoryIds.Contains(e.CategoryId.Value)) ||
                    e.TeacherId == userId.Value);
                birthdayCategoryFilter = assignedCategoryIds;
            }
            else
            {
                // Staff sin categorías: solo ve eventos donde es el entrenador
                query = query.Where(e => e.TeacherId == userId.Value);
                birthdayCategoryFilter = new List<Guid>(); // sin cumpleaños
            }
        }
        else
        {
            // AcademyAdmin/SuperAdmin: sin restricciones adicionales
            if (categoryId.HasValue)
                query = query.Where(e => e.CategoryId == categoryId || e.CategoryId == null);
            if (headquarterId.HasValue)
                query = query.Where(e => e.HeadquarterId == headquarterId || e.HeadquarterId == null);
        }

        var dbEvents = await query.OrderBy(e => e.StartTime).ToListAsync();
        var result = dbEvents.Select(e => MapToMobileDto(e)).ToList();

        // Add virtual birthday events for the full 12 month range
        for (int mOffset = 0; mOffset < 12; mOffset++)
        {
            var target = from.AddMonths(mOffset);
            var bdays  = await BuildBirthdayEventsAsync(academyId, target.Year, target.Month, birthdayCategoryFilter);
            result.AddRange(bdays.Select(b => new MobileEventDto
            {
                Id       = b.Id.ToString(),
                Title    = b.Title,
                Date     = b.StartTime.ToString("yyyy-MM-dd"),
                Type     = "Birthday",
                AllDay   = true,
                Category = b.CategoryName,
            }));
        }

        return result.OrderBy(e => e.Date).ThenBy(e => e.StartTime).ToList();
    }

    // =========================================================
    // Phase 3 — Mobile App: GetNextEventAsync
    // =========================================================

    /// <summary>
    /// Returns the next upcoming event in human-readable format for the dashboard hero card.
    /// </summary>
    public async Task<NextEventDto?> GetNextEventAsync(
        Guid academyId,
        Guid? categoryId = null,
        Guid? userId = null,
        string userRole = "")
    {
        var now = DateTime.UtcNow;

        var query = _db.Events
            .Include(e => e.Headquarter)
            .Include(e => e.Category)
            .Where(e =>
                e.AcademyId == academyId &&
                !e.IsDeleted &&
                e.StartTime >= now);

        if (userRole == "Student" && categoryId.HasValue)
        {
            query = query.Where(e => e.CategoryId == categoryId || e.CategoryId == null);
        }
        else if (userRole == "Staff" && userId.HasValue)
        {
            var userWithCategories = await _db.Users
                .Include(u => u.AssignedCategories)
                .FirstOrDefaultAsync(u => u.Id == userId.Value);

            if (userWithCategories != null && userWithCategories.AssignedCategories.Any())
            {
                var assignedCategoryIds = userWithCategories.AssignedCategories.Select(c => c.Id).ToList();
                query = query.Where(e =>
                    (e.CategoryId.HasValue && assignedCategoryIds.Contains(e.CategoryId.Value)) ||
                    e.TeacherId == userId.Value);
            }
            else
            {
                query = query.Where(e => e.TeacherId == userId.Value);
            }
        }
        else if (categoryId.HasValue)
        {
            query = query.Where(e => e.CategoryId == categoryId || e.CategoryId == null);
        }

        var next = await query.OrderBy(e => e.StartTime).FirstOrDefaultAsync();

        if (next is null) return null;

        // Build human-readable date string
        var localTime = next.StartTime; // Keep as UTC; mobile will adjust if needed
        var today     = now.Date;
        var eventDay  = localTime.Date;

        string dateStr;
        if (eventDay == today)
            dateStr = $"Hoy, {localTime:HH:mm} hrs";
        else if (eventDay == today.AddDays(1))
            dateStr = $"Mañana, {localTime:HH:mm} hrs";
        else
            dateStr = $"{localTime:dddd d 'de' MMMM}, {localTime:HH:mm} hrs";

        return new NextEventDto
        {
            Title    = next.Title,
            Date     = dateStr,
            Location = next.Headquarter?.Name ?? "Sede Principal",
            Type     = MapEventTypeKey(next.Type),
        };
    }

    // =========================================================
    // PRIVATE HELPERS (Phase 3)
    // =========================================================

    private static MobileEventDto MapToMobileDto(Event e) => new()
    {
        Id          = e.Id.ToString(),
        Title       = e.Title,
        Date        = e.StartTime.ToString("yyyy-MM-dd"),
        StartTime   = e.StartTime.ToString("HH:mm"),
        EndTime     = e.EndTime.ToString("HH:mm"),
        Type        = MapEventTypeKey(e.Type),
        Location    = e.Headquarter?.Name,
        Category    = e.Category?.Name,
        Description = e.Description,
        AllDay      = false,
    };

    private static string MapEventTypeKey(EventType t) => t switch
    {
        EventType.Training        => "Training",
        EventType.FriendlyMatch   => "Tournament",
        EventType.TournamentMatch => "Tournament",
        EventType.Birthday        => "Birthday",
        _                         => "Other",
    };
}
