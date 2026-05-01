using System;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Configuration;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

[ApiController]
[Route("api/academy-config")]
[Authorize(Roles = "AcademyAdmin,Staff")]
public class AcademyConfigurationController : ControllerBase
{
    private readonly AcademyConfigurationService _service;

    public AcademyConfigurationController(AcademyConfigurationService service)
    {
        _service = service;
    }

    private Guid GetAcademyId()
    {
        var idStr = User.FindFirst("academyId")?.Value;
        return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
    }

    // Headquarter endpoints
    [HttpGet("headquarters")]
    public async Task<IActionResult> GetHeadquarters()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _service.GetHeadquartersAsync(academyId));
    }

    [HttpPost("headquarters")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> CreateHeadquarter([FromBody] CreateHeadquarterDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _service.CreateHeadquarterAsync(academyId, dto));
    }

    [HttpPut("headquarters/{id}")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> UpdateHeadquarter(Guid id, [FromBody] CreateHeadquarterDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try { return Ok(await _service.UpdateHeadquarterAsync(academyId, id, dto)); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("headquarters/{id}")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> DeleteHeadquarter(Guid id)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        await _service.DeleteHeadquarterAsync(academyId, id);
        return NoContent();
    }

    // Category endpoints
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();

        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userId = Guid.TryParse(userIdStr, out var uId) ? uId : (Guid?)null;
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";

        return Ok(await _service.GetCategoriesAsync(academyId, userId, userRole));
    }

    [HttpPost("categories")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try { return Ok(await _service.CreateCategoryAsync(academyId, dto)); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("categories/{id}")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] CreateCategoryDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try { return Ok(await _service.UpdateCategoryAsync(academyId, id, dto)); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // Role endpoints
    [HttpGet("roles")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> GetRoles()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _service.GetRolesAsync(academyId));
    }

    [HttpPost("roles")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> CreateRole([FromBody] CreateAcademyRoleDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _service.CreateRoleAsync(academyId, dto));
    }

    [HttpPut("roles/{id}")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] CreateAcademyRoleDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try { return Ok(await _service.UpdateRoleAsync(academyId, id, dto)); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // User endpoints
    [HttpGet("users")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> GetUsers()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _service.GetUsersAsync(academyId));
    }

    [HttpPost("users")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> CreateUser([FromBody] CreateAcademyUserDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try { return Ok(await _service.CreateUserAsync(academyId, dto)); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("users/{id}")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] CreateAcademyUserDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try { return Ok(await _service.UpdateUserAsync(academyId, id, dto)); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("users/{id}/reset-password")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> ResetPassword(Guid id)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try 
        { 
            await _service.ResetUserPasswordAsync(academyId, id, "123456"); 
            return Ok(new { message = "Contraseña reseteada correctamente." });
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }
}
