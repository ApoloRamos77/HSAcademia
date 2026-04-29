using System;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Domain.Entities;

public class FinancialGoal
{
    public Guid Id { get; set; }
    public Guid AcademyId { get; set; }
    public Guid? HeadquarterId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal TargetIncome { get; set; }
    public decimal TargetProfit { get; set; }
    public FinancialGoalStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Academy Academy { get; set; } = null!;
    public Headquarter? Headquarter { get; set; }
}
