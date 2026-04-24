using HSAcademia.Application.DTOs.Attendance;

namespace HSAcademia.Application.Interfaces;

public interface IAttendanceService
{
    // ── Existing: date-based roll call ──
    Task<List<StudentAttendanceDto>> GetAttendanceByCategoryAndDateAsync(Guid academyId, Guid categoryId, DateTime date);
    Task MarkAttendanceAsync(Guid academyId, Guid categoryId, MarkAttendanceDto dto);

    // ── Phase 2: event-linked roll call ──

    /// <summary>
    /// Returns the attendance list for a specific event.
    /// Throws InvalidOperationException if the 15-minute window hasn't opened yet.
    /// </summary>
    Task<List<StudentAttendanceDto>> GetAttendanceByEventAsync(Guid academyId, Guid eventId);

    /// <summary>
    /// Saves attendance records linked to a calendar event.
    /// Enforces the 15-minute pre-start window rule.
    /// </summary>
    Task MarkAttendanceByEventAsync(Guid academyId, MarkAttendanceByEventDto dto);

    // ── Phase 2: analytics ──

    /// <summary>
    /// Returns monthly attendance percentage per student in a category.
    /// Flags IsAlert = true when percentage is below 70%.
    /// </summary>
    Task<AttendanceMetricsSummaryDto> GetMonthlyMetricsAsync(Guid academyId, Guid categoryId, int year, int month);
}
