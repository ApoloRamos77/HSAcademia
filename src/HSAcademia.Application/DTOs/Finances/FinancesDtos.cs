using System;

namespace HSAcademia.Application.DTOs.Finances;

public class FinancialConfigDto
{
    public int DefaultPaymentDay { get; set; }
}

public class UpdateFinancialConfigDto
{
    public int DefaultPaymentDay { get; set; }
}

public class PaymentRecordDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
    public int Type { get; set; }
    public int DaysOverdue { get; set; }
}

public class MarkAsPaidDto
{
    public Guid PaymentRecordId { get; set; }
}
