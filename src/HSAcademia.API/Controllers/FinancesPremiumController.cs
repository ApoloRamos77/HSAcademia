using System;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.FinancesPremium;
using HSAcademia.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

[ApiController]
[Route("api/finances-premium")]
[Authorize]
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
    public async Task<IActionResult> GetExpenses([FromQuery] int month, [FromQuery] int year)
    {
        if (month == 0) month = DateTime.UtcNow.Month;
        if (year == 0) year = DateTime.UtcNow.Year;
        var result = await _service.GetExpensesAsync(GetAcademyId(), month, year);
        return Ok(result);
    }

    [HttpPost("expenses")]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseDto dto)
    {
        var result = await _service.CreateExpenseAsync(GetAcademyId(), GetUserId(), dto);
        return Ok(result);
    }

    [HttpDelete("expenses/{id}")]
    public async Task<IActionResult> DeleteExpense(Guid id)
    {
        await _service.DeleteExpenseAsync(id, GetAcademyId());
        return Ok(new { message = "Gasto eliminado correctamente" });
    }

    // ── Petty Cash ────────────────────────────────────────────────────────────

    [HttpGet("petty-cash")]
    public async Task<IActionResult> GetPettyCash([FromQuery] int month, [FromQuery] int year)
    {
        if (month == 0) month = DateTime.UtcNow.Month;
        if (year == 0) year = DateTime.UtcNow.Year;
        var result = await _service.GetPettyCashAsync(GetAcademyId(), month, year);
        return Ok(result);
    }

    [HttpPost("petty-cash")]
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
    public async Task<IActionResult> AddTransaction([FromBody] AddPettyCashTransactionDto dto)
    {
        var result = await _service.AddTransactionAsync(GetAcademyId(), GetUserId(), dto);
        return Ok(result);
    }

    // ── Staff Payments ────────────────────────────────────────────────────────

    [HttpGet("staff-payments")]
    public async Task<IActionResult> GetStaffPayments([FromQuery] int month, [FromQuery] int year)
    {
        if (month == 0) month = DateTime.UtcNow.Month;
        if (year == 0) year = DateTime.UtcNow.Year;
        var result = await _service.GetStaffPaymentsAsync(GetAcademyId(), month, year);
        return Ok(result);
    }

    [HttpPost("staff-payments")]
    public async Task<IActionResult> CreateStaffPayment([FromBody] CreateStaffPaymentDto dto)
    {
        var result = await _service.CreateStaffPaymentAsync(GetAcademyId(), dto);
        return Ok(result);
    }

    [HttpPatch("staff-payments/{id}/mark-paid")]
    public async Task<IActionResult> MarkPaid(Guid id)
    {
        try
        {
            var result = await _service.MarkStaffPaymentPaidAsync(id, GetAcademyId());
            return Ok(result);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // ── Dashboard Summary ─────────────────────────────────────────────────────

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] int month, [FromQuery] int year)
    {
        if (month == 0) month = DateTime.UtcNow.Month;
        if (year == 0) year = DateTime.UtcNow.Year;
        var result = await _service.GetFinanceSummaryAsync(GetAcademyId(), month, year);
        return Ok(result);
    }
}
