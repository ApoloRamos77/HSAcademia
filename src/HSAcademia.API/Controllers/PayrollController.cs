using HSAcademia.API.Authorization;
using HSAcademia.Infrastructure.Persistence;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.API.Controllers;

/// <summary>Planilla del personal: tarifarios, liquidaciones por periodo, adelantos y "mis pagos".</summary>
[ApiController]
[Route("api/payroll")]
[Authorize(Roles = "AcademyAdmin,Staff")]
public class PayrollController : ControllerBase
{
    private readonly PayrollService _payroll;

    public PayrollController(PayrollService payroll) => _payroll = payroll;

    public sealed class PayRequest
    {
        public string Method { get; set; } = "Cash";
        public DateTime? PaidOn { get; set; }
    }

    private async Task<IActionResult> Run(Func<Task<object?>> action)
    {
        try { return Ok(await action()); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    private async Task<string> ActorAsync(AppDbContext db)
    {
        var id = User.GetUserId();
        return await db.Users.Where(u => u.Id == id).Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync() ?? "—";
    }

    // ── Tarifarios ───────────────────────────────────────────
    [HttpGet("scales")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public Task<IActionResult> Scales() => Run(async () => await _payroll.ScalesAsync(User.GetAcademyId()));

    [HttpPost("scales")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public Task<IActionResult> CreateScale([FromBody] PayrollService.ScaleInput input) =>
        Run(async () => await _payroll.SaveScaleAsync(User.GetAcademyId(), null, input));

    [HttpPut("scales/{id:guid}")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public Task<IActionResult> UpdateScale(Guid id, [FromBody] PayrollService.ScaleInput input) =>
        Run(async () => await _payroll.SaveScaleAsync(User.GetAcademyId(), id, input));

    [HttpDelete("scales/{id:guid}")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public Task<IActionResult> DeleteScale(Guid id) =>
        Run(async () => { await _payroll.DeleteScaleAsync(User.GetAcademyId(), id); return null; });

    // ── Cálculo y periodos ───────────────────────────────────
    [HttpGet("suggest-period")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public Task<IActionResult> SuggestPeriod([FromQuery] Guid staffId) =>
        Run(async () => await _payroll.SuggestPeriodAsync(User.GetAcademyId(), staffId));

    [HttpGet("preview")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public Task<IActionResult> Preview([FromQuery] Guid staffId, [FromQuery] DateTime start, [FromQuery] DateTime end) =>
        Run(async () => await _payroll.CalculateAsync(User.GetAcademyId(), staffId, start, end));

    // ── Liquidaciones ────────────────────────────────────────
    [HttpGet("payments")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public Task<IActionResult> List([FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] Guid? staffId) =>
        Run(async () => await _payroll.ListAsync(User.GetAcademyId(), from.Date, to.Date, staffId));

    [HttpGet("payments/{id:guid}")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public Task<IActionResult> Get(Guid id) => Run(async () => await _payroll.GetAsync(User.GetAcademyId(), id));

    [HttpPost("payments")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public Task<IActionResult> Create([FromBody] PayrollService.PaymentInput input) =>
        Run(async () => await _payroll.CreateAsync(User.GetAcademyId(), input));

    [HttpPut("payments/{id:guid}")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public Task<IActionResult> Update(Guid id, [FromBody] PayrollService.PaymentInput input) =>
        Run(async () => await _payroll.UpdateDraftAsync(User.GetAcademyId(), id, input));

    [HttpPost("payments/{id:guid}/approve")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public Task<IActionResult> Approve(Guid id, [FromServices] AppDbContext db) =>
        Run(async () => await _payroll.ApproveAsync(User.GetAcademyId(), id, await ActorAsync(db)));

    [HttpPost("payments/{id:guid}/reopen")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public Task<IActionResult> Reopen(Guid id) => Run(async () => await _payroll.ReopenAsync(User.GetAcademyId(), id));

    [HttpPost("payments/{id:guid}/pay")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public Task<IActionResult> Pay(Guid id, [FromBody] PayRequest body) =>
        Run(async () => await _payroll.PayAsync(User.GetAcademyId(), id, body.Method, body.PaidOn));

    [HttpDelete("payments/{id:guid}")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public Task<IActionResult> Delete(Guid id) =>
        Run(async () => { await _payroll.DeleteAsync(User.GetAcademyId(), id); return null; });

    // ── Adelantos ────────────────────────────────────────────
    [HttpGet("advances")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public Task<IActionResult> Advances([FromQuery] Guid? staffId, [FromQuery] bool pending = false) =>
        Run(async () => await _payroll.AdvancesAsync(User.GetAcademyId(), staffId, pending));

    [HttpPost("advances")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public Task<IActionResult> AddAdvance([FromBody] PayrollService.AdvanceInput input, [FromServices] AppDbContext db) =>
        Run(async () => await _payroll.AddAdvanceAsync(User.GetAcademyId(), input, await ActorAsync(db)));

    [HttpDelete("advances/{id:guid}")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public Task<IActionResult> DeleteAdvance(Guid id) =>
        Run(async () => { await _payroll.DeleteAdvanceAsync(User.GetAcademyId(), id); return null; });

    // ── Para el propio colaborador ───────────────────────────
    /// <summary>Horas y monto estimado del periodo en curso, pagos recibidos y adelantos pendientes.</summary>
    [HttpGet("me")]
    public Task<IActionResult> Mine() => Run(async () => await _payroll.MineAsync(User.GetAcademyId(), User.GetUserId()));
}
