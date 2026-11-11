using HSAcademia.API.Authorization;
using HSAcademia.Infrastructure.Persistence;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.API.Controllers;

/// <summary>Cobro de alumnos por sesión o paquete: cobro en campo, liquidación del mes y resumen.</summary>
[ApiController]
[Route("api/finances/sessions")]
[Authorize]
public class SessionBillingController : ControllerBase
{
    private readonly SessionBillingService _sessions;

    public SessionBillingController(SessionBillingService sessions) => _sessions = sessions;

    public sealed class ChargeSessionRequest
    {
        public Guid StudentId { get; set; }
        public Guid? EventId { get; set; }
        public DateTime? Date { get; set; }
    }

    public sealed class LiquidateRequest
    {
        public int Year { get; set; }
        public int Month { get; set; }
    }

    /// <summary>Crea la deuda de la sesión (una sola vez por sesión); luego se cobra como cualquier deuda.</summary>
    [HttpPost("charge")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.CollectionsCollect)]
    public async Task<IActionResult> Charge([FromBody] ChargeSessionRequest dto)
    {
        if (!await ScopeChecker.IsAllowedAsync(HttpContext, ScopeTarget.Student, dto.StudentId, Permissions.CollectionsCollect))
            return ScopeChecker.Denied();
        try { return Ok(await _sessions.ChargeSessionAsync(User.GetAcademyId(), dto.StudentId, dto.EventId, dto.Date)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("liquidate")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.CollectionsGenerate, AcademyWide = true)]
    public async Task<IActionResult> Liquidate([FromBody] LiquidateRequest dto)
    {
        try { return Ok(await _sessions.LiquidateAsync(User.GetAcademyId(), dto.Year, dto.Month)); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Acumulado del mes de un alumno (personal).</summary>
    [HttpGet("summary/{studentId:guid}")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.StudentsPayments, Permissions.CollectionsView, Permissions.CollectionsCollect)]
    [RequireScope(ScopeTarget.Student, "studentId", Permissions.CollectionsView, Permissions.CollectionsCollect)]
    public async Task<IActionResult> Summary(Guid studentId, [FromQuery] int? year, [FromQuery] int? month)
    {
        var (y, m) = Period(year, month);
        return Ok((await _sessions.SummariesAsync(User.GetAcademyId(), new[] { studentId }, y, m)).FirstOrDefault());
    }

    /// <summary>Acumulado del mes de los hijos que pagan por sesión o paquete (familia).</summary>
    [HttpGet("my-summary")]
    [Authorize(Roles = "Guardian,Student")]
    public async Task<IActionResult> MySummary([FromQuery] int? year, [FromQuery] int? month, [FromServices] AppDbContext db)
    {
        var academyId = User.GetAcademyId();
        var userId = User.GetUserId();
        var ids = await db.Students.Where(s => s.AcademyId == academyId && s.IsActive && (s.GuardianId == userId || s.UserId == userId))
            .Select(s => s.Id).ToListAsync();
        var (y, m) = Period(year, month);
        return Ok(await _sessions.SummariesAsync(academyId, ids, y, m));
    }

    private static (int Year, int Month) Period(int? year, int? month)
    {
        var now = AcademyClock.Now;
        return year is >= 2000 and <= 2100 && month is >= 1 and <= 12 ? (year.Value, month.Value) : (now.Year, now.Month);
    }
}
