using HSAcademia.Application.DTOs.SuperAdmin;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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

    /// <summary>
    /// GET /api/public/academies/{key} — datos públicos para la landing de una academia.
    /// key = slug, Id o TenantId. Solo información de contacto y categorías (sin datos de alumnos ni montos).
    /// </summary>
    [HttpGet("academies/{key}")]
    [EnableRateLimiting("public")]
    public async Task<IActionResult> GetAcademyLanding(string key, [FromServices] HSAcademia.Infrastructure.Persistence.AppDbContext db)
    {
        var isGuid = Guid.TryParse(key, out var id);
        var slug = key.Trim().ToLowerInvariant();

        var academy = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            db.Academies.Where(a => a.Status == HSAcademia.Domain.Enums.AcademyStatus.Active &&
                (a.SlugName == slug || (isGuid && (a.Id == id || a.AcademyId == id)))));
        if (academy == null) return NotFound(new { message = "Academia no encontrada." });

        var headquarters = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.Headquarters.Where(h => h.AcademyId == academy.AcademyId && h.IsActive)
              .OrderBy(h => h.Name)
              .Select(h => new { h.Name, h.Address, h.ContactPhone }));

        var categories = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.Categories.Where(c => c.AcademyId == academy.AcademyId && c.IsActive)
              .OrderBy(c => c.IsFree).ThenByDescending(c => c.StartDateOfBirth)
              .Select(c => new { c.Id, c.Name, c.StartDateOfBirth, c.EndDateOfBirth, c.IsFree, c.MinAge, Headquarter = c.Headquarter!.Name }));

        var phone = academy.ContactPhone ?? headquarters.Select(h => h.ContactPhone).FirstOrDefault(p => p != null);
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());

        return Ok(new
        {
            id = academy.Id,
            slug = academy.SlugName,
            name = academy.Name,
            description = academy.Description,
            logoUrl = academy.LogoUrl,
            sport = academy.Sport,
            city = academy.City,
            website = academy.Website,
            contact = new
            {
                phone,
                email = academy.ContactEmail,
                address = academy.Address ?? headquarters.Select(h => h.Address).FirstOrDefault(a => a != null),
                whatsapp = digits.Length == 9 ? "51" + digits : digits
            },
            headquarters = headquarters.Select(h => new { h.Name, h.Address }),
            categories = categories.Select(c => new
            {
                c.Id,
                c.Name,
                c.Headquarter,
                c.IsFree,
                birthYears = c.IsFree || c.StartDateOfBirth == null || c.EndDateOfBirth == null
                    ? (c.MinAge.HasValue ? $"Libre · desde {c.MinAge} años" : "Libre")
                    : c.StartDateOfBirth.Value.Year == c.EndDateOfBirth.Value.Year
                        ? $"{c.StartDateOfBirth.Value.Year}"
                        : $"{Math.Min(c.StartDateOfBirth.Value.Year, c.EndDateOfBirth.Value.Year)}–{Math.Max(c.StartDateOfBirth.Value.Year, c.EndDateOfBirth.Value.Year)}"
            })
        });
    }

    /// <summary>Submit a new academy registration request (public form)</summary>
    [HttpPost("registration-request")]
    [EnableRateLimiting("public")]
    public async Task<IActionResult> SubmitRequest([FromBody] SubmitRegistrationRequestDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _service.SubmitRegistrationRequestAsync(dto);
        if (!result.IsSuccess) return BadRequest(new { message = result.Error });

        return Ok(new { message = "Solicitud enviada exitosamente. El equipo de Clubio la revisará en breve.", requestId = result.Value });
    }
}
