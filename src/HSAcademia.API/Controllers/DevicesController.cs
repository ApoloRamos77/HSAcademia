using HSAcademia.API.Authorization;
using HSAcademia.Domain.Entities;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.API.Controllers;

public sealed record DeviceTokenRequest(string? Token, string? Platform);

/// <summary>Registro de los celulares que reciben notificaciones.</summary>
[ApiController]
[Route("api/devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private const int MaxTokenLength = 512;

    private readonly AppDbContext _db;

    private readonly ILogger<DevicesController> _logger;

    public DevicesController(AppDbContext db, ILogger<DevicesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] DeviceTokenRequest request)
    {
        var token = request.Token?.Trim();
        if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength)
            return BadRequest(new { message = "Identificador de celular no válido." });

        var userId = User.GetUserId();
        var academyId = User.GetAcademyId();
        if (userId == Guid.Empty || academyId == Guid.Empty) return Unauthorized();

        // Un celular recibe solo lo de la última cuenta que ingresó en él.
        var device = await _db.DeviceTokens.FirstOrDefaultAsync(d => d.Token == token);
        if (device == null)
        {
            _db.DeviceTokens.Add(new DeviceToken { Token = token, UserId = userId, AcademyId = academyId, Platform = "android" });
        }
        else
        {
            device.UserId = userId;
            device.AcademyId = academyId;
            device.LastSeenAt = DateTime.UtcNow;
        }

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { /* la app lo envió dos veces a la vez: ya quedó registrado */ }
        _logger.LogInformation("Celular registrado para notificaciones. Usuario {UserId}, academia {AcademyId}, {State}.",
            userId, academyId, device == null ? "nuevo" : "ya conocido");
        return NoContent();
    }

    /// <summary>Al cerrar sesión el celular deja de recibir avisos de esa cuenta.</summary>
    [HttpPost("unregister")]
    public async Task<IActionResult> Unregister([FromBody] DeviceTokenRequest request)
    {
        var userId = User.GetUserId();
        await _db.DeviceTokens.Where(d => d.Token == request.Token && d.UserId == userId).ExecuteDeleteAsync();
        return NoContent();
    }
}
