using System;
using System.Security.Claims;
using System.Threading.Tasks;
using HSAcademia.API.Authorization;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

/// <summary>
/// Mobile-only endpoints for Student / Guardian self-service.
/// </summary>
[ApiController]
[Route("api/mobile/students")]
[Authorize(Roles = "Student,Guardian")]
public class MobileStudentController : ControllerBase
{
    private readonly StudentService _studentService;

    public MobileStudentController(StudentService studentService)
    {
        _studentService = studentService;
    }

    private Guid GetAcademyId()
    {
        var v = User.FindFirst("academyId")?.Value;
        return Guid.TryParse(v, out var id) ? id : Guid.Empty;
    }

    private Guid GetUserId()
    {
        var v = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(v, out var id) ? id : Guid.Empty;
    }

    /// <summary>
    /// POST /api/mobile/students/{studentId}/photo — el apoderado sube la foto de su hijo (o el alumno la suya).
    /// Misma validación que el personal: JPG, PNG o WEBP de hasta 3 MB, nombre aleatorio. Queda en la auditoría.
    /// </summary>
    [HttpPost("{studentId:guid}/photo")]
    [RequestSizeLimit(UploadStorage.MaxPhotoBytes + 64 * 1024)]
    public async Task<IActionResult> UploadPhoto(Guid studentId, Microsoft.AspNetCore.Http.IFormFile file, [FromServices] UploadStorage storage)
    {
        var academyId = GetAcademyId();
        var userId = GetUserId();
        if (academyId == Guid.Empty || userId == Guid.Empty) return Unauthorized();
        var (url, error) = await storage.SaveStudentPhotoAsync(file);
        if (error != null) return BadRequest(new { message = error });
        var (found, previous) = await _studentService.SetPhotoAsync(academyId, studentId, url, userId);
        if (!found) { storage.DeleteStudentPhoto(url); return NotFound(new { message = "Alumno no encontrado." }); }
        storage.DeleteStudentPhoto(previous);
        return Ok(new { photoUrl = url });
    }

    /// <summary>DELETE /api/mobile/students/{studentId}/photo — el apoderado quita la foto de su hijo.</summary>
    [HttpDelete("{studentId:guid}/photo")]
    public async Task<IActionResult> DeletePhoto(Guid studentId, [FromServices] UploadStorage storage)
    {
        var academyId = GetAcademyId();
        var userId = GetUserId();
        if (academyId == Guid.Empty || userId == Guid.Empty) return Unauthorized();
        var (found, previous) = await _studentService.SetPhotoAsync(academyId, studentId, null, userId);
        if (!found) return NotFound(new { message = "Alumno no encontrado." });
        storage.DeleteStudentPhoto(previous);
        return Ok(new { photoUrl = (string?)null });
    }

    /// <summary>GET /api/mobile/students/my-profile — Returns the student's personal + nutrition card.</summary>
    [HttpGet("my-profile")]
    public async Task<IActionResult> GetMyProfile()
    {
        var academyId = GetAcademyId();
        var userId    = GetUserId();

        if (academyId == Guid.Empty || userId == Guid.Empty)
            return Unauthorized();

        var profile = await _studentService.GetMyProfileAsync(academyId, userId);

        if (profile == null)
            return NotFound(new { message = "No se encontró el perfil del alumno." });

        return Ok(profile);
    }

    /// <summary>GET /api/mobile/students/my-students — Returns all students linked to the guardian.</summary>
    [HttpGet("my-students")]
    public async Task<IActionResult> GetMyStudents()
    {
        var academyId = GetAcademyId();
        var userId    = GetUserId();

        if (academyId == Guid.Empty || userId == Guid.Empty)
            return Unauthorized();

        var students = await _studentService.GetMyStudentsAsync(academyId, userId);
        return Ok(students);
    }

    /// <summary>GET /api/mobile/students/{studentId}/evaluations — evaluaciones deportivas visibles para la familia.</summary>
    [HttpGet("{studentId:guid}/evaluations")]
    public async Task<IActionResult> GetEvaluations(Guid studentId,
        [FromServices] AccessControlService access, [FromServices] EvaluationService evaluations)
    {
        var academyId = GetAcademyId();
        if (!await access.UserOwnsStudentAsync(academyId, GetUserId(), studentId))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "No tienes acceso a la información de este alumno." });
        return Ok(await evaluations.GetByStudentAsync(academyId, studentId, onlyVisibleToFamily: true));
    }

    /// <summary>GET /api/mobile/students/{studentId}/sports-summary — partidos, minutos, goles y asistencias.</summary>
    [HttpGet("{studentId:guid}/sports-summary")]
    public async Task<IActionResult> GetSportsSummary(Guid studentId,
        [FromServices] AccessControlService access, [FromServices] SportsService sports, [FromQuery] int? take = null)
    {
        var academyId = GetAcademyId();
        if (!await access.UserOwnsStudentAsync(academyId, GetUserId(), studentId))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "No tienes acceso a la información de este alumno." });
        return Ok(await sports.GetStudentSummaryAsync(academyId, studentId, take ?? 10));
    }
}
