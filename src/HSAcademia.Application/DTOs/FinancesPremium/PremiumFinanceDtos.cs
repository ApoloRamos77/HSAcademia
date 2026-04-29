using System;
using System.Collections.Generic;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Application.DTOs.FinancesPremium;

// ── Petty Cash ─────────────────────────────────────────────
public class PettyCashDto
{
    public Guid Id { get; set; }
    public Guid? HeadquarterId { get; set; }
    public string? HeadquarterName { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal AssignedAmount { get; set; }
    public decimal CurrentBalance { get; set; }
    public List<PettyCashTransactionDto> Transactions { get; set; } = new();
}

public class PettyCashTransactionDto
{
    public Guid Id { get; set; }
    public PettyCashTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string Concept { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? RegisteredByName { get; set; }
}

public class CreatePettyCashDto
{
    public Guid? HeadquarterId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal AssignedAmount { get; set; }
}

public class AddPettyCashTransactionDto
{
    public Guid PettyCashId { get; set; }
    public PettyCashTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string Concept { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}

// ── Staff Payments ─────────────────────────────────────────
public class StaffPaymentDto
{
    public Guid Id { get; set; }
    public Guid StaffId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public int PeriodMonth { get; set; }
    public int PeriodYear { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal Bonuses { get; set; }
    public decimal Deductions { get; set; }
    public decimal TotalPaid { get; set; }
    public StaffPaymentStatus Status { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? Notes { get; set; }
}

public class CreateStaffPaymentDto
{
    public Guid StaffId { get; set; }
    public int PeriodMonth { get; set; }
    public int PeriodYear { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal Bonuses { get; set; }
    public decimal Deductions { get; set; }
    public string? Notes { get; set; }
}

// ── Dashboard Summary ──────────────────────────────────────
public class FinanceSummaryDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal TotalStaffPayments { get; set; }
    public decimal NetBalance { get; set; }
    public List<ExpenseByCategoryDto> ExpensesByCategory { get; set; } = new();
}

public class ExpenseByCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public class MonthlyTrendDto
{
    public string Label { get; set; } = string.Empty; // "Ene", "Feb", etc.
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal StaffPayments { get; set; }
    public decimal NetBalance { get; set; }
}

// ── Financial Goals ───────────────────────────────────────────────────

public class FinancialGoalDto
{
    public Guid Id { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal TargetIncome { get; set; }
    public decimal TargetProfit { get; set; }
    public string Status { get; set; } = string.Empty;
    // Enriched at query time:
    public decimal CurrentIncome { get; set; }
    public decimal CurrentProfit { get; set; }
    public decimal IncomeProgress { get; set; }   // 0-100 %
    public decimal ProfitProgress { get; set; }   // 0-100 %
    public decimal MissingIncome { get; set; }    // How much more is needed
}

public class CreateFinancialGoalDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal TargetIncome { get; set; }
    public decimal TargetProfit { get; set; }
}

// ── Monthly Closings ───────────────────────────────────────────────────

public class MonthlyClosingDto
{
    public Guid Id { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit { get; set; }
    public decimal PettyCashBalance { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ClosedAt { get; set; }
    public string? ClosedByName { get; set; }
    public string? Notes { get; set; }
}

public class CloseMonthDto
{
    public int Month { get; set; }
    public int Year { get; set; }
    public string? Notes { get; set; }
}
