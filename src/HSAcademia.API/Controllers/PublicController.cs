using HSAcademia.Application.DTOs.SuperAdmin;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

/// <summary>
/// Public endpoint — no auth required — for academy onboarding registration form.
/// </summary>
[ApiController]
[Route("api/public")]
public class PublicController : ControllerBase
{
    private readonly SuperAdminService _service;

    public PublicController(SuperAdminService service) => _service = service;

    /// <summary>Submit a new academy registration request (public form)</summary>
    [HttpPost("registration-request")]
    public async Task<IActionResult> SubmitRequest([FromBody] SubmitRegistrationRequestDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _service.SubmitRegistrationRequestAsync(dto);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });

        return Ok(new { message = "Solicitud enviada exitosamente. El equipo de ADHSOFT SPORT la revisará en breve.", requestId = result.Value });
    }
}
