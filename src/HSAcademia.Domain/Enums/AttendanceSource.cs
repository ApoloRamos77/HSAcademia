namespace HSAcademia.Domain.Enums;

/// <summary>Cómo se registró una marca de asistencia.</summary>
public enum AttendanceSource
{
    List = 1,
    Qr = 2,
    /// <summary>Falta registrada por el cierre automático al vencer el plazo.</summary>
    Auto = 3
}
