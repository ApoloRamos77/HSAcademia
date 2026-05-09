using System;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Student;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

[ApiController]
[Route("api/students")]
[Authorize(Roles = "AcademyAdmin,Staff")]
public class StudentController : ControllerBase
{
    private readonly StudentService _studentService;

    public StudentController(StudentService studentService)
    {
        _studentService = studentService;
    }

    private Guid GetAcademyId()
    {
        var idStr = User.FindFirst("academyId")?.Value;
        return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
    }

    [HttpGet]
    public async Task<IActionResult> GetStudents()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();

        var userIdStr = User.FindFirst("userId")?.Value;
        var userId = Guid.TryParse(userIdStr, out var uId) ? uId : (Guid?)null;
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";

        return Ok(await _studentService.GetStudentsAsync(academyId, userId, userRole));
    }

    [HttpPost]
    public async Task<IActionResult> CreateStudent([FromBody] CreateStudentDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try 
        { 
            return Ok(await _studentService.CreateStudentAsync(academyId, dto)); 
        }
        catch (Exception ex) 
        { 
            return BadRequest(new { message = ex.Message }); 
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateStudent(Guid id, [FromBody] CreateStudentDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try 
        { 
            return Ok(await _studentService.UpdateStudentAsync(academyId, id, dto)); 
        }
        catch (Exception ex) 
        { 
            return BadRequest(new { message = ex.Message }); 
        }
    }

    [HttpGet("{id}/medical-record")]
    public async Task<IActionResult> GetMedicalRecord(Guid id)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _studentService.GetStudentMedicalRecordAsync(academyId, id));
    }

    [HttpGet("{id}/nutrition-records")]
    public async Task<IActionResult> GetNutritionRecords(Guid id)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _studentService.GetStudentNutritionRecordsAsync(academyId, id));
    }

    [HttpPost("{id}/nutrition-records")]
    public async Task<IActionResult> AddNutritionRecord(Guid id, [FromBody] CreateStudentNutritionRecordDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();

        var userIdStr = User.FindFirst("userId")?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

        try
        {
            var record = await _studentService.AddStudentNutritionRecordAsync(academyId, id, dto, userId);
            return Ok(record);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
