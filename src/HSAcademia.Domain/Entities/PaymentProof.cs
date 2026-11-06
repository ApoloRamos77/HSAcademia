namespace HSAcademia.Domain.Entities;

public enum PaymentProofStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

/// <summary>
/// Comprobante de pago enviado por el apoderado/alumno (Yape, Plin, transferencia...).
/// No mueve dinero por sí solo: la academia lo VALIDA y recién entonces se registra el abono
/// (con su número de recibo) sobre la deuda. Si lo rechaza, queda el motivo para la familia.
/// </summary>
public class PaymentProof
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }

    public Guid PaymentRecordId { get; set; }
    public virtual PaymentRecord PaymentRecord { get; set; } = null!;

    public Guid StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public Guid SubmittedByUserId { get; set; }
    public virtual User SubmittedBy { get; set; } = null!;

    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Yape;
    public string? OperationNumber { get; set; }
    public string VoucherUrl { get; set; } = string.Empty;
    /// <summary>Fecha en que la familia dice haber pagado.</summary>
    public DateTime PaidOn { get; set; }
    public string? Notes { get; set; }

    public PaymentProofStatus Status { get; set; } = PaymentProofStatus.Pending;
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    /// <summary>Número de recibo emitido al aprobar.</summary>
    public string? ReceiptNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
