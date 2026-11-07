using System;
using System.Collections.Generic;

namespace HSAcademia.Application.DTOs.Finances;

/// <summary>Cobro con varios ítems bajo un solo comprobante.</summary>
public class CheckoutDto
{
    /// <summary>Apoderado que paga (opcional). Si se omite, se usa PayerName o el apoderado del primer alumno.</summary>
    public Guid? GuardianId { get; set; }
    public string? PayerName { get; set; }
    public string? PayerDocument { get; set; }

    public string Method { get; set; } = "Cash"; // Cash | BankTransfer | Yape | Plin | Card | Other
    public string? OperationNumber { get; set; }
    public string? VoucherUrl { get; set; }
    public string? Notes { get; set; }
    /// <summary>"web" | "app" (informativo).</summary>
    public string? Channel { get; set; }
    /// <summary>Clave única por intento de cobro (evita cobros duplicados al reintentar).</summary>
    public string? ClientRequestId { get; set; }
    /// <summary>Promoción elegida por el cajero (opcional). Pronto pago se aplica solo.</summary>
    public Guid? PromotionId { get; set; }

    public List<CheckoutDebtItemDto> Debts { get; set; } = new();
    public List<CheckoutProductItemDto> Products { get; set; } = new();
}

public class CheckoutDebtItemDto
{
    public Guid PaymentRecordId { get; set; }
    /// <summary>
    /// Monto que se cobra ahora. Vacío = cancelar la deuda completa (con los descuentos que correspondan).
    /// Un abono parcial no recibe descuentos automáticos (pronto pago / promoción).
    /// </summary>
    public decimal? Amount { get; set; }
    /// <summary>Descuento manual adicional en caja (rebaja el importe de la deuda).</summary>
    public decimal Discount { get; set; }
    /// <summary>
    /// Mensualidad vencida sin regularizar: decisión obligatoria (con o sin mora + observación).
    /// Si se cobra mora, entra en el mismo recibo como un ítem aparte.
    /// </summary>
    public RegularizeDebtDto? Regularization { get; set; }
}

public class CheckoutProductItemDto
{
    public Guid ProductId { get; set; }
    public Guid? StudentId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal Discount { get; set; }
    public bool IsGift { get; set; }
}

public class VoidReceiptDto
{
    public string Reason { get; set; } = string.Empty;
}

