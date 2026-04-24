using System;

namespace HSAcademia.Domain.Entities;

public class ProductSale
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public virtual Academy Academy { get; set; } = null!;

    public Guid ProductId { get; set; }
    public virtual Product Product { get; set; } = null!;

    // Optional link to Student
    public Guid? StudentId { get; set; }
    public virtual Student? Student { get; set; }

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime SaleDate { get; set; } = DateTime.UtcNow;
    
    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}
