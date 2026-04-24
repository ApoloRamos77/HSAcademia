using System.ComponentModel.DataAnnotations;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Application.DTOs.Calendar;

public class CreateEventDto
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public EventType Type { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    public Guid? HeadquarterId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid? TournamentId { get; set; }

    [MaxLength(200)]
    public string? OpponentTeam { get; set; }
}
