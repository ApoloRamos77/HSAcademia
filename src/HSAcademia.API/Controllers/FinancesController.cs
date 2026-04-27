using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Finances;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

[ApiController]
[Route("api/finances")]
[Authorize]
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

    // ── Config ────────────────────────────────────────────────────
    [HttpGet("config")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    public async Task<IActionResult> GetConfig()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _financesService.GetConfigAsync(academyId));
    }

    [HttpPut("config")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    public async Task<IActionResult> UpdateConfig([FromBody] UpdateFinancialConfigDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _financesService.UpdateConfigAsync(academyId, dto));
    }

    // ── Generate Debts ────────────────────────────────────────────
    [HttpPost("generate-debts")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    public async Task<IActionResult> GenerateDebts()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        int count = await _financesService.GenerateMonthlyDebtsAsync(academyId);
        return Ok(new { message = $"Se generaron {count} nuevas deudas.", generatedCount = count });
    }

    // ── Query Debts ───────────────────────────────────────────────
    [HttpGet("debts/pending")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    public async Task<IActionResult> GetPendingDebts()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _financesService.GetPendingDebtsAsync(academyId));
    }

    [HttpGet("debts/all")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    public async Task<IActionResult> GetAllDebts()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _financesService.GetAllDebtsAsync(academyId));
    }

    // ── Register Payment (partial/full + method + voucher) ────────
    [HttpPost("debts/pay")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    public async Task<IActionResult> RegisterPayment([FromBody] RegisterPaymentDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try
        {
            return Ok(await _financesService.RegisterPaymentAsync(academyId, dto));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Quick-pay (legacy full cash) ──────────────────────────────
    [HttpPost("debts/{id}/pay")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    public async Task<IActionResult> MarkAsPaid(Guid id)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try
        {
            return Ok(await _financesService.MarkAsPaidAsync(academyId, id));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Recalculate / Proration ───────────────────────────────────
    [HttpPost("debts/recalculate")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    public async Task<IActionResult> RecalculatePayment([FromBody] RecalculatePaymentDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try
        {
            return Ok(await _financesService.RecalculatePaymentAsync(academyId, dto));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Exclude / Exonerate ───────────────────────────────────────
    [HttpPost("debts/exclude")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    public async Task<IActionResult> ExcludePayment([FromBody] ExcludePaymentDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try
        {
            return Ok(await _financesService.ExcludePaymentAsync(academyId, dto));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Mobile / Guardian ─────────────────────────────────────────
    [HttpGet("my-debts")]
    [Authorize(Roles = "Guardian,Student")]
    public async Task<IActionResult> GetMyDebts()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();
        return Ok(await _financesService.GetMyDebtsAsync(academyId, userId));
    }

    [HttpPost("mobile/pay")]
    [Authorize(Roles = "Guardian,Student")]
    public async Task<IActionResult> MobilePayDebts([FromBody] List<Guid> debtIds)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();
        try
        {
            await _financesService.ProcessMobilePaymentAsync(academyId, userId, debtIds);
            return Ok(new { message = "Pago procesado exitosamente." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
