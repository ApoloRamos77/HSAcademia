using System;

namespace HSAcademia.Domain.Entities;

public class AcademyRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; } // Tenant Discriminator
    
    public string Name { get; set; } = string.Empty; // e.g. "Recepción", "Coordinador"
    public string? Description { get; set; }
    
    public bool IsActive { get; set; } = true;

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public virtual Academy? Academy { get; set; }
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
