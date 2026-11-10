using HSAcademia.API.Authorization;
using System;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Announcement;
using HSAcademia.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnnouncementController : ControllerBase
{
    private readonly IAnnouncementService _announcementService;

    public AnnouncementController(IAnnouncementService announcementService)
    {
        _announcementService = announcementService;
    }

    private Guid GetAcademyId()
    {
        var idStr = User.FindFirst("academyId")?.Value;
        return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
    }

    private Guid GetUserId()
    {
        var idStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
    }

    [HttpGet]
    public async Task<IActionResult> GetAnnouncements()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();

        var result = await _announcementService.GetAnnouncementsAsync(academyId);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.AnnouncementsPublish)]
    public async Task<IActionResult> CreateAnnouncement([FromBody] CreateAnnouncementDto dto)
    {
        var academyId = GetAcademyId();
        var userId = GetUserId();
        
        if (academyId == Guid.Empty || userId == Guid.Empty) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest(new { message = "El título y el mensaje son obligatorios." });
        if (dto.Title.Length > 150 || dto.Content.Length > 4000)
            return BadRequest(new { message = "El título admite hasta 150 caracteres y el mensaje hasta 4000." });

        var result = await _announcementService.CreateAnnouncementAsync(academyId, userId, dto);
        return Created("", result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.AnnouncementsPublish)]
    public async Task<IActionResult> DeleteAnnouncement(Guid id)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();

        try
        {
            await _announcementService.DeleteAnnouncementAsync(academyId, id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
