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

    // ── Phase 3: Mobile App endpoints ──

    /// <summary>
    /// Processes a QR code scan from the coach's mobile app.
    /// Registers the student as Present for the current day/session.
    /// Throws InvalidOperationException (409) if already marked today.
    /// Throws KeyNotFoundException (404) if student not found.
    /// </summary>
    Task<QrScanResultDto> ScanQrAsync(Guid academyId, QrScanDto dto);

    /// <summary>
    /// Returns the attendance history grouped by month for the authenticated student.
    /// Returns the last 3 months by default.
    /// </summary>
    Task<List<MobileAttendanceMonthDto>> GetMyAttendanceHistoryAsync(Guid academyId, Guid studentId, int months = 3);

    /// <summary>
    /// Returns a compact summary (present/justified/absent/total) for the current month,
    /// used by the dashboard widget.
    /// </summary>
    Task<MobileAttendanceSummaryDto> GetMyAttendanceSummaryAsync(Guid academyId, Guid studentId);

    // ── Staff Mobile: attendance management ──

    /// <summary>
    /// Returns students belonging to the Staff's assigned categories with their
    /// attendance status for the given date.
    /// </summary>
    Task<List<StudentAttendanceDto>> GetMyStudentsAttendanceAsync(Guid academyId, Guid staffUserId, DateTime date, Guid? categoryId = null, Guid? eventId = null, Guid? headquarterId = null);

    /// <summary>
    /// Saves attendance records for the Staff's students (validates category ownership).
    /// </summary>
    Task SaveMyStudentsAttendanceAsync(Guid academyId, Guid staffUserId, MarkAttendanceDto dto);

    /// <summary>Looks up the Student.Id linked to a given userId (Student.UserId or Student.GuardianId).</summary>
    Task<Guid?> ResolveStudentIdAsync(Guid academyId, Guid userId);

    /// <summary>Closes attendance for an event (Coach or Admin). Prevents further marking.</summary>
    Task CloseAttendanceAsync(Guid academyId, Guid eventId, Guid closedByUserId);

    /// <summary>Reopens a closed attendance session (Admin only).</summary>
    Task ReopenAttendanceAsync(Guid academyId, Guid eventId);

    /// <summary>Returns the training session history for a staff member's assigned categories.</summary>
    Task<List<StaffTrainingSessionDto>> GetStaffTrainingHistoryAsync(Guid academyId, Guid staffUserId, int months, Guid? headquarterId = null);
}
