using System;
using System.Collections.Generic;

namespace HSAcademia.Application.DTOs.Finances;

public class FinancialConfigDto
{
    public int DefaultPaymentDay { get; set; }
}

public class UpdateFinancialConfigDto
{
    public int DefaultPaymentDay { get; set; }
}

public class PaymentInstallmentDto
{
    public Guid Id { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTime PaidAt { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? OperationNumber { get; set; }
    public string? VoucherUrl { get; set; }
    public string? Notes { get; set; }
}

public class PaymentRecordDto
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountPending => Amount - AmountPaid;
    public DateTime DueDate { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
    public int Type { get; set; }
    /// <summary>Positive = days overdue. 0 = on time / future.</summary>
    public int DaysOverdue { get; set; }
    /// <summary>Status: Pagado | En Curso | Vencido</summary>
    public string Status { get; set; } = string.Empty;

    // Proration
    public bool IsProrated { get; set; }
    public DateTime? ProratedStartDate { get; set; }
    public int? ProratedTotalDays { get; set; }
    public int? ProratedDaysCharged { get; set; }

    // Exclusion
    public string ExclusionType { get; set; } = "None";
    public string? ExclusionNote { get; set; }

    public List<PaymentInstallmentDto> Installments { get; set; } = new();
}

/// <summary>Register a full or partial payment with method, voucher, operation number.</summary>
public class RegisterPaymentDto
{
    public Guid PaymentRecordId { get; set; }
    public decimal AmountPaid { get; set; }
    public string Method { get; set; } = "Cash";  // Cash | BankTransfer | Yape | Plin | Card | Other
    public string? OperationNumber { get; set; }
    public string? VoucherUrl { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Mark an existing charge as exonerated (free class, recovery, etc.).</summary>
public class ExcludePaymentDto
{
    public Guid PaymentRecordId { get; set; }
    /// <summary>FreeClass | Recovery | Suspension | OtherExclusion</summary>
    public string ExclusionType { get; set; } = "FreeClass";
    public string? ExclusionNote { get; set; }
}

/// <summary>Recalculate (regenerate) a payment amount, e.g. after proration update.</summary>
public class RecalculatePaymentDto
{
    public Guid PaymentRecordId { get; set; }
    /// <summary>New amount to set. If null, the system recalculates from category/preferential fee.</summary>
    public decimal? NewAmount { get; set; }
    /// <summary>Optional proration: start date within the billing period.</summary>
    public DateTime? ProratedStartDate { get; set; }
    public string? Notes { get; set; }
}

// Legacy — kept for mobile backward compatibility
public class MarkAsPaidDto
{
    public Guid PaymentRecordId { get; set; }
}

