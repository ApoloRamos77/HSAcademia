using System;
using System.Collections.Generic;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Domain.Entities;

/// <summary>Tramo del tarifario: desde la N.ª hora (o sesión) del mismo día se paga esta tarifa.</summary>
public class PayTier
{
    public int From { get; set; }
    public decimal Rate { get; set; }
}

/// <summary>
/// Tarifario por hora o por sesión. La academia marca uno como predeterminado por unidad;
/// a una persona se le puede asignar otro como excepción.
/// </summary>
public class PayScale
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public PayUnit Unit { get; set; }
    public List<PayTier> Tiers { get; set; } = new();
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    /// <summary>Tarifa de la posición dada (1 = primera hora o sesión del día).</summary>
    public decimal RateFor(int position)
    {
        decimal rate = 0;
        var best = int.MinValue;
        foreach (var t in Tiers)
            if (t.From <= position && t.From > best) { best = t.From; rate = t.Rate; }
        return rate;
    }
}

/// <summary>Bono, descuento o adelanto de una liquidación, siempre con su concepto.</summary>
public class StaffPaymentLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StaffPaymentId { get; set; }
    public StaffPaymentLineKind Kind { get; set; }
    public string Concept { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public Guid? StaffAdvanceId { get; set; }
}

/// <summary>Dinero entregado al personal antes de su pago; se descuenta en la siguiente liquidación.</summary>
public class StaffAdvance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public Guid StaffId { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Concept { get; set; } = string.Empty;
    public string? Method { get; set; }
    /// <summary>Liquidación que lo descontó (pagada). Null mientras esté pendiente.</summary>
    public Guid? StaffPaymentId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
