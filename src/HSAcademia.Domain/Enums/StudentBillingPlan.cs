namespace HSAcademia.Domain.Enums;

/// <summary>Cómo paga el alumno sus clases.</summary>
public enum StudentBillingPlan
{
    /// <summary>Mensualidad fija (lo de siempre).</summary>
    Monthly = 0,
    /// <summary>Paga solo las sesiones a las que asiste; se liquida a fin de mes con tope en la mensualidad.</summary>
    PerSession = 1,
    /// <summary>Paquete mensual de N sesiones; las sesiones extra se cobran a precio por sesión.</summary>
    Package = 2
}
