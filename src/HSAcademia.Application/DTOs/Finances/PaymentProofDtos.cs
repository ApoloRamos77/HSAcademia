namespace HSAcademia.Application.DTOs.Finances;

public class PaymentProofDto
{
    public Guid Id { get; set; }
    public Guid PaymentRecordId { get; set; }
    public string DebtDescription { get; set; } = string.Empty;
    public decimal DebtPending { get; set; }
    /// <summary>Pagado después del vencimiento y aún sin regularizar: al aprobar se decide la mora.</summary>
    public bool RequiresRegularization { get; set; }
    public DateTime DebtDueDate { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string SubmittedByName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? OperationNumber { get; set; }
    public string VoucherUrl { get; set; } = string.Empty;
    public DateTime PaidOn { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;   // Pending | Approved | Rejected
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? ReceiptNumber { get; set; }
}

public class ApprovePaymentProofDto
{
    /// <summary>Monto a abonar; si es null se usa el monto declarado (limitado al saldo pendiente).</summary>
    public decimal? Amount { get; set; }
    public string? Notes { get; set; }
    /// <summary>Pago hecho después del vencimiento: decisión de mora obligatoria al aprobar.</summary>
    public RegularizeDebtDto? Regularization { get; set; }
}

public class RejectPaymentProofDto
{
    public string Reason { get; set; } = string.Empty;
}
