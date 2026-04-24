namespace HSAcademia.Domain.Entities;

public enum RegistrationRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public class AcademyRegistrationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Academy contact info (submitted via public form)
    public string AcademyName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Sport { get; set; }
    public string? Website { get; set; }
    public string? AdditionalInfo { get; set; }

    // Review
    public RegistrationRequestStatus Status { get; set; } = RegistrationRequestStatus.Pending;
    public string? ReviewNotes { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // If approved, links to created academy
    public Guid? CreatedAcademyId { get; set; }
    public Academy? CreatedAcademy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
