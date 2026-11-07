using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Finances;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HSAcademia.API.Authorization;

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
    [RequirePermission(Permissions.ManageFinances)]
    public async Task<IActionResult> GetConfig()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _financesService.GetConfigAsync(academyId));
    }

    [HttpPut("config")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.ManageFinances)]
    public async Task<IActionResult> UpdateConfig([FromBody] UpdateFinancialConfigDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _financesService.UpdateConfigAsync(academyId, dto));
    }

    // ── Generate Debts ──────────────────────────────────────
    [HttpPost("generate-debts")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.ManageFinances)]
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
    [RequirePermission(Permissions.ManageFinances)]
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
    [RequirePermission(Permissions.ManageFinances, Permissions.ManageStore)] // Tienda: recibo combinado
    public async Task<IActionResult> GetPendingDebts([FromQuery] int? year, [FromQuery] int? month, [FromQuery] Guid? studentId)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _financesService.GetPendingDebtsAsync(academyId, year, month, studentId));
    }

    [HttpGet("debts/all")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.ManageFinances)]
    public async Task<IActionResult> GetAllDebts([FromQuery] int? year, [FromQuery] int? month)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _financesService.GetAllDebtsAsync(academyId, year, month));
    }

    // ── Register Payment (partial/full + method + voucher) ────────
    [HttpPost("debts/pay")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.ManageFinances)]
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

    /// <summary>Regulariza una mensualidad vencida: con o sin mora, con observación obligatoria.</summary>
    [HttpPost("debts/{id:guid}/regularize")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.ManageFinances)]
    public async Task<IActionResult> Regularize(Guid id, [FromBody] RegularizeDebtDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try
        {
            var userId = User.GetUserId();
            return Ok(await _financesService.RegularizeAsync(academyId, id, userId == Guid.Empty ? null : userId, dto));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── Upload Voucher ───────────────────────────────────────────────
    [HttpPost("upload-voucher")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.ManageFinances, Permissions.ManageStore)]
    [RequestSizeLimit(UploadStorage.MaxVoucherBytes + 64 * 1024)]
    public async Task<IActionResult> UploadVoucher(Microsoft.AspNetCore.Http.IFormFile file, [FromServices] UploadStorage storage)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();

        var (url, error) = await storage.SaveVoucherAsync(file);
        return error != null ? BadRequest(new { message = error }) : Ok(new { url });
    }

    // ── Quick-pay (legacy full cash) ──────────────────────────────
    [HttpPost("debts/{id}/pay")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.ManageFinances)]
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
    [RequirePermission(Permissions.ManageFinances)]
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
    [RequirePermission(Permissions.ManageFinances)]
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

    /// <summary>
    /// DESHABILITADO por seguridad: marcaba deudas como pagadas sin ningún cobro real.
    /// Se mantiene la ruta para que la app móvil instalada muestre un mensaje claro en vez de fallar.
    /// Reemplazo previsto: el apoderado sube su voucher y la academia lo valida.
    /// </summary>
    [HttpPost("mobile/pay")]
    [Authorize(Roles = "Guardian,Student")]
    public IActionResult MobilePayDebts([FromBody] List<Guid>? debtIds)
    {
        return BadRequest(new
        {
            message = "El pago directo desde la app ya no está disponible. " +
                      "Paga por Yape, Plin o transferencia y envía tu comprobante desde el portal web " +
                      "(Estado de cuenta → Enviar comprobante); la academia lo validará y registrará tu pago."
        });
    }
}
