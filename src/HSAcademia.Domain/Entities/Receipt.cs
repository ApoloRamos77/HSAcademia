using System;
using System.Collections.Generic;
using System.Linq;

namespace HSAcademia.Domain.Entities;

public enum ReceiptStatus
{
    Issued = 1,
    Voided = 2
}

public enum ReceiptLineType
{
    Debt = 1,     // Mensualidad u otra deuda del alumno (abono total o parcial)
    Product = 2   // Venta de tienda (uniforme, indumentaria, etc.)
}

/// <summary>
/// Comprobante de cobro: una cabecera con varios ítems (mensualidades de hermanos, productos, etc.)
/// bajo un único número correlativo. Al anularlo se revierten todos sus ítems.
/// </summary>
public class Receipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }

    /// <summary>Número correlativo de la academia (mismo contador que los recibos anteriores).</summary>
    public string Number { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    // ── Pagador ────────────────────────────────────────────────
    public string PayerName { get; set; } = string.Empty;
    public string? PayerDocument { get; set; }
    public Guid? GuardianId { get; set; }

    // ── Pago ───────────────────────────────────────────────────
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public string? OperationNumber { get; set; }
    public string? VoucherUrl { get; set; }
    public string? Notes { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal Total { get; set; }

    /// <summary>Origen: web, app, comprobante validado, etc. (informativo).</summary>
    public string? Channel { get; set; }

    /// <summary>
    /// Clave enviada por la app/web para cada intento de cobro. Si la conexión se corta y se reintenta,
    /// el servidor devuelve el mismo recibo en vez de cobrar dos veces.
    /// </summary>
    public string? ClientRequestId { get; set; }

    /// <summary>Rendición de caja en la que se entregó este recibo (null = aún no rendido).</summary>
    public Guid? CashHandoverId { get; set; }
    public virtual CashHandover? CashHandover { get; set; }

    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }

    // ── Anulación ──────────────────────────────────────────────
    public ReceiptStatus Status { get; set; } = ReceiptStatus.Issued;
    public DateTime? VoidedAt { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ICollection<ReceiptLine> Lines { get; set; } = new List<ReceiptLine>();

    /// <summary>Reparto del pago. Solo tiene filas cuando Method es Mixed.</summary>
    public virtual ICollection<ReceiptPayment> Payments { get; set; } = new List<ReceiptPayment>();

    /// <summary>Cuánto entró por cada medio de pago. En recibos mixtos requiere haber cargado Payments.</summary>
    public IReadOnlyList<(PaymentMethod Method, decimal Amount)> PaymentParts()
    {
        if (Method != PaymentMethod.Mixed) return new[] { (Method, Total) };
        if (Payments.Count == 0)
            throw new InvalidOperationException($"No se cargó el detalle de pagos del recibo mixto N° {Number}.");
        return Payments.OrderBy(p => p.Position).Select(p => (p.Method, p.Amount)).ToList();
    }

    public decimal CashAmount() => PaymentParts().Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);

    public decimal DigitalAmount() => Total - CashAmount();
}

/// <summary>Parte de un recibo mixto pagada con un medio concreto (ej. S/ 60 en efectivo y S/ 40 por Yape).</summary>
public class ReceiptPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public virtual Receipt Receipt { get; set; } = null!;
    public int Position { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public string? OperationNumber { get; set; }
    public string? VoucherUrl { get; set; }
}

/// <summary>
/// Ítem del comprobante. Guarda una copia de la descripción y los importes (para reimprimir igual)
/// y el vínculo al abono o a la venta que generó (para poder anularlo).
/// </summary>
public class ReceiptLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiptId { get; set; }
    public virtual Receipt Receipt { get; set; } = null!;
    public int Position { get; set; }

    public ReceiptLineType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? StudentId { get; set; }
    public string? StudentName { get; set; }

    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    /// <summary>Qué descuentos se aplicaron (ej. "Pronto pago 5%", "Manual").</summary>
    public string? DiscountLabel { get; set; }
    public decimal Total { get; set; }

    public Guid? PaymentRecordId { get; set; }
    public Guid? PaymentInstallmentId { get; set; }
    public Guid? ProductSaleId { get; set; }
    public Guid? ProductId { get; set; }
}
