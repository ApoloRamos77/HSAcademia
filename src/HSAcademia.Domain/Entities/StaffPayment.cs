using System;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Domain.Entities;

public class StaffPayment
{
    public Guid Id { get; set; }
    public Guid AcademyId { get; set; }
    public Guid StaffId { get; set; }
    public int PeriodMonth { get; set; }
    public int PeriodYear { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal Bonuses { get; set; }
    public decimal Deductions { get; set; }
    public decimal TotalPaid { get; set; }
    public StaffPaymentStatus Status { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Academy Academy { get; set; } = null!;
    public User Staff { get; set; } = null!;
}
