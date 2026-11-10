using HSAcademia.API.Authorization;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "AcademyAdmin,Staff")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboard;
    private readonly AccessScopeService _scope;

    public DashboardController(DashboardService dashboard, AccessScopeService scope)
    {
        _dashboard = dashboard;
        _scope = scope;
    }

    /// <summary>GET /api/dashboard — indicadores del panel principal de la academia.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var academyId = User.GetAcademyId();
        var userId = User.GetUserId();
        if (academyId == Guid.Empty || userId == Guid.Empty) return Unauthorized();

        var isAdmin = User.IsInRole("AcademyAdmin");
        var scope = await _scope.CurrentAsync();
        var canSeeFinances = isAdmin || (scope.Has(Permissions.FinancesSummary) && scope.IsAcademyWide);

        return Ok(await _dashboard.GetAcademyDashboardAsync(academyId, userId, isAdmin, canSeeFinances));
    }
}
