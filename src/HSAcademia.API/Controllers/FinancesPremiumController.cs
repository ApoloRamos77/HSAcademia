using System;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.FinancesPremium;
using HSAcademia.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HSAcademia.API.Authorization;

namespace HSAcademia.API.Controllers;

[ApiController]
[Route("api/finances-premium")]
[Authorize(Roles = "AcademyAdmin,Staff")]
public class FinancesPremiumController : ControllerBase
{
    private readonly IFinancesPremiumService _service;

    public FinancesPremiumController(IFinancesPremiumService service)
    {
        _service = service;
    }

    private Guid GetAcademyId()
    {
        var idStr = User.FindFirst("academyId")?.Value ?? User.FindFirst("AcademyId")?.Value;
        return string.IsNullOrEmpty(idStr) ? Guid.Empty : Guid.Parse(idStr);
    }

    private Guid GetUserId()
    {
        var idStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return string.IsNullOrEmpty(idStr) ? Guid.Empty : Guid.Parse(idStr);
    }

    // ── Expenses ──────────────────────────────────────────────────────────────

    [HttpGet("expenses")]
    [RequirePermission(Permissions.FinancesExpenses, AcademyWide = true)]
    public async Task<IActionResult> GetExpenses([FromQuery] int month, [FromQuery] int year)
    {
        if (month == 0) month = DateTime.UtcNow.Month;
        if (year == 0) year = DateTime.UtcNow.Year;
        var result = await _service.GetExpensesAsync(GetAcademyId(), month, year);
        return Ok(result);
    }

