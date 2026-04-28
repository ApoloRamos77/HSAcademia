using HSAcademia.Application.DTOs.Calendar;

namespace HSAcademia.Application.Interfaces;

public interface ICalendarService
{
    /// <summary>
    /// Returns real events + virtual birthday events for the given month/year.
    /// Filters by optional HeadquarterId, CategoryId and EventType.
    /// </summary>
    Task<List<EventDto>> GetEventsForMonthAsync(
        Guid academyId,
        int year,
        int month,
        Guid? headquarterId = null,
        Guid? categoryId = null,
        int? eventType = null);

    Task<EventDto?> GetEventByIdAsync(Guid academyId, Guid eventId);

    /// <summary>
    /// Creates an event after validating teacher/headquarter scheduling conflicts.
    /// </summary>
    Task<EventDto> CreateEventAsync(Guid academyId, CreateEventDto dto);

    Task<EventDto> UpdateEventAsync(Guid academyId, Guid eventId, UpdateEventDto dto);

    Task DeleteEventAsync(Guid academyId, Guid eventId);

    /// <summary>
    /// Shifts StartTime and EndTime of all non-deleted events for the academy by the given number of days.
    /// Used to correct bulk timezone offsets. Positive = add days, negative = subtract days.
    /// </summary>
    Task<int> BulkShiftDaysAsync(Guid academyId, int days);

    // --- Tournament ---
    Task<List<TournamentDto>> GetTournamentsAsync(Guid academyId);
    Task<TournamentDto> CreateTournamentAsync(Guid academyId, CreateTournamentDto dto);
    Task DeleteTournamentAsync(Guid academyId, Guid tournamentId);

    // ── Phase 3: Mobile App endpoints ──

    /// <summary>
    /// Returns upcoming events for the mobile calendar screen, optionally filtered by
    /// the student's CategoryId. Covers the current month + next month.
    /// Maps EventDto → MobileEventDto (YYYY-MM-DD dates, HH:mm times, type keys).
    /// </summary>
    Task<List<MobileEventDto>> GetMobileEventsAsync(
        Guid academyId,
        Guid? categoryId = null,
        Guid? headquarterId = null);

    /// <summary>
    /// Returns the next upcoming event for the academy (or the student's category)
    /// as a human-readable hero card entry for the dashboard.
    /// Returns null if no upcoming event exists.
    /// </summary>
    Task<NextEventDto?> GetNextEventAsync(Guid academyId, Guid? categoryId = null);
}

