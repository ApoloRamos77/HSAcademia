namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Hora de negocio de la academia (Perú, UTC-5 sin horario de verano).
/// En la base se guardan instantes en UTC; "hoy", "este mes" y los rangos mensuales
/// se calculan en hora local para que un pago a las 8 p. m. no caiga en el día/mes siguiente.
/// </summary>
public static class AcademyClock
{
    public static readonly TimeZoneInfo Zone = ResolveZone();

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "America/Lima", "SA Pacific Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        // Contenedor sin tzdata: Perú no usa horario de verano, UTC-5 fijo es exacto.
        return TimeZoneInfo.CreateCustomTimeZone("America/Lima", TimeSpan.FromHours(-5), "Lima", "Lima");
    }

    /// <summary>Fecha y hora local actual (Kind = Unspecified).</summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    /// <summary>
    /// "Ahora" en la convención de los EVENTOS del calendario: el frontend guarda la hora de pared local
    /// con sufijo Z (las 18:00 de Lima se guardan como 18:00 UTC) y la muestra con timeZone 'UTC'.
    /// Para comparar contra Event.StartTime/EndTime hay que usar este valor, no DateTime.UtcNow.
    /// </summary>
    public static DateTime WallNow => DateTime.SpecifyKind(Now, DateTimeKind.Utc);

    /// <summary>Fecha local de hoy, a medianoche (Kind = Unspecified, apta para columnas "date").</summary>
    public static DateTime Today => Now.Date;

    /// <summary>Fecha local de un instante UTC.</summary>
    public static DateTime ToLocalDate(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone).Date;

    /// <summary>Rango [inicio, fin) en UTC que corresponde al mes calendario local indicado.</summary>
    public static (DateTime StartUtc, DateTime EndUtc) MonthRangeUtc(int year, int month)
    {
        var startLocal = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var endLocal = startLocal.AddMonths(1);
        return (TimeZoneInfo.ConvertTimeToUtc(startLocal, Zone), TimeZoneInfo.ConvertTimeToUtc(endLocal, Zone));
    }
}
