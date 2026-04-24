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

    // ─────────────────────────────────────────────────────────────
    // Phase 1 — Date-based roll call
    // ─────────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────────
    // Phase 2 — Event-linked roll call (15-minute window rule)
    // ─────────────────────────────────────────────────────────────

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
            // Window not open yet
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

    // ─────────────────────────────────────────────────────────────
    // Phase 2 — Analytics: monthly attendance metrics
    // ─────────────────────────────────────────────────────────────

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
            return BadRequest(new { message = "Año o mes inválidos." });

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
}
