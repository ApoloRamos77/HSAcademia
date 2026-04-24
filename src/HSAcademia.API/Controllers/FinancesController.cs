using System;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Finances;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

[ApiController]
[Route("api/finances")]
[Authorize(Roles = "AcademyAdmin,Staff")]
public class FinancesController : ControllerBase
{
    private readonly FinancesService _financesService;

    public FinancesController(FinancesService financesService)
    {
        _financesService = financesService;
    }

    private Guid GetAcademyId()
    {
        var idStr = User.FindFirst("academyId")?.Value;
        return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
    }

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _financesService.GetConfigAsync(academyId));
    }

    [HttpPut("config")]
    public async Task<IActionResult> UpdateConfig([FromBody] UpdateFinancialConfigDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _financesService.UpdateConfigAsync(academyId, dto));
    }

    [HttpPost("generate-debts")]
    public async Task<IActionResult> GenerateDebts()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        int count = await _financesService.GenerateMonthlyDebtsAsync(academyId);
        return Ok(new { message = $"Se generaron {count} nuevas deudas.", generatedCount = count });
    }

    [HttpGet("debts/pending")]
    public async Task<IActionResult> GetPendingDebts()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _financesService.GetPendingDebtsAsync(academyId));
    }

    [HttpPost("debts/{id}/pay")]
    public async Task<IActionResult> MarkAsPaid(Guid id)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try 
        {
            return Ok(await _financesService.MarkAsPaidAsync(academyId, id));
        }
        catch(Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
