using System;
using System.Collections.Generic;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Domain.Entities;

public class StaffPayment
{
    public Guid Id { get; set; }
    public Guid AcademyId { get; set; }
    public Guid StaffId { get; set; }
    /// <summary>Mes y año del fin del periodo (resúmenes financieros mensuales).</summary>
    public int PeriodMonth { get; set; }
    public int PeriodYear { get; set; }
    /// <summary>Periodo liquidado (fechas locales, ambos inclusive).</summary>
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public PayFrequency Frequency { get; set; }
    /// <summary>Parte fija (sueldo) del periodo.</summary>
    public decimal BaseAmount { get; set; }
    /// <summary>Lo ganado por horas o sesiones dictadas.</summary>
    public decimal UnitsAmount { get; set; }
    public decimal? Hours { get; set; }
    /// <summary>Detalle por día (JSON) tal como se calculó.</summary>
    public string? Detail { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedByName { get; set; }
    public decimal Bonuses { get; set; }
    public decimal Deductions { get; set; }
    public int? SessionsCount { get; set; }
    public decimal TotalPaid { get; set; }
    public StaffPaymentStatus Status { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Academy Academy { get; set; } = null!;
    public User Staff { get; set; } = null!;
    public List<StaffPaymentLine> Lines { get; set; } = new();
}
