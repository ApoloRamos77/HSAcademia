using System;

namespace HSAcademia.Domain.Entities;

public enum DiscountKind
{
    /// <summary>Hermanos: se aplica al generar la mensualidad a partir del N-ésimo hijo inscrito.</summary>
    Sibling = 1,
    /// <summary>Pronto pago: se aplica al cancelar la mensualidad completa hasta X días antes del vencimiento.</summary>
    EarlyPayment = 2,
    /// <summary>Promoción con vigencia: el cajero la elige al cobrar (ej. "Campaña verano", "Uniforme 20%").</summary>
    Promotion = 3
}

public enum DiscountValueType { Percent = 1, Amount = 2 }

public enum DiscountScope { MonthlyFees = 1, Products = 2, All = 3 }

/// <summary>Regla de descuento configurable por la academia.</summary>
public class DiscountRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DiscountKind Kind { get; set; }
    public DiscountValueType ValueType { get; set; } = DiscountValueType.Percent;
    /// <summary>Porcentaje (0-100) o monto fijo en soles por ítem.</summary>
    public decimal Value { get; set; }
    public DiscountScope Scope { get; set; } = DiscountScope.MonthlyFees;

    /// <summary>Hermanos: desde qué hijo aplica (2 = segundo hijo en adelante).</summary>
    public int? FromSiblingNumber { get; set; }
    /// <summary>Pronto pago: días de anticipación al vencimiento (0 = hasta el mismo día del vencimiento).</summary>
    public int? DaysBeforeDue { get; set; }

    /// <summary>Vigencia (fechas locales). Vacío = sin límite.</summary>
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    /// <summary>Solo para una categoría (opcional).</summary>
    public Guid? CategoryId { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public bool AppliesToMonthlyFees => Scope is DiscountScope.MonthlyFees or DiscountScope.All;
    public bool AppliesToProducts => Scope is DiscountScope.Products or DiscountScope.All;

    /// <summary>Descuento para un importe (nunca mayor al importe).</summary>
    public decimal DiscountFor(decimal amount)
    {
        if (amount <= 0 || Value <= 0) return 0m;
        var d = ValueType == DiscountValueType.Percent
            ? Math.Round(amount * Math.Min(Value, 100m) / 100m, 2)
            : Value;
        return Math.Min(d, amount);
    }

    public bool IsValidOn(DateTime localDate) =>
        IsActive && !IsDeleted &&
        (!ValidFrom.HasValue || localDate.Date >= ValidFrom.Value.Date) &&
        (!ValidTo.HasValue || localDate.Date <= ValidTo.Value.Date);

    public string Label => ValueType == DiscountValueType.Percent
        ? $"{Name} {Value:0.##}%"
        : $"{Name} S/ {Value:0.00}";
}
