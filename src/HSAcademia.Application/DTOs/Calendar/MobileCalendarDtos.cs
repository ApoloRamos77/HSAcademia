namespace HSAcademia.Application.DTOs.Calendar;

// ─────────────────────────────────────────────────────────────
// Mobile-specific Calendar DTOs — Fase 3 App Móvil HSAcademia
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Compact event representation optimised for mobile display.
/// Fields map directly to the react-native-calendars EventCard component.
/// </summary>
public class MobileEventDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>Date in YYYY-MM-DD format for calendar marking.</summary>
    public string Date { get; set; } = string.Empty;

    public string? StartTime { get; set; }    // "HH:mm"
    public string? EndTime { get; set; }      // "HH:mm"

    /// <summary>
    /// Event type key matching the mobile EVENT_TYPES dictionary:
    /// Training | Tournament | Social | Birthday | Other
    /// </summary>
    public string Type { get; set; } = "Other";

    public string? Location { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public bool AllDay { get; set; } = false;
}

/// <summary>
/// Next upcoming event for the dashboard hero card.
/// </summary>
public class NextEventDto
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Human-readable date+time string, e.g. "Hoy, 16:30 hrs" or "Mañana, 09:00 hrs".</summary>
    public string Date { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;
    public string Type { get; set; } = "Training";
}
