using HSAcademia.API.Authorization;
using HSAcademia.Application.DTOs.Finances;
using HSAcademia.Domain.Entities;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

/// <summary>
/// Formulario multipart del comprobante. Agrupar los campos en una clase es necesario para que
/// Swagger (Swashbuckle) pueda documentar endpoints con IFormFile.
/// </summary>
public class SubmitPaymentProofForm
{
    /// <summary>Foto (JPG, PNG, WEBP) o PDF del comprobante, máximo 5 MB.</summary>
    public IFormFile? File { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Yape | Plin | BankTransfer | Card | Other</summary>
    public string? Method { get; set; }
    public string? OperationNumber { get; set; }
    /// <summary>Fecha de pago (yyyy-MM-dd).</summary>
    public DateTime? PaidOn { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Comprobantes de pago: la familia los envía y la academia los valida.</summary>
[ApiController]
[Route("api/finances")]
[Authorize]
public class PaymentProofsController : ControllerBase
{
    private readonly PaymentProofService _proofs;

    public PaymentProofsController(PaymentProofService proofs) => _proofs = proofs;

    // ── Familia (app móvil / portal web) ──────────────────────────

    /// <summary>
    /// POST /api/finances/my-debts/{debtId}/proof  (multipart/form-data)
    /// Campos: file, amount, method (Yape|Plin|BankTransfer|Card|Other), operationNumber, paidOn (yyyy-MM-dd), notes.
    /// </summary>
    [HttpPost("my-debts/{debtId:guid}/proof")]
    [Authorize(Roles = "Guardian,Student")]
    [RequestSizeLimit(UploadStorage.MaxVoucherBytes + 64 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Submit(
        Guid debtId,
        [FromForm] SubmitPaymentProofForm form,
        [FromServices] UploadStorage storage)
    {
        var academyId = User.GetAcademyId();
        var userId = User.GetUserId();
        if (academyId == Guid.Empty || userId == Guid.Empty) return Unauthorized();

        var (url, error) = await storage.SaveVoucherAsync(form.File);
        if (error != null) return BadRequest(new { message = error });

        var proof = await _proofs.SubmitAsync(academyId, userId, debtId, form.Amount, form.Method,
            form.OperationNumber, form.PaidOn, form.Notes, url!);
        return Ok(proof);
    }

    /// <summary>GET /api/finances/my-proofs — comprobantes enviados por la familia y su estado.</summary>
    [HttpGet("my-proofs")]
    [Authorize(Roles = "Guardian,Student")]
    public async Task<IActionResult> Mine()
    {
        var academyId = User.GetAcademyId();
        var userId = User.GetUserId();
        if (academyId == Guid.Empty || userId == Guid.Empty) return Unauthorized();
        return Ok(await _proofs.GetMineAsync(academyId, userId));
    }

    // ── Academia ─────────────────────────────────────────────────

    /// <summary>GET /api/finances/proofs?status=Pending|Approved|Rejected</summary>
    [HttpGet("proofs")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.CollectionsApproveProofs)]
    public async Task<IActionResult> List([FromQuery] string? status = null)
    {
        PaymentProofStatus? filter = Enum.TryParse<PaymentProofStatus>(status, true, out var s) ? s : null;
        return Ok(await _proofs.GetForReviewAsync(User.GetAcademyId(), filter));
    }

    [HttpGet("proofs/pending-count")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.CollectionsApproveProofs)]
    public async Task<IActionResult> PendingCount() =>
        Ok(new { count = await _proofs.CountPendingAsync(User.GetAcademyId()) });

    [HttpPost("proofs/{id:guid}/approve")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.CollectionsApproveProofs)]
    [RequireScope(ScopeTarget.Proof, "id")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApprovePaymentProofDto? dto) =>
        Ok(await _proofs.ApproveAsync(User.GetAcademyId(), User.GetUserId(), id, dto ?? new ApprovePaymentProofDto()));

    [HttpPost("proofs/{id:guid}/reject")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.CollectionsApproveProofs)]
    [RequireScope(ScopeTarget.Proof, "id")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectPaymentProofDto dto) =>
        Ok(await _proofs.RejectAsync(User.GetAcademyId(), User.GetUserId(), id, dto.Reason));
}
