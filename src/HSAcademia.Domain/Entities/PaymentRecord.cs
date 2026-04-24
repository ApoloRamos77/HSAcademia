using System;

namespace HSAcademia.Domain.Entities;

public enum PaymentType
{
    MonthlyFee = 1,
    ProductSale = 2,
    RegistrationFee = 3,
    Other = 4
}

public class PaymentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public virtual Academy Academy { get; set; } = null!;

    public Guid StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    
    public bool IsPaid { get; set; } = false;
    public DateTime? PaidDate { get; set; }
    
    public PaymentType Type { get; set; } = PaymentType.MonthlyFee;

    // Optional link to a ProductSale if this payment originated from a store sale
    public Guid? ProductSaleId { get; set; }
    public virtual ProductSale? ProductSale { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}
