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
}
