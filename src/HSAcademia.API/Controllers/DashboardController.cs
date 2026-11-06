using HSAcademia.API.Authorization;
using HSAcademia.Infrastructure.Persistence;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "AcademyAdmin,Staff")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboard;
    private readonly AppDbContext _db;

    public DashboardController(DashboardService dashboard, AppDbContext db)
    {
        _dashboard = dashboard;
        _db = db;
    }

    /// <summary>GET /api/dashboard — indicadores del panel principal de la academia.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var academyId = User.GetAcademyId();
        var userId = User.GetUserId();
        if (academyId == Guid.Empty || userId == Guid.Empty) return Unauthorized();

        var isAdmin = User.IsInRole("AcademyAdmin");
        var canSeeFinances = isAdmin || await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.AcademyId == academyId && u.AcademyRole != null && u.AcademyRole.IsActive)
            .Select(u => u.AcademyRole!.Permissions)
            .FirstOrDefaultAsync() is { } perms && perms.Contains(Permissions.ManageFinances);

        return Ok(await _dashboard.GetAcademyDashboardAsync(academyId, userId, isAdmin, canSeeFinances));
    }
}
