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
        return Ok(await _studentService.GetStudentsAsync(academyId));
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
}
