using System.ComponentModel.DataAnnotations;

namespace HSAcademia.Application.DTOs.SuperAdmin;

public class EditRegistrationRequestDto
{
    [Required, MaxLength(100)]
    public string AcademyName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required, MaxLength(100)]
    public string ContactName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(100)]
    public string ContactEmail { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? ContactPhone { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    [MaxLength(50)]
    public string? Sport { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    [MaxLength(1000)]
    public string? AdditionalInfo { get; set; }
}
