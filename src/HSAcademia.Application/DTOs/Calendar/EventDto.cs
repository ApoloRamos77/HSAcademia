using HSAcademia.Domain.Enums;

namespace HSAcademia.Application.DTOs.Calendar;

public class EventDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public EventType Type { get; set; }
    public string TypeLabel { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public Guid? HeadquarterId { get; set; }
    public string? HeadquarterName { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public Guid? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public Guid? TournamentId { get; set; }
    public string? TournamentName { get; set; }
    public string? OpponentTeam { get; set; }

    /// <summary>
    /// Virtual birthday events generated on-the-fly are not persisted in DB.
    /// </summary>
    public bool IsVirtual { get; set; } = false;
}
