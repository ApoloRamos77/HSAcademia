using System;

namespace HSAcademia.Domain.Entities;

/// <summary>
/// Campaña de referidos: define cuánto gana la familia que recomienda (referente) y, opcionalmente,
/// el alumno nuevo (referido) en su primer cobro. Una sola campaña vigente a la vez.
/// Los montos se congelan en cada referido al registrarlo: cambiar la campaña no altera lo ya prometido.
/// </summary>
public class ReferralCampaign
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Vigencia (fechas locales, ambas incluidas).</summary>
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    /// <summary>Descuento en soles para el referente, en su siguiente mensualidad.</summary>
    public decimal ReferrerAmount { get; set; }
    /// <summary>Descuento en soles para el referido en su primer cobro (0 = sin beneficio).</summary>
    public decimal ReferredAmount { get; set; }
    /// <summary>Solo para alumnos nuevos de esta categoría (opcional).</summary>
    public Guid? CategoryId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public bool IsValidOn(DateTime localDate) =>
        IsActive && !IsDeleted && localDate.Date >= ValidFrom.Date && localDate.Date <= ValidTo.Date;
}

public enum ReferralStatus
{
    /// <summary>Registrado: el referido aún no paga su primer cobro.</summary>
    Registered = 1,
    /// <summary>Ganado: el referido pagó; el beneficio del referente está pendiente de aplicar (o aplicado en parte).</summary>
    Earned = 2,
    /// <summary>Completado: todos los beneficios quedaron aplicados.</summary>
    Completed = 3,
    /// <summary>Anulado por administración (con motivo).</summary>
    Annulled = 4
}

/// <summary>Un alumno nuevo (referido) recomendado por otro alumno (referente).</summary>
public class Referral
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public Guid CampaignId { get; set; }
    public virtual ReferralCampaign Campaign { get; set; } = null!;
    public Guid ReferrerStudentId { get; set; }
    public virtual Student ReferrerStudent { get; set; } = null!;
    public Guid ReferredStudentId { get; set; }
    public virtual Student ReferredStudent { get; set; } = null!;

    /// <summary>Montos congelados al registrar (los de la campaña vigente ese día).</summary>
    public decimal ReferrerAmount { get; set; }
    public decimal ReferredAmount { get; set; }

    public ReferralStatus Status { get; set; } = ReferralStatus.Registered;
    /// <summary>Cuándo el referido pagó su primer cobro (vacío si aún no, o si ese pago se anuló).</summary>
    public DateTime? EarnedAt { get; set; }
    /// <summary>Se avisó a la familia del referente que ganó el beneficio.</summary>
    public DateTime? NotifiedAt { get; set; }
    public string? Notes { get; set; }

    public string? AnnulReason { get; set; }
    public DateTime? AnnulledAt { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum ReferralSide
{
    /// <summary>Beneficio del alumno que recomendó.</summary>
    Referrer = 1,
    /// <summary>Beneficio de bienvenida del alumno nuevo.</summary>
    Referred = 2
}

/// <summary>
/// Descuento de un referido aplicado sobre una deuda concreta. Lo pendiente del referente es
/// su monto congelado menos lo aplicado vigente; un descuento revertido (deuda borrada, pago anulado
/// o referido anulado) deja de contar y el beneficio vuelve a quedar disponible.
/// </summary>
public class ReferralBenefit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; }
    public Guid ReferralId { get; set; }
    public virtual Referral Referral { get; set; } = null!;
    public ReferralSide Side { get; set; }
    public Guid PaymentRecordId { get; set; }
    public Guid StudentId { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Texto agregado al detalle de descuentos de la deuda (para quitarlo al revertir).</summary>
    public string Label { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevertedAt { get; set; }
    public string? RevertReason { get; set; }
}
