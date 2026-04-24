namespace HSAcademia.Application.DTOs.Attendance;

/// <summary>
/// Monthly attendance metric per student in a category.
/// </summary>
public class AttendanceMetricDto
{
    public Guid StudentId { get; set; }
    public string FullName { get; set; } = string.Empty;

    /// <summary>Total sessions (events/dates) in the period.</summary>
    public int TotalSessions { get; set; }

    /// <summary>Sessions where the student was Present or Late.</summary>
    public int PresentCount { get; set; }

    /// <summary>Attendance percentage (0–100).</summary>
    public double AttendancePercent { get; set; }

    /// <summary>True when AttendancePercent &lt; 70.</summary>
    public bool IsAlert { get; set; }
}

/// <summary>
/// Summary payload returned by the metrics endpoint.
/// </summary>
public class AttendanceMetricsSummaryDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public List<AttendanceMetricDto> Students { get; set; } = new();
}