public class ReceiptDto
{
    public Guid? Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public string PayerName { get; set; } = string.Empty;
    public string? PayerDocument { get; set; }
    public Guid? GuardianId { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? OperationNumber { get; set; }
    public string? VoucherUrl { get; set; }
    public string? Notes { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal Total { get; set; }
    public string? Channel { get; set; }
    public string? CreatedByName { get; set; }
    /// <summary>"Issued" | "Voided".</summary>
    public string Status { get; set; } = "Issued";
    public DateTime? VoidedAt { get; set; }
    public string? VoidReason { get; set; }
    /// <summary>Recibo emitido antes de los comprobantes con varios ítems (reconstruido; no se puede anular aquí).</summary>
    public bool IsLegacy { get; set; }
    public Guid? CashHandoverId { get; set; }
    public bool IsPreview { get; set; }
    public List<ReceiptLineDto> Lines { get; set; } = new();
}

public class ReceiptLineDto
{
    /// <summary>"Debt" | "Product".</summary>
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? StudentId { get; set; }
    public string? StudentName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public string? DiscountLabel { get; set; }
    public decimal Total { get; set; }
    public Guid? PaymentRecordId { get; set; }
    public Guid? ProductId { get; set; }
    /// <summary>Deudas: saldo pendiente antes de este cobro.</summary>
    public decimal? PendingBefore { get; set; }
}

/// <summary>Cuenta familiar: el apoderado, sus hijos inscritos y todo lo que tienen pendiente.</summary>
public class FamilyAccountDto
{
    public Guid? GuardianId { get; set; }
    public string PayerName { get; set; } = string.Empty;
    public string? PayerDocument { get; set; }
    public string? PayerPhone { get; set; }
    public List<FamilyStudentDto> Students { get; set; } = new();
    public List<PaymentRecordDto> PendingDebts { get; set; } = new();
    public decimal TotalPending { get; set; }
}

public class FamilyStudentDto
{
    public Guid StudentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

// ── Descuentos y promociones ─────────────────────────────────────
public class DiscountRuleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>"Sibling" | "EarlyPayment" | "Promotion".</summary>
    public string Kind { get; set; } = string.Empty;
    /// <summary>"Percent" | "Amount".</summary>
    public string ValueType { get; set; } = "Percent";
    public decimal Value { get; set; }
    /// <summary>"MonthlyFees" | "Products" | "All".</summary>
    public string Scope { get; set; } = "MonthlyFees";
    public int? FromSiblingNumber { get; set; }
    public int? DaysBeforeDue { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public bool IsActive { get; set; }
    public bool IsCurrentlyValid { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class SaveDiscountRuleDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Kind { get; set; } = "Promotion";
    public string ValueType { get; set; } = "Percent";
    public decimal Value { get; set; }
    public string Scope { get; set; } = "MonthlyFees";
    public int? FromSiblingNumber { get; set; }
    public int? DaysBeforeDue { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public Guid? CategoryId { get; set; }
    public bool IsActive { get; set; } = true;
}

// ── Cuadre de caja ───────────────────────────────────────────────
public class CashMethodTotalDto
{
    public string Method { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Total { get; set; }
}

public class CashCollectorSummaryDto
{
    public Guid? CollectorUserId { get; set; }
    public string CollectorName { get; set; } = string.Empty;
    public int ReceiptsCount { get; set; }
    public decimal Total { get; set; }
    public decimal Cash { get; set; }
    public decimal Digital { get; set; }
    public List<CashMethodTotalDto> ByMethod { get; set; } = new();
    /// <summary>Efectivo cobrado (cualquier día) que aún no se rinde.</summary>
    public decimal PendingCash { get; set; }
    public int PendingReceipts { get; set; }
    /// <summary>Efectivo declarado en rendiciones aún no recibidas por finanzas.</summary>
    public decimal InTransitCash { get; set; }
}

public class CashDaySummaryDto
{
    public DateTime Date { get; set; }
    public decimal Total { get; set; }
    public decimal Cash { get; set; }
    public decimal Digital { get; set; }
    public int ReceiptsCount { get; set; }
    public int VoidedCount { get; set; }
    public List<CashMethodTotalDto> ByMethod { get; set; } = new();
    public List<CashCollectorSummaryDto> Collectors { get; set; } = new();
    public List<CashHandoverDto> Handovers { get; set; } = new();
}

public class MyCashDto
{
    public string CollectorName { get; set; } = string.Empty;
    public int PendingReceipts { get; set; }
    public decimal PendingCash { get; set; }
    public decimal PendingDigital { get; set; }
    public List<CashMethodTotalDto> ByMethod { get; set; } = new();
    public List<ReceiptDto> Receipts { get; set; } = new();
    public List<CashHandoverDto> RecentHandovers { get; set; } = new();
}

public class CashHandoverDto
{
    public Guid Id { get; set; }
    public Guid CollectorUserId { get; set; }
    public string CollectorName { get; set; } = string.Empty;
    public DateTime DeclaredAt { get; set; }
    public int ReceiptsCount { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal DigitalTotal { get; set; }
    public decimal DeclaredCash { get; set; }
    /// <summary>Declarado − esperado (negativo = falta dinero).</summary>
    public decimal DeclaredDifference { get; set; }
    public string? Notes { get; set; }
    /// <summary>"Pending" | "Received".</summary>
    public string Status { get; set; } = "Pending";
    public string? ReceivedByName { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public decimal? ReceivedCash { get; set; }
    /// <summary>Recibido − esperado.</summary>
    public decimal? ReceivedDifference { get; set; }
    public string? ReceivedNotes { get; set; }
}

public class DeclareHandoverDto
{
    public decimal DeclaredCash { get; set; }
    public string? Notes { get; set; }
}

public class ReceiveHandoverDto
{
    public decimal ReceivedCash { get; set; }
    public string? Notes { get; set; }
}
