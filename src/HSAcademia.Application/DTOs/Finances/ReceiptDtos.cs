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

    public List<CheckoutDebtItemDto> Debts { get; set; } = new();
    public List<CheckoutProductItemDto> Products { get; set; } = new();
}

public class CheckoutDebtItemDto
{
    public Guid PaymentRecordId { get; set; }
    /// <summary>Monto que se cobra ahora (abono total o parcial).</summary>
    public decimal Amount { get; set; }
    /// <summary>Descuento aplicado a la deuda en caja (rebaja el importe de la deuda).</summary>
    public decimal Discount { get; set; }
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
    public decimal Total { get; set; }
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
