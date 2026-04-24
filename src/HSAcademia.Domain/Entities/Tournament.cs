namespace HSAcademia.Domain.Entities;

public class Tournament
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; } // Tenant
    public string Name { get; set; } = string.Empty;
    public string Organizer { get; set; } = string.Empty;
    public string MainLocation { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public virtual Academy Academy { get; set; } = null!;
}
