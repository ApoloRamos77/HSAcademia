using HSAcademia.Application.DTOs.SuperAdmin;
using HSAcademia.Infrastructure.Services;
using HSAcademia.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

[ApiController]
[Route("api/super-admin")]
[Authorize(Roles = "SuperAdmin")]
public class SuperAdminController : ControllerBase
{
    private readonly SuperAdminService _service;
    private readonly ILogger<SuperAdminController> _logger;

    public SuperAdminController(SuperAdminService service, ILogger<SuperAdminController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value ?? Guid.Empty.ToString());

    // =====================================================================
    // Dashboard
    // =====================================================================

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var stats = await _service.GetDashboardStatsAsync();
        return Ok(stats);
    }

    // =====================================================================
    // Registration Requests
    // =====================================================================

    [HttpGet("registration-requests")]
    public async Task<IActionResult> GetRequests([FromQuery] string? status = null)
    {
        RegistrationRequestStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<RegistrationRequestStatus>(status, true, out var parsed))
            statusFilter = parsed;

        var list = await _service.GetRegistrationRequestsAsync(statusFilter);
        return Ok(list);
    }

    [HttpGet("registration-requests/{id:guid}")]
    public async Task<IActionResult> GetRequest(Guid id)
    {
        var result = await _service.GetRegistrationRequestByIdAsync(id);
        if (!result.IsSuccess) return NotFound(new { message = result.Error });
        return Ok(result.Value);
    }

    [HttpPut("registration-requests/{id:guid}")]
    public async Task<IActionResult> EditRequest(Guid id, [FromBody] EditRegistrationRequestDto dto)
    {
        var result = await _service.EditRegistrationRequestAsync(id, dto);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return Ok(new { message = "Solicitud actualizada correctamente." });
    }

    [HttpPost("registration-requests/{id:guid}/approve")]
    public async Task<IActionResult> ApproveRequest(Guid id, [FromBody] ApproveRequestDto dto)
    {
        var result = await _service.ApproveRegistrationRequestAsync(id, dto.ReviewNotes ?? "", CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return Ok(result.Value);
    }

    [HttpPost("registration-requests/{id:guid}/reject")]
    public async Task<IActionResult> RejectRequest(Guid id, [FromBody] RejectRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { message = "El motivo de rechazo es obligatorio." });

        var result = await _service.RejectRegistrationRequestAsync(id, dto.Reason, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return Ok(new { message = "Solicitud rechazada." });
    }

    // =====================================================================
    // Academies
    // =====================================================================

    [HttpGet("academies")]
    public async Task<IActionResult> GetAcademies([FromQuery] string? status = null)
    {
        var list = await _service.GetAcademiesAsync(status);
        return Ok(list);
    }

    [HttpGet("academies/{id:guid}")]
    public async Task<IActionResult> GetAcademy(Guid id)
    {
        var result = await _service.GetAcademyByIdAsync(id);
        if (!result.IsSuccess) return NotFound(new { message = result.Error });
        return Ok(result.Value);
    }

    [HttpPut("academies/{id:guid}")]
    public async Task<IActionResult> EditAcademy(Guid id, [FromBody] EditAcademyDto dto)
    {
        var result = await _service.EditAcademyAsync(id, dto);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return Ok(new { message = "Academia actualizada correctamente." });
    }

    [HttpPost("academies/{id:guid}/suspend")]
    public async Task<IActionResult> SuspendAcademy(Guid id, [FromBody] SuspendAcademyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { message = "El motivo de suspensión es obligatorio." });

        var result = await _service.SuspendAcademyAsync(id, dto.Reason, CurrentUserId);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return Ok(new { message = "Academia suspendida." });
    }

    [HttpPost("academies/{id:guid}/reactivate")]
    public async Task<IActionResult> ReactivateAcademy(Guid id)
    {
        var result = await _service.ReactivateAcademyAsync(id);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return Ok(new { message = "Academia reactivada." });
    }

    [HttpDelete("academies/{id:guid}")]
    public async Task<IActionResult> DeactivateAcademy(Guid id, [FromBody] DeactivateAcademyDto dto)
    {
        var result = await _service.DeactivateAcademyAsync(id, dto.Reason);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return Ok(new { message = "Academia dada de baja permanentemente." });
    }

    // =====================================================================
    // Users
    // =====================================================================

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] Guid? academyId = null, [FromQuery] string? role = null)
    {
        var list = await _service.GetUsersAsync(academyId, role);
        return Ok(list);
    }

    [HttpPost("users/{id:guid}/suspend")]
    public async Task<IActionResult> SuspendUser(Guid id, [FromBody] SuspendUserDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { message = "El motivo es obligatorio." });

        var result = await _service.SuspendUserAsync(id, dto.Reason);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return Ok(new { message = "Usuario suspendido." });
    }

    [HttpPost("users/{id:guid}/reactivate")]
    public async Task<IActionResult> ReactivateUser(Guid id)
    {
        var result = await _service.ReactivateUserAsync(id);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return Ok(new { message = "Usuario reactivado." });
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeactivateUser(Guid id, [FromBody] DeactivateUserDto dto)
    {
        var result = await _service.DeactivateUserAsync(id, dto.Reason);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });
        return Ok(new { message = "Usuario dado de baja permanentemente." });
    }
}
