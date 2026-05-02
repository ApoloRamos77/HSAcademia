namespace HSAcademia.Application.DTOs.Attendance;

// ─────────────────────────────────────────────────────────────
// Mobile-specific DTOs — Fase 3 App Móvil HSAcademia
// ─────────────────────────────────────────────────────────────

/// <summary>
/// Payload sent by the mobile scanner when it reads a student QR code.
/// </summary>
public class QrScanDto
{
    /// <summary>Student's UserId embedded in the QR payload (field "u").</summary>
    public Guid StudentUserId { get; set; }

    /// <summary>ISO-8601 timestamp from the mobile device.</summary>
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Response returned after a successful QR scan.
/// </summary>
public class QrScanResultDto
{
    public Guid AttendanceId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = "Registrado";
    public DateTime MarkedAt { get; set; }
}

/// <summary>
/// Single attendance record for mobile timeline display.
/// </summary>
public class MobileAttendanceRecordDto
{
    public Guid AttendanceId { get; set; }

    /// <summary>Date in YYYY-MM-DD format (local).</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>"Presente" | "Ausente" | "Justificado" | "Tardanza"</summary>
    public string Status { get; set; } = string.Empty;

    public string? Category { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Monthly attendance grouped for the mobile history screen.
/// </summary>
public class MobileAttendanceMonthDto
{
    public string Month { get; set; } = string.Empty;      // e.g. "mayo 2026"
    public string YearMonth { get; set; } = string.Empty;  // e.g. "2026-05" — use for filtering
    public int Present { get; set; }
    public int Justified { get; set; }
    public int Absent { get; set; }
    public int Total { get; set; }
    public List<MobileAttendanceRecordDto> Records { get; set; } = new();
}

/// <summary>
/// Compact monthly summary for the dashboard widget.
/// </summary>
public class MobileAttendanceSummaryDto
{
    public int Present { get; set; }
    public int Justified { get; set; }
    public int Absent { get; set; }
    public int Total { get; set; }
}

/// <summary>Single training session shown in coach history.</summary>
public class StaffTrainingSessionDto
{
    public Guid EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;        // YYYY-MM-DD
    public string StartTime { get; set; } = string.Empty;   // HH:mm
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string HeadquarterName { get; set; } = string.Empty;
    public bool AttendanceClosed { get; set; }
    public DateTime? AttendanceClosedAt { get; set; }
    // Attendance counters (0 if not yet taken)
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int JustifiedCount { get; set; }
    public int TotalStudents { get; set; }
    public bool AttendanceTaken { get; set; }  // true if any record exists
}

/// <summary>Monthly group of staff training sessions.</summary>
public class StaffTrainingHistoryMonthDto
{
    public string Month { get; set; } = string.Empty;
    public List<StaffTrainingSessionDto> Sessions { get; set; } = new();
}
