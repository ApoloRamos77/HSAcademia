using HSAcademia.Application.DTOs.Auth;
using HSAcademia.Infrastructure.Persistence;
using HSAcademia.Infrastructure.Services;
using HSAcademia.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly AppDbContext _db;

    public AuthController(AuthService auth, AppDbContext db)
    {
        _auth = auth;
        _db = db;
    }

    /// <summary>Login para todos los roles (SuperAdmin, AcademyAdmin, Staff)</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _auth.LoginAsync(dto);
        if (!result.IsSuccess) return Unauthorized(new { message = result.Error });

        return Ok(result.Value);
    }

    /// <summary>Lista pública de academias activas para el selector del login móvil</summary>
    [AllowAnonymous]
    [HttpGet("academies")]
    public async Task<IActionResult> GetPublicAcademies([FromQuery] string? search = null)
    {
        var query = _db.Academies.IgnoreQueryFilters()
            .Where(a => a.Status == AcademyStatus.Active && !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a => a.Name.ToLower().Contains(search.ToLower()));

        var academies = await query
            .OrderBy(a => a.Name)
            .Select(a => new
            {
                id = a.Id,
                tenantId = a.AcademyId,
                name = a.Name,
                city = a.City,
                sport = a.Sport
            })
            .Take(50)
            .ToListAsync();

        return Ok(academies);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

        var result = await _auth.ChangePasswordAsync(userId, dto);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });

        return Ok(new { message = "Contraseña actualizada exitosamente." });
    }
}
