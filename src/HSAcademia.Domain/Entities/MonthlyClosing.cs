using System;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Domain.Entities;

public class MonthlyClosing
{
    public Guid Id { get; set; }
    public Guid AcademyId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit { get; set; }
    public decimal PettyCashBalance { get; set; }
    public MonthlyClosingStatus Status { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedById { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Academy Academy { get; set; } = null!;
    public User? ClosedBy { get; set; }
}
