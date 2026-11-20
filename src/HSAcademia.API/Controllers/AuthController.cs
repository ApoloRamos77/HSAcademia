using HSAcademia.Application.DTOs.Auth;
using HSAcademia.Infrastructure.Persistence;
using HSAcademia.Infrastructure.Services;
using HSAcademia.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    [EnableRateLimiting("login")]
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
    [EnableRateLimiting("public")]
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

    /// <summary>Permisos y alcance vigentes: permite actualizarlos sin volver a iniciar sesión.</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();
        var me = await _auth.GetCurrentAsync(userId);
        return me == null ? Unauthorized() : Ok(me);
    }

    /// <summary>Cada miembro del personal pone o cambia su propia foto (JPG, PNG o WEBP, máx. 3 MB).</summary>
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [HttpPost("me/photo")]
    [RequestSizeLimit(HSAcademia.API.Authorization.UploadStorage.MaxPhotoBytes + 64 * 1024)]
    public async Task<IActionResult> UploadMyPhoto(Microsoft.AspNetCore.Http.IFormFile file,
        [FromServices] HSAcademia.API.Authorization.UploadStorage storage, [FromServices] AcademyConfigurationService config)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId) || !Guid.TryParse(User.FindFirst("academyId")?.Value, out var academyId)) return Unauthorized();
        var (url, error) = await storage.SaveStaffPhotoAsync(file);
        if (error != null) return BadRequest(new { message = error });
        var (found, previous) = await config.SetStaffPhotoAsync(academyId, userId, url, userId);
        if (!found) { storage.DeleteStaffPhoto(url); return NotFound(new { message = "Usuario no encontrado." }); }
        storage.DeleteStaffPhoto(previous);
        return Ok(new { avatarUrl = url });
    }

    /// <summary>Cada miembro del personal quita su propia foto.</summary>
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [HttpDelete("me/photo")]
    public async Task<IActionResult> DeleteMyPhoto([FromServices] HSAcademia.API.Authorization.UploadStorage storage, [FromServices] AcademyConfigurationService config)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId) || !Guid.TryParse(User.FindFirst("academyId")?.Value, out var academyId)) return Unauthorized();
        var (found, previous) = await config.SetStaffPhotoAsync(academyId, userId, null, userId);
        if (!found) return NotFound(new { message = "Usuario no encontrado." });
        storage.DeleteStaffPhoto(previous);
        return Ok(new { avatarUrl = (string?)null });
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();
        var profile = await _auth.GetProfileAsync(userId);
        return profile == null ? Unauthorized() : Ok(profile);
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();
        var result = await _auth.UpdateProfileAsync(userId, dto);
        return result.IsSuccess ? Ok(new { message = "Perfil actualizado." }) : BadRequest(new { message = result.Error });
    }

    [Authorize]
    [HttpPost("change-password")]
    [EnableRateLimiting("login")]
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
