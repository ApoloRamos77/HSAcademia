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

    [HttpPost("next-receipt")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    public async Task<IActionResult> GenerateNextReceipt()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        var nextNumber = await _financesService.GenerateNextReceiptNumberAsync(academyId);
        return Ok(new { receiptNumber = nextNumber });
    }

    // ── Generate Debts ──────────────────────────────────────
    [HttpPost("generate-debts")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    public async Task<IActionResult> GenerateDebts([FromBody] GenerateDebtsRequestDto? dto = null)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        var (generated, replaced, cleaned) = await _financesService.GenerateMonthlyDebtsAsync(academyId, dto?.Year, dto?.Month);
        var period = dto?.Year != null && dto?.Month != null
            ? $"{new System.Globalization.CultureInfo("es-PE").DateTimeFormat.GetMonthName(dto.Month.Value)} {dto.Year}"
            : "mes actual";
        var parts = new List<string>();
        if (generated > 0) parts.Add($"{generated} nuevos");
        if (replaced  > 0) parts.Add($"{replaced} actualizados");
        if (cleaned   > 0) parts.Add($"{cleaned} eliminados (antes de fecha inicio)");
        var detail = parts.Any() ? string.Join(", ", parts) : "sin cambios";
        return Ok(new { message = $"Motor ejecutado para {period}: {detail}.", generatedCount = generated, replacedCount = replaced, cleanedCount = cleaned });
    }

    // ── Generate next-month debt for a specific student ───────────
    [HttpPost("debts/{studentId}/generate-next-month")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    public async Task<IActionResult> GenerateNextMonthDebt(Guid studentId)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try
        {
            var result = await _financesService.GenerateNextMonthDebtAsync(academyId, studentId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Query Debts ──────────────────────────────────────
    [HttpGet("debts/pending")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    public async Task<IActionResult> GetPendingDebts([FromQuery] int? year, [FromQuery] int? month)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _financesService.GetPendingDebtsAsync(academyId, year, month));
    }

    [HttpGet("debts/all")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    public async Task<IActionResult> GetAllDebts([FromQuery] int? year, [FromQuery] int? month)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _financesService.GetAllDebtsAsync(academyId, year, month));
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

    // ── Upload Voucher ───────────────────────────────────────────────
    [HttpPost("upload-voucher")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    public async Task<IActionResult> UploadVoucher(Microsoft.AspNetCore.Http.IFormFile file)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No se proporcionó ningún archivo." });

        var uploadsFolder = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "uploads", "vouchers");
        if (!System.IO.Directory.Exists(uploadsFolder))
            System.IO.Directory.CreateDirectory(uploadsFolder);

        var ext = System.IO.Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = System.IO.Path.Combine(uploadsFolder, fileName);

        using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var url = $"/uploads/vouchers/{fileName}";
        return Ok(new { url });
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
