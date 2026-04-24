using System.ComponentModel.DataAnnotations;

namespace HSAcademia.Application.DTOs.Calendar;

public class TournamentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Organizer { get; set; } = string.Empty;
    public string MainLocation { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateTournamentDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Organizer { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string MainLocation { get; set; } = string.Empty;
}
