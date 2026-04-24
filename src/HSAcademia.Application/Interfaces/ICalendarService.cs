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

    Task DeleteEventAsync(Guid academyId, Guid eventId);

    // --- Tournament ---
    Task<List<TournamentDto>> GetTournamentsAsync(Guid academyId);
    Task<TournamentDto> CreateTournamentAsync(Guid academyId, CreateTournamentDto dto);
    Task DeleteTournamentAsync(Guid academyId, Guid tournamentId);
}
