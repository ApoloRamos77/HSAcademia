using HSAcademia.Application.DTOs.Attendance;
using HSAcademia.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    private Guid GetAcademyId()
    {
        var idStr = User.FindFirst("academyId")?.Value;
        return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
    }

    private Guid? GetStudentId()
    {
        var idStr = User.FindFirst("studentId")?.Value;
        return Guid.TryParse(idStr, out var id) ? id : null;
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Phase 1 â€” Date-based roll call
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// GET /api/attendance/category/{categoryId}?date=2026-04-24
    /// Returns all students in the category with their attendance status for the given date.
    /// </summary>
    [HttpGet("category/{categoryId}")]
    public async Task<IActionResult> GetAttendanceByCategory(Guid categoryId, [FromQuery] DateTime date)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try
        {
            var result = await _attendanceService.GetAttendanceByCategoryAndDateAsync(academyId, categoryId, date);
            return Ok(result);
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// POST /api/attendance/category/{categoryId}
    /// Saves bulk attendance for a date (no event required).
    /// </summary>
    [HttpPost("category/{categoryId}")]
    public async Task<IActionResult> MarkAttendance(Guid categoryId, [FromBody] MarkAttendanceDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try
        {
            await _attendanceService.MarkAttendanceAsync(academyId, categoryId, dto);
            return Ok(new { message = "Asistencia guardada correctamente." });
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Phase 2 â€” Event-linked roll call (15-minute window rule)
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// GET /api/attendance/event/{eventId}
    /// Returns the attendance list for a specific calendar event.
    /// Returns 409 Conflict if the 15-minute window hasn't opened yet.
    /// </summary>
    [HttpGet("event/{eventId}")]
    public async Task<IActionResult> GetAttendanceByEvent(Guid eventId)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try
        {
            var result = await _attendanceService.GetAttendanceByEventAsync(academyId, eventId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// POST /api/attendance/event
    /// Saves bulk attendance linked to a calendar event.
    /// Returns 409 Conflict if the 15-minute window hasn't opened.
    /// </summary>
    [HttpPost("event")]
    public async Task<IActionResult> MarkAttendanceByEvent([FromBody] MarkAttendanceByEventDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try
        {
            await _attendanceService.MarkAttendanceByEventAsync(academyId, dto);
            return Ok(new { message = "Asistencia guardada correctamente para el evento." });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Phase 2 â€” Analytics: monthly attendance metrics
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// GET /api/attendance/metrics/category/{categoryId}?year=2026&amp;month=4
    /// Returns monthly attendance percentage per student.
    /// IsAlert = true when percentage &lt; 70%.
    /// </summary>
    [HttpGet("metrics/category/{categoryId}")]
    public async Task<IActionResult> GetMonthlyMetrics(
        Guid categoryId,
        [FromQuery] int year,
        [FromQuery] int month)
    {
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
            return BadRequest(new { message = "AÃ±o o mes invÃ¡lidos." });

        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try
        {
            var result = await _attendanceService.GetMonthlyMetricsAsync(academyId, categoryId, year, month);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Phase 3 â€” Mobile App endpoints
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// POST /api/attendance/scan
    /// Coach scans a student QR â†’ registers attendance as Present for today.
    /// Returns 409 if already marked today, 404 if student not found.
    /// </summary>
    [HttpPost("scan")]
    public async Task<IActionResult> ScanQr([FromBody] QrScanDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try
        {
            var result = await _attendanceService.ScanQrAsync(academyId, dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            // Already scanned today â†’ 409 Conflict
            return Conflict(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/attendance/my-history?months=3
    /// Returns the authenticated student's attendance grouped by month.
    /// Resolves studentId from the JWT "studentId" claim.
    /// </summary>
    [HttpGet("my-history")]
    public async Task<IActionResult> GetMyHistory([FromQuery] int months = 60, [FromQuery] Guid? studentId = null)
    {
        months = Math.Clamp(months, 1, 60);
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();

        // Primary: provided via query (e.g. Guardian viewing specific student)
        var targetStudentId = studentId;

        // Fallback: embedded in token
        if (targetStudentId is null)
        {
            targetStudentId = GetStudentId();
        }

        // Fallback: look up via userId (NameIdentifier)
        if (targetStudentId is null)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
                return BadRequest(new { message = "Token inválido." });

            var found = await _attendanceService.ResolveStudentIdAsync(academyId, userId);
            if (found is null)
                return NotFound(new { message = "No se encontró un alumno vinculado a tu usuario." });
            targetStudentId = found;
        }

        try
        {
            var result = await _attendanceService.GetMyAttendanceHistoryAsync(
                academyId, targetStudentId.Value, months);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex)            { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// GET /api/attendance/my-summary
    /// Returns a compact present/absent/justified/total for the current month.
    /// Used by the mobile dashboard widget.
    /// </summary>
    [HttpGet("my-summary")]
    public async Task<IActionResult> GetMySummary()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();

        var studentId = GetStudentId();
        if (studentId is null)
            return BadRequest(new { message = "El token no contiene un studentId vÃ¡lido." });

        try
        {
            var result = await _attendanceService.GetMyAttendanceSummaryAsync(
                academyId, studentId.Value);
            return Ok(result);
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }
    // ─────────────────────────────────────────────────────────────
    // Staff Mobile — lista y registro de asistencia
    // ─────────────────────────────────────────────────────────────

    [HttpGet("mobile/my-students")]
    [Authorize(Roles = "Staff,AcademyAdmin")]
    public async Task<IActionResult> GetMyStudentsAttendance(
        [FromQuery] DateTime? date = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? eventId = null,
        [FromQuery] Guid? headquarterId = null)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized(new { message = "Token inválido." });
        var targetDate = date?.Date ?? DateTime.UtcNow.Date;
        try
        {
            var result = await _attendanceService.GetMyStudentsAttendanceAsync(
                academyId, userId, targetDate, categoryId, eventId, headquarterId);
            return Ok(result);
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("mobile/my-students")]
    [Authorize(Roles = "Staff,AcademyAdmin")]
    public async Task<IActionResult> SaveMyStudentsAttendance([FromBody] MarkAttendanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized(new { message = "Token inválido." });
        try
        {
            await _attendanceService.SaveMyStudentsAttendanceAsync(academyId, userId, dto);
            return Ok(new { message = "Asistencia guardada correctamente." });
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── Attendance Closure ──────────────────────────────────────

    [HttpPost("events/{eventId}/close")]
    [Authorize(Roles = "Staff,AcademyAdmin")]
    public async Task<IActionResult> CloseAttendance(Guid eventId)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();
        try
        {
            await _attendanceService.CloseAttendanceAsync(academyId, eventId, userId);
            return Ok(new { message = "Asistencia cerrada correctamente." });
        }
        catch (KeyNotFoundException ex)     { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (Exception ex)                { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("events/{eventId}/reopen")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> ReopenAttendance(Guid eventId)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try
        {
            await _attendanceService.ReopenAttendanceAsync(academyId, eventId);
            return Ok(new { message = "Asistencia reabierta correctamente." });
        }
        catch (KeyNotFoundException ex)     { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (Exception ex)                { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("mobile/my-training-history")]
    [Authorize(Roles = "Staff,AcademyAdmin")]
    public async Task<IActionResult> GetStaffTrainingHistory([FromQuery] int months = 6)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();
        try
        {
            var result = await _attendanceService.GetStaffTrainingHistoryAsync(academyId, userId, months);
            return Ok(result);
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── Admin: Staff Attendance ──

    [HttpGet("staff")]
    [Authorize(Roles = "AcademyAdmin,SuperAdmin")]
    public async Task<IActionResult> GetStaffAttendance([FromQuery] DateTime date)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        var data = await _attendanceService.GetStaffAttendanceAsync(academyId, date);
        return Ok(data);
    }

    [HttpPost("staff")]
    [Authorize(Roles = "AcademyAdmin,SuperAdmin")]
    public async Task<IActionResult> SaveStaffAttendance([FromBody] MarkStaffAttendanceDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        await _attendanceService.SaveStaffAttendanceAsync(academyId, dto);
        return Ok(new { message = "Asistencia de staff guardada correctamente." });
    }
}