    [HttpPost("expenses")]
    [RequirePermission(Permissions.FinancesExpenses, AcademyWide = true)]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseDto dto)
    {
        var result = await _service.CreateExpenseAsync(GetAcademyId(), GetUserId(), dto);
        return Ok(result);
    }

    [HttpPut("expenses/{id}")]
    [RequirePermission(Permissions.FinancesExpenses, AcademyWide = true)]
    public async Task<IActionResult> UpdateExpense(Guid id, [FromBody] UpdateExpenseDto dto)
    {
        var result = await _service.UpdateExpenseAsync(GetAcademyId(), id, dto);
        return Ok(result);
    }

    [HttpDelete("expenses/{id}")]
    [RequirePermission(Permissions.FinancesExpenses, AcademyWide = true)]
    public async Task<IActionResult> DeleteExpense(Guid id)
    {
        await _service.DeleteExpenseAsync(id, GetAcademyId());
        return Ok(new { message = "Gasto eliminado correctamente" });
    }

    // ── Petty Cash ────────────────────────────────────────────────────────────

    [HttpGet("petty-cash")]
    [RequirePermission(Permissions.FinancesExpenses, AcademyWide = true)]
    public async Task<IActionResult> GetPettyCash([FromQuery] int month, [FromQuery] int year)
    {
        if (month == 0) month = DateTime.UtcNow.Month;
        if (year == 0) year = DateTime.UtcNow.Year;
        var result = await _service.GetPettyCashAsync(GetAcademyId(), month, year);
        return Ok(result);
    }

    [HttpPost("petty-cash")]
    [RequirePermission(Permissions.FinancesExpenses, AcademyWide = true)]
    public async Task<IActionResult> CreatePettyCash([FromBody] CreatePettyCashDto dto)
    {
        try
        {
            var result = await _service.CreatePettyCashAsync(GetAcademyId(), dto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("petty-cash/transaction")]
    [RequirePermission(Permissions.FinancesExpenses, AcademyWide = true)]
    public async Task<IActionResult> AddTransaction([FromBody] AddPettyCashTransactionDto dto)
    {
        var result = await _service.AddTransactionAsync(GetAcademyId(), GetUserId(), dto);
        return Ok(result);
    }

    // ── Staff Payments ────────────────────────────────────────────────────────

    [HttpGet("staff-payments")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public async Task<IActionResult> GetStaffPayments([FromQuery] int month, [FromQuery] int year)
    {
        if (month == 0) month = DateTime.UtcNow.Month;
        if (year == 0) year = DateTime.UtcNow.Year;
        var result = await _service.GetStaffPaymentsAsync(GetAcademyId(), month, year);
        return Ok(result);
    }

    [HttpGet("my-payments")]
    public async Task<IActionResult> GetMyPayments([FromQuery] int month, [FromQuery] int year)
    {
        if (month == 0) month = DateTime.UtcNow.Month;
        if (year == 0) year = DateTime.UtcNow.Year;
        var userId = GetUserId();
        var result = await _service.GetMyStaffPaymentsAsync(GetAcademyId(), userId, month, year);
        return Ok(result);
    }

    [HttpGet("staff-payments/calculate")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public async Task<IActionResult> CalculateStaffPayment([FromQuery] Guid staffId, [FromQuery] int month, [FromQuery] int year)
    {
        if (month == 0) month = DateTime.UtcNow.Month;
        if (year == 0) year = DateTime.UtcNow.Year;
        try
        {
            var result = await _service.CalculateStaffPaymentAsync(GetAcademyId(), staffId, month, year);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("staff-payments")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public async Task<IActionResult> CreateStaffPayment([FromBody] CreateStaffPaymentDto dto)
    {
        var result = await _service.CreateStaffPaymentAsync(GetAcademyId(), dto);
        return Ok(result);
    }

    [HttpPatch("staff-payments/{id}/mark-paid")]
    [RequirePermission(Permissions.FinancesStaffPayments, AcademyWide = true)]
    public async Task<IActionResult> MarkPaid(Guid id)
    {
        // Errores de negocio (ya pagado, periodo cerrado, no encontrado) los traduce el manejador global.
        var result = await _service.MarkStaffPaymentPaidAsync(id, GetAcademyId());
        return Ok(result);
    }

    // ── Dashboard Summary ─────────────────────────────────────────────────────

    [HttpGet("summary")]
    [RequirePermission(Permissions.FinancesSummary, AcademyWide = true)]
    public async Task<IActionResult> GetSummary([FromQuery] int month, [FromQuery] int year)
    {
        if (month == 0) month = DateTime.UtcNow.Month;
        if (year == 0) year = DateTime.UtcNow.Year;
        var result = await _service.GetFinanceSummaryAsync(GetAcademyId(), month, year);
        return Ok(result);
    }

    [HttpGet("trends")]
    [RequirePermission(Permissions.FinancesSummary, AcademyWide = true)]
    public async Task<IActionResult> GetTrends([FromQuery] int months = 6)
    {
        var result = await _service.GetTrendDataAsync(GetAcademyId(), months);
        return Ok(result);
    }

    // ── Financial Goals ───────────────────────────────────────────────────────

    [HttpGet("goals")]
    [RequirePermission(Permissions.FinancesSummary, AcademyWide = true)]
    public async Task<IActionResult> GetGoal([FromQuery] int month, [FromQuery] int year)
    {
        if (month == 0) month = DateTime.UtcNow.Month;
        if (year == 0) year = DateTime.UtcNow.Year;
        var result = await _service.GetGoalAsync(GetAcademyId(), month, year);
        // Retornamos 200 con o sin data, el frontend decide si mostrar "Crear Meta" si es null
        return Ok(result);
    }

    [HttpPost("goals")]
    [RequirePermission(Permissions.FinancesSummary, AcademyWide = true)]
    public async Task<IActionResult> UpsertGoal([FromBody] CreateFinancialGoalDto dto)
    {
        var result = await _service.UpsertGoalAsync(GetAcademyId(), dto);
        return Ok(result);
    }

    // ── Monthly Closings ──────────────────────────────────────────────────────

    [HttpGet("closings")]
    [RequirePermission(Permissions.FinancesSummary, Permissions.FinancesClose, Permissions.CollectionsView, Permissions.CollectionsCollect, Permissions.StoreSell)]
    public async Task<IActionResult> GetMonthlyClosing([FromQuery] int month, [FromQuery] int year)
    {
        if (month == 0) month = DateTime.UtcNow.Month;
        if (year == 0) year = DateTime.UtcNow.Year;
        var result = await _service.GetMonthlyClosingAsync(GetAcademyId(), month, year);
        return Ok(result);
    }

    [HttpPost("monthly-closings/{year}/{month}/close")]
    [HttpPost("closings")] // ruta que usa el frontend (antes no existía y el cierre fallaba)
    [RequirePermission(Permissions.FinancesClose, AcademyWide = true)]
    public async Task<IActionResult> CloseMonth([FromBody] CloseMonthDto dto, int? year = null, int? month = null)
    {
        if (year.HasValue) dto.Year = year.Value;
        if (month.HasValue) dto.Month = month.Value;
        var result = await _service.CloseMonthAsync(GetAcademyId(), GetUserId(), dto);
        return Ok(result);
    }

    // ── Financial Period Closings (Block modifications) ──────────────────────

    [HttpGet("periods/{year}/{month}")]
    [RequirePermission(Permissions.FinancesSummary, Permissions.FinancesClose, AcademyWide = true)]
    public async Task<IActionResult> GetPeriodStatus(int year, int month)
    {
        var result = await _service.GetPeriodStatusAsync(GetAcademyId(), month, year);
        return Ok(result);
    }

    [HttpPost("periods/{year}/{month}/toggle")]
    [RequirePermission(Permissions.FinancesClose, AcademyWide = true)]
    public async Task<IActionResult> TogglePeriodClose(int year, int month)
    {
        var result = await _service.TogglePeriodCloseAsync(GetAcademyId(), month, year, GetUserId());
        return Ok(result);
    }
}
