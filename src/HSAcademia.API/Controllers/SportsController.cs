using HSAcademia.API.Authorization;
using HSAcademia.Application.DTOs.Calendar;
using HSAcademia.Application.DTOs.Student;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

/// <summary>Horarios recurrentes, resultados de partidos, estadísticas y evaluaciones deportivas.</summary>
[ApiController]
[Route("api/sports")]
[Authorize(Roles = "AcademyAdmin,Staff")]
public class SportsController : ControllerBase
{
    private readonly SportsService _sports;
    private readonly EvaluationService _evaluations;

    public SportsController(SportsService sports, EvaluationService evaluations)
    {
        _sports = sports;
        _evaluations = evaluations;
    }

    private bool IsAdmin => User.IsInRole("AcademyAdmin");

    // ── Horarios recurrentes ─────────────────────────────────────
    /// <summary>POST /api/sports/schedules — crea una serie de entrenamientos (rango de fechas + días de la semana).</summary>
    [HttpPost("schedules")]
    [RequirePermission(Permissions.ManageEvents)]
    public async Task<IActionResult> CreateSchedule([FromBody] CreateRecurringEventsDto dto) =>
        Ok(await _sports.CreateRecurringAsync(User.GetAcademyId(), dto));

    /// <summary>DELETE /api/sports/schedules/{seriesId} — elimina los entrenamientos FUTUROS de la serie sin asistencia.</summary>
    [HttpDelete("schedules/{seriesId:guid}")]
    [RequirePermission(Permissions.ManageEvents)]
    public async Task<IActionResult> DeleteSchedule(Guid seriesId)
    {
        var deleted = await _sports.DeleteFutureSeriesAsync(User.GetAcademyId(), seriesId);
        return Ok(new { deleted, message = $"Se eliminaron {deleted} entrenamiento(s) futuros de la serie." });
    }

    // ── Partidos ─────────────────────────────────────────────────
    [HttpGet("matches/{eventId:guid}")]
    public async Task<IActionResult> GetMatch(Guid eventId) =>
        Ok(await _sports.GetMatchResultAsync(User.GetAcademyId(), eventId));

    [HttpPut("matches/{eventId:guid}")]
    [RequirePermission(Permissions.ManageEvents)]
    public async Task<IActionResult> SaveMatch(Guid eventId, [FromBody] SaveMatchResultDto dto) =>
        Ok(await _sports.SaveMatchResultAsync(User.GetAcademyId(), eventId, dto));

    [HttpGet("students/{studentId:guid}/summary")]
    [RequirePermission(Permissions.ViewStudents, Permissions.ManageEvents)]
    public async Task<IActionResult> StudentSummary(Guid studentId) =>
        Ok(await _sports.GetStudentSummaryAsync(User.GetAcademyId(), studentId));

    // ── Evaluaciones ─────────────────────────────────────────────
    [HttpGet("students/{studentId:guid}/evaluations")]
    [RequirePermission(Permissions.ViewStudents)]
    public async Task<IActionResult> GetEvaluations(Guid studentId) =>
        Ok(await _evaluations.GetByStudentAsync(User.GetAcademyId(), studentId, onlyVisibleToFamily: false));

    [HttpPost("students/{studentId:guid}/evaluations")]
    [RequirePermission(Permissions.ViewStudents)]
    public async Task<IActionResult> CreateEvaluation(Guid studentId, [FromBody] SaveEvaluationDto dto) =>
        Ok(await _evaluations.CreateAsync(User.GetAcademyId(), User.GetUserId(), IsAdmin, studentId, dto));

    [HttpPut("evaluations/{id:guid}")]
    [RequirePermission(Permissions.ViewStudents)]
    public async Task<IActionResult> UpdateEvaluation(Guid id, [FromBody] SaveEvaluationDto dto) =>
        Ok(await _evaluations.UpdateAsync(User.GetAcademyId(), User.GetUserId(), IsAdmin, id, dto));

    [HttpDelete("evaluations/{id:guid}")]
    [RequirePermission(Permissions.ViewStudents)]
    public async Task<IActionResult> DeleteEvaluation(Guid id)
    {
        await _evaluations.DeleteAsync(User.GetAcademyId(), User.GetUserId(), IsAdmin, id);
        return NoContent();
    }
}
