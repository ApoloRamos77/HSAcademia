using System;
using System.Threading.Tasks;
using HSAcademia.API.Authorization;
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
    private readonly ActivityService _activity;

    public AcademyConfigurationController(AcademyConfigurationService service, ActivityService activity)
    {
        _service = service;
        _activity = activity;
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
    [HttpGet("permissions")]
    [Authorize(Roles = "AcademyAdmin")]
    public IActionResult GetPermissionCatalog() => Ok(Permissions.Catalog);

    [HttpPost("roles/{id}/clone")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> CloneRole(Guid id)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try { return Ok(await _service.CloneRoleAsync(academyId, id)); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("roles/{id}/status")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> SetRoleStatus(Guid id, [FromBody] SetAcademyRoleStatusDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try { return Ok(await _service.SetRoleStatusAsync(academyId, id, dto.IsActive)); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("roles/{id}")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try { await _service.DeleteRoleAsync(academyId, id); return NoContent(); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

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

    // Encabezado de los recibos: todo el personal lo lee para imprimir; solo el administrador lo cambia.
    [HttpGet("receipt-profile")]
    public async Task<IActionResult> GetReceiptProfile()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try { return Ok(await _service.GetReceiptProfileAsync(academyId)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPut("receipt-profile")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> UpdateReceiptProfile([FromBody] ReceiptProfileDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try { return Ok(await _service.UpdateReceiptProfileAsync(academyId, dto)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // User endpoints
    [HttpGet("users")]
    [RequirePermission(Permissions.FinancesStaffPayments)]
    public async Task<IActionResult> GetUsers()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        // Quien lleva la planilla solo necesita al personal, no a las familias.
        return Ok(await _service.GetUsersAsync(academyId, staffOnly: !User.IsInRole("AcademyAdmin")));
    }

    [HttpPost("users")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> CreateUser([FromBody] CreateAcademyUserDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try { return Ok(await _service.CreateUserAsync(academyId, dto, User.GetUserId())); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("users/{id}")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] CreateAcademyUserDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try { return Ok(await _service.UpdateUserAsync(academyId, id, dto, User.GetUserId())); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("users/{id}/employment")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public async Task<IActionResult> UpdateEmployment(Guid id, [FromBody] UpdateEmploymentDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try { await _service.UpdateEmploymentAsync(academyId, id, dto); return NoContent(); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Bitácora de la academia, de lo más reciente a lo más antiguo.</summary>
    [HttpGet("activity")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> GetActivity(
        [FromQuery] string? module, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 30)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _activity.GetAsync(academyId, module, from, to, search, page, pageSize));
    }

    [HttpPost("users/{id}/transfer-ownership")]
    [Authorize(Roles = "AcademyAdmin")]
    public async Task<IActionResult> TransferOwnership(Guid id)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try { await _service.TransferOwnershipAsync(academyId, User.GetUserId(), id); return NoContent(); }
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
            var temporaryPassword = await _service.ResetUserPasswordAsync(academyId, id, User.GetUserId());
            return Ok(new
            {
                message = "Contraseña reseteada. Entrega la contraseña temporal al usuario: deberá cambiarla al ingresar.",
                temporaryPassword
            });
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }
}
