using System;
using System.Collections.Generic;

namespace HSAcademia.Domain.Entities;

public enum PaymentType
{
    MonthlyFee = 1,
    ProductSale = 2,
    RegistrationFee = 3,
    Other = 4,
    /// <summary>Mora por pago fuera de fecha (la decide administración al regularizar).</summary>
    LateFee = 5
}

/// <summary>How a payment (or partial payment) was received.</summary>
public enum PaymentMethod
{
    Cash = 1,
    BankTransfer = 2,
    Yape = 3,
    Plin = 4,
    Card = 5,
    /// <summary>Un mismo recibo pagado con varios medios: el reparto está en Receipt.Payments.</summary>
    Mixed = 6,
    Other = 9
}

/// <summary>Reason why a period should not be charged (exoneration).</summary>
public enum ExclusionType
{
    None = 0,
    FreeClass = 1,      // Clase gratuita
    Recovery = 2,       // Clase de recuperación
    Suspension = 3,     // Alumno suspendido ese período
    OtherExclusion = 9
}

public class PaymentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public virtual Academy Academy { get; set; } = null!;

    public Guid StudentId { get; set; }
    public virtual Student Student { get; set; } = null!;

    public string Description { get; set; } = string.Empty;

    /// <summary>Total original amount (full fee or prorated).</summary>
    public decimal Amount { get; set; }

    /// <summary>Total already paid (sum of all PaymentInstallments).</summary>
    public decimal AmountPaid { get; set; } = 0m;

    /// <summary>Amount that was discounted or not charged (loss/gift/scholarship).</summary>
    public decimal DiscountAmount { get; set; } = 0m;

    // ── Regularización de mensualidad vencida ───────────────────
    // Pasado el vencimiento, la familia debe acercarse a administración: se decide cobrar mora o no,
    // siempre con una observación. Hasta entonces no se puede cobrar ni enviar comprobante.
    public DateTime? RegularizedAt { get; set; }
    public Guid? RegularizedByUserId { get; set; }
    public string? RegularizedByName { get; set; }
    public string? RegularizationNote { get; set; }
    /// <summary>Mora cobrada (null = se regularizó sin mora).</summary>
    public decimal? LateFeeAmount { get; set; }
    /// <summary>Deuda de mora generada al regularizar.</summary>
    public Guid? LateFeeRecordId { get; set; }
    /// <summary>Detalle de los descuentos aplicados al generar (ej. "Beca 50% · Hermanos 10%").</summary>
    public string? DiscountDetail { get; set; }

    public DateTime DueDate { get; set; }

    /// <summary>Periodo de la mensualidad (año/mes). Identifica la deuda sin depender del texto de Description.</summary>
    public int? PeriodYear { get; set; }
    public int? PeriodMonth { get; set; }

    public bool IsPaid { get; set; } = false;
    public DateTime? PaidDate { get; set; }

    public PaymentType Type { get; set; } = PaymentType.MonthlyFee;

    // ── Proration ──────────────────────────────────────────────
    public bool IsProrated { get; set; } = false;
    /// <summary>Start date used for proration calculation.</summary>
    public DateTime? ProratedStartDate { get; set; }
    /// <summary>Total days in billing period.</summary>
    public int? ProratedTotalDays { get; set; }
    /// <summary>Days actually charged after proration.</summary>
    public int? ProratedDaysCharged { get; set; }

    // ── Exclusion (free / recovery / etc.) ─────────────────────
    public ExclusionType ExclusionType { get; set; } = ExclusionType.None;
    public string? ExclusionNote { get; set; }

    // ── Optional link to a ProductSale ─────────────────────────
    public Guid? ProductSaleId { get; set; }
    public virtual ProductSale? ProductSale { get; set; }

    // ── Partial payment installments ───────────────────────────
    public virtual ICollection<PaymentInstallment> Installments { get; set; } = new List<PaymentInstallment>();

    // ── Audit ─────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}

/// <summary>Represents one partial or full payment applied to a PaymentRecord.</summary>
public class PaymentInstallment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentRecordId { get; set; }
    public virtual PaymentRecord PaymentRecord { get; set; } = null!;

    public decimal AmountPaid { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public string? OperationNumber { get; set; }
    public string? VoucherUrl { get; set; }
    public string? Notes { get; set; }

    /// <summary>Sequential receipt number assigned at the moment of payment (e.g. "000044").</summary>
    public string? ReceiptNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

