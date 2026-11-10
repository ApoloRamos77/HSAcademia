using HSAcademia.API.Authorization;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

/// <summary>Avisos de la familia en su app.</summary>
[ApiController]
[Route("api/notices")]
[Authorize(Roles = "Student,Guardian")]
public class NoticesController : ControllerBase
{
    private readonly FamilyNoticeService _notices;

    public NoticesController(FamilyNoticeService notices) => _notices = notices;

    [HttpGet("mine")]
    public async Task<IActionResult> Mine() => Ok(await _notices.ListMineAsync(User.GetAcademyId(), User.GetUserId()));

    [HttpPost("mine/read")]
    public async Task<IActionResult> MarkRead()
    {
        await _notices.MarkAllReadAsync(User.GetAcademyId(), User.GetUserId());
        return NoContent();
    }
}
