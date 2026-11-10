using HSAcademia.API.Authorization;
using HSAcademia.Application.DTOs.Attendance;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

/// <summary>Asistencia de entrenadores por sesión: administrador y coordinadores con permiso.</summary>
[ApiController]
[Route("api/staff-attendance")]
[Authorize(Roles = "AcademyAdmin,Staff")]
[RequirePermission(Permissions.StaffAttendanceMark)]
public class StaffAttendanceController : ControllerBase
{
    private readonly StaffAttendanceService _service;

    public StaffAttendanceController(StaffAttendanceService service) => _service = service;

    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions([FromQuery] DateTime? date = null) =>
        Ok(await _service.GetSessionsAsync(User.GetAcademyId(), User.GetUserId(), date?.Date ?? AcademyClock.Today));

    [HttpGet("trainers")]
    public async Task<IActionResult> Trainers() => Ok(await _service.GetTrainersAsync(User.GetAcademyId()));

    [HttpPost("sessions")]
    public async Task<IActionResult> Save([FromBody] SaveStaffSessionsDto dto)
    {
        try
        {
            await _service.SaveAsync(User.GetAcademyId(), User.GetUserId(), dto.Records);
            return Ok(new { message = "Asistencia guardada." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("report")]
    public async Task<IActionResult> Report([FromQuery] int year, [FromQuery] int month)
    {
        if (year < 2000 || year > 2100 || month < 1 || month > 12) return BadRequest(new { message = "Año o mes inválidos." });
        return Ok(await _service.MonthlyReportAsync(User.GetAcademyId(), User.GetUserId(), year, month));
    }
}
