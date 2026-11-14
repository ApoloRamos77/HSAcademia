using HSAcademia.API.Authorization;
using HSAcademia.Application.DTOs.Calendar;
using HSAcademia.Infrastructure.Persistence;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.API.Controllers;

/// <summary>Torneos para la app de la familia: sus torneos, resultados y autorización.</summary>
[ApiController]
[Route("api/tournaments/mobile")]
[Authorize(Roles = "Student,Guardian")]
public class TournamentsMobileController : ControllerBase
{
    private readonly TournamentComplianceService _compliance;
    private readonly AccessControlService _access;

    public TournamentsMobileController(TournamentComplianceService compliance, AccessControlService access)
    {
        _compliance = compliance;
        _access = access;
    }

    /// <summary>GET /api/tournaments/mobile/mine?studentId=… — torneos en los que está el alumno (o los de la familia).</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] Guid? studentId)
    {
        var academyId = User.GetAcademyId();
        var userId = User.GetUserId();
        if (studentId.HasValue && !await _access.UserOwnsStudentAsync(academyId, userId, studentId.Value))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "No tienes acceso a este alumno." });
        return Ok(await _compliance.MyTournamentsAsync(academyId, userId, studentId));
    }

    /// <summary>POST /api/tournaments/mobile/{id}/authorize — el apoderado acepta la autorización del torneo.</summary>
    [HttpPost("{id:guid}/authorize")]
    public async Task<IActionResult> Authorize(Guid id, [FromBody] AuthorizeTournamentDto dto, [FromServices] AppDbContext db)
    {
        var academyId = User.GetAcademyId();
        var userId = User.GetUserId();
        if (!await _access.UserOwnsStudentAsync(academyId, userId, dto.StudentId))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "No puedes autorizar a este alumno." });
        try
        {
            var name = await db.Users.Where(u => u.Id == userId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync();
            await _compliance.AuthorizeAsync(academyId, id, dto.StudentId, "app", userId, name?.Trim(), null);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
