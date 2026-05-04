using System;
using System.Security.Claims;
using System.Threading.Tasks;
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
}
