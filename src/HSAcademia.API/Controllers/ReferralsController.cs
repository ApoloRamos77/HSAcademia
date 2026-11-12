using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HSAcademia.API.Authorization;
using HSAcademia.Infrastructure.Persistence;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.API.Controllers;

/// <summary>Programa de referidos: campañas, registro, anulación e indicadores.</summary>
[ApiController]
[Route("api/referrals")]
[Authorize(Roles = "AcademyAdmin,Staff")]
public class ReferralsController : ControllerBase
{
    private readonly ReferralService _referrals;
    private readonly AppDbContext _db;

    public ReferralsController(ReferralService referrals, AppDbContext db)
    {
        _referrals = referrals;
        _db = db;
    }

    public sealed class AnnulRequest { public string? Reason { get; set; } }

    // ── Campañas ──
    [HttpGet("campaigns")]
    [RequirePermission(Permissions.FinancesConfig, Permissions.CollectionsView, Permissions.StudentsEdit)]
    public Task<IActionResult> Campaigns() => Run(a => _referrals.CampaignsAsync(a));

    /// <summary>Campaña vigente hoy (o null): la usa el formulario de inscripción.</summary>
    [HttpGet("campaigns/current")]
    [RequirePermission(Permissions.StudentsEdit, Permissions.CollectionsCollect, Permissions.FinancesConfig)]
    public Task<IActionResult> Current() => Run(async a => (object?)await _referrals.CurrentCampaignAsync(a) ?? new { none = true });

    [HttpPost("campaigns")]
    [RequirePermission(Permissions.FinancesConfig, AcademyWide = true)]
    public Task<IActionResult> CreateCampaign([FromBody] ReferralService.SaveCampaignDto dto) => Run(a => _referrals.SaveCampaignAsync(a, null, dto));

    [HttpPut("campaigns/{id:guid}")]
    [RequirePermission(Permissions.FinancesConfig, AcademyWide = true)]
    public Task<IActionResult> UpdateCampaign(Guid id, [FromBody] ReferralService.SaveCampaignDto dto) => Run(a => _referrals.SaveCampaignAsync(a, id, dto));

    [HttpDelete("campaigns/{id:guid}")]
    [RequirePermission(Permissions.FinancesConfig, AcademyWide = true)]
    public Task<IActionResult> DeleteCampaign(Guid id) => Run(async a => { await _referrals.DeleteCampaignAsync(a, id); return new { ok = true }; });

    // ── Referidos ──
    [HttpGet]
    [RequirePermission(Permissions.FinancesConfig, Permissions.CollectionsView, AcademyWide = true)]
    public Task<IActionResult> List([FromQuery] Guid? campaignId, [FromQuery] string? status) =>
        Run(a => _referrals.ListAsync(a, campaignId, status, null));

    /// <summary>Referidos en los que participa un alumno (como referente o como referido).</summary>
    [HttpGet("student/{studentId:guid}")]
    [RequirePermission(Permissions.StudentsPayments, Permissions.CollectionsView, Permissions.CollectionsCollect)]
    [RequireScope(ScopeTarget.Student, "studentId", Permissions.CollectionsView, Permissions.CollectionsCollect)]
    public Task<IActionResult> ForStudent(Guid studentId) => Run(a => _referrals.ListAsync(a, null, null, studentId));

    [HttpGet("summary")]
    [RequirePermission(Permissions.FinancesConfig, Permissions.CollectionsView, AcademyWide = true)]
    public Task<IActionResult> Summary([FromQuery] Guid? campaignId) => Run(a => _referrals.SummaryAsync(a, campaignId));

    /// <summary>Registra a un alumno nuevo como referido de otro (antes de su primer cobro).</summary>
    [HttpPost]
    [RequirePermission(Permissions.StudentsEdit, Permissions.CollectionsCollect)]
    public async Task<IActionResult> Register([FromBody] ReferralService.RegisterReferralDto dto)
    {
        if (!await ScopeChecker.IsAllowedAsync(HttpContext, ScopeTarget.Student, dto.ReferredStudentId, Permissions.StudentsEdit, Permissions.CollectionsCollect))
            return ScopeChecker.Denied();
        return await Run(async a => await _referrals.RegisterAsync(a, dto, await ActorAsync()));
    }

    [HttpPost("{id:guid}/annul")]
    [RequirePermission(Permissions.FinancesConfig, AcademyWide = true)]
    public Task<IActionResult> Annul(Guid id, [FromBody] AnnulRequest dto) => Run(a => _referrals.AnnulAsync(a, id, dto?.Reason));

    /// <summary>Recalcula y aplica los beneficios pendientes de toda la academia.</summary>
    [HttpPost("sync")]
    [RequirePermission(Permissions.FinancesConfig, AcademyWide = true)]
    public Task<IActionResult> Sync() => Run(async a => { await _referrals.SyncAsync(a); return new { ok = true }; });

    private async Task<string> ActorAsync()
    {
        var id = User.GetUserId();
        return await _db.Users.Where(u => u.Id == id).Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync() ?? "—";
    }

    private async Task<IActionResult> Run<T>(Func<Guid, Task<T>> action)
    {
        var academyId = User.GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try { return Ok(await action(academyId)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (Exception ex) when (ex is not DbUpdateException) { return BadRequest(new { message = ex.Message }); }
    }
}
