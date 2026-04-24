using System;

namespace HSAcademia.Domain.Entities;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; } // Tenant Discriminator
    public Guid HeadquarterId { get; set; } // Which headquarter it belongs to

    public string Name { get; set; } = string.Empty; // e.g. "Sub-12"
    public int MinAge { get; set; }
    public int MaxAge { get; set; }
    public decimal MonthlyFee { get; set; } = 0m;
    public bool IsActive { get; set; } = true;

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public virtual Academy? Academy { get; set; }
    public virtual Headquarter? Headquarter { get; set; }

    public virtual ICollection<User> StaffMembers { get; set; } = new List<User>();
}
