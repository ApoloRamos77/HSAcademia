using HSAcademia.Application.DTOs.Auth;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth) => _auth = auth;

    /// <summary>Login para todos los roles (SuperAdmin, AcademyAdmin, Staff)</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _auth.LoginAsync(dto);
        if (!result.IsSuccess) return Unauthorized(new { message = result.Error });

        return Ok(result.Value);
    }
}
