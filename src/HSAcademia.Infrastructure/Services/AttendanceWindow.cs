using HSAcademia.Domain.Entities;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Plazo para marcar asistencia de una sesión (alumnos y entrenadores): abre 15 minutos antes del inicio
/// y vence al terminar el día siguiente. Después del vencimiento solo el administrador puede modificarla.
/// Las horas de los eventos son "hora de pared" guardada como UTC; ver AcademyClock.WallNow.
/// </summary>
public static class AttendanceWindow
{
    public const int OpensMinutesBefore = 15;

    /// <summary>Máximo tiempo tras el vencimiento para recibir marcas hechas a tiempo en un celular sin señal.</summary>
    private const int OfflineGraceDays = 7;

    public static DateTime OpensAt(Event ev) => ev.StartTime.AddMinutes(-OpensMinutesBefore);

    /// <summary>Primer instante fuera de plazo: las 00:00 de dos días después de la sesión.</summary>
    public static DateTime ClosesAt(Event ev) => ev.StartTime.Date.AddDays(2);

    /// <summary>Si la marca se hizo dentro del plazo, según la hora real en que se registró en el celular.</summary>
    public static bool MarkedInTime(Event ev, DateTime markedAt)
    {
        var wall = ToWall(markedAt);
        return wall >= OpensAt(ev) && wall < ClosesAt(ev) && AcademyClock.WallNow < ClosesAt(ev).AddDays(OfflineGraceDays);
    }

    /// <summary>Por qué no se puede marcar ahora; null si se puede.</summary>
    public static string? BlockReason(Event ev, bool isAdmin, DateTime? markedAt = null)
    {
        var now = AcademyClock.WallNow;
        if (now < OpensAt(ev))
            return $"La lista abre a las {OpensAt(ev):HH:mm}, {OpensMinutesBefore} minutos antes del inicio.";
        if (isAdmin || now < ClosesAt(ev) || (markedAt.HasValue && MarkedInTime(ev, markedAt.Value))) return null;
        return "El plazo para marcar venció. Solo un administrador puede modificarla.";
    }

    private static DateTime ToWall(DateTime instant) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(instant.ToUniversalTime(), AcademyClock.Zone), DateTimeKind.Utc);
}
