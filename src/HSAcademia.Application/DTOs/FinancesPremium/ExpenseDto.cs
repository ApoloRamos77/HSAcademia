using System;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Application.DTOs.FinancesPremium;

public class ExpenseDto
{
    public Guid Id { get; set; }
    public ExpenseType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? VoucherUrl { get; set; }
}

public class CreateExpenseDto
{
    public ExpenseType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? VoucherUrl { get; set; }
}
