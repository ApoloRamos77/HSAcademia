using System;

namespace HSAcademia.Domain.Entities;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public virtual Academy Academy { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProductCategory { get; set; } = string.Empty; // e.g. "Uniforme", "Suplemento"
    public decimal Price { get; set; }
    public int Stock { get; set; }
    
    public bool IsActive { get; set; } = true;

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}
