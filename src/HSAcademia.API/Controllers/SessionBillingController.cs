using HSAcademia.API.Authorization;
using HSAcademia.Application.DTOs.Finances;
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
    private readonly ReceiptService _receipts;
    private readonly AppDbContext _db;

    /// <summary>Plazo para enviar un cobro hecho sin señal (como la asistencia).</summary>
    private const int OfflineDays = 7;

    public SessionBillingController(SessionBillingService sessions, ReceiptService receipts, AppDbContext db)
    {
        _sessions = sessions;
        _receipts = receipts;
        _db = db;
    }

    public sealed class ChargeAndPayRequest
    {
        public Guid StudentId { get; set; }
        public Guid? EventId { get; set; }
        public DateTime? Date { get; set; }
        public string Method { get; set; } = "Cash";
        public string? OperationNumber { get; set; }
        /// <summary>Clave única del cobro (la genera el celular): un reintento nunca cobra dos veces.</summary>
        public string ClientRequestId { get; set; } = "";
        /// <summary>Hora real del cobro si se hizo sin señal (se envía después).</summary>
        public DateTime? ChargedAt { get; set; }
        public string? Channel { get; set; }
    }

    private async Task<(Guid Id, string Name)> ActorAsync()
    {
        var id = User.GetUserId();
        var name = await _db.Users.Where(u => u.Id == id).Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync();
        return (id, name ?? "");
    }

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
        var (actorId, actorName) = await ActorAsync();
        try { return Ok(await _sessions.ChargeSessionAsync(User.GetAcademyId(), dto.StudentId, dto.EventId, dto.Date, actorId, actorName)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>Lo que se cobraría por esta sesión (estimado, sin registrar nada).</summary>
    [HttpGet("quote")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.CollectionsCollect)]
    public async Task<IActionResult> Quote([FromQuery] Guid studentId, [FromQuery] Guid? eventId, [FromQuery] DateTime? date)
    {
        if (!await ScopeChecker.IsAllowedAsync(HttpContext, ScopeTarget.Student, studentId, Permissions.CollectionsCollect))
            return ScopeChecker.Denied();
        try { return Ok(await _sessions.QuoteAsync(User.GetAcademyId(), studentId, eventId, date)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Cobro en un paso: registra la sesión (marca Presente), y la cobra con recibo.
    /// Idempotente por ClientRequestId: la app lo reintenta tal cual si se hizo sin señal.
    /// </summary>
    [HttpPost("charge-and-pay")]
    [Authorize(Roles = "AcademyAdmin,Staff")]
    [RequirePermission(Permissions.CollectionsCollect)]
    public async Task<IActionResult> ChargeAndPay([FromBody] ChargeAndPayRequest dto)
    {
        var academyId = User.GetAcademyId();
        var requestId = dto.ClientRequestId?.Trim() ?? "";
        if (requestId.Length is < 8 or > 64) return BadRequest(new { message = "Falta la clave del cobro. Actualiza la app." });
        // El medio de pago se valida antes de registrar la sesión: un cobro rechazado no deja nada a medias.
        if (!Enum.TryParse<HSAcademia.Domain.Entities.PaymentMethod>(dto.Method, true, out var method) || method == HSAcademia.Domain.Entities.PaymentMethod.Mixed)
            return BadRequest(new { message = "Medio de pago no válido." });
        if (method is HSAcademia.Domain.Entities.PaymentMethod.Yape or HSAcademia.Domain.Entities.PaymentMethod.Plin or HSAcademia.Domain.Entities.PaymentMethod.BankTransfer
            && string.IsNullOrWhiteSpace(dto.OperationNumber))
            return BadRequest(new { message = "Para Yape, Plin o transferencia indica el número de operación." });
        if (!await ScopeChecker.IsAllowedAsync(HttpContext, ScopeTarget.Student, dto.StudentId, Permissions.CollectionsCollect))
            return ScopeChecker.Denied();
        try
        {
            var previous = await _receipts.FindByClientRequestIdAsync(academyId, requestId);
            if (previous != null) return Ok(new { paid = true, alreadyRegistered = true, receipt = previous });

            if (dto.ChargedAt.HasValue)
            {
                var chargedDay = AcademyClock.ToLocalDate(dto.ChargedAt.Value.ToUniversalTime());
                if (chargedDay < AcademyClock.Today.AddDays(-OfflineDays))
                    return BadRequest(new { message = $"Pasaron más de {OfflineDays} días desde este cobro sin señal: regístralo en oficina." });
            }

            var (actorId, actorName) = await ActorAsync();
            var charge = await _sessions.ChargeSessionAsync(academyId, dto.StudentId, dto.EventId, dto.Date, actorId, actorName);
            var record = await _db.PaymentRecords.AsNoTracking().FirstAsync(p => p.Id == charge.DebtId);
            if (record.IsPaid)
                return Ok(new { paid = true, alreadyPaid = charge.AlreadyCharged, coveredByCredit = record.SessionCreditApplied > 0, charge, receipt = (ReceiptDto?)null });

            var offline = dto.ChargedAt.HasValue && (DateTime.UtcNow - dto.ChargedAt.Value.ToUniversalTime()).TotalMinutes > 10;
            var receipt = await _receipts.CheckoutAsync(academyId, new CheckoutDto
            {
                Method = dto.Method,
                OperationNumber = dto.OperationNumber,
                Channel = string.IsNullOrWhiteSpace(dto.Channel) ? "app" : dto.Channel,
                ClientRequestId = requestId,
                Notes = offline ? $"Cobrado sin señal el {TimeZoneInfo.ConvertTimeFromUtc(dto.ChargedAt!.Value.ToUniversalTime(), AcademyClock.Zone):dd/MM/yyyy HH:mm}" : null,
                Debts = new List<CheckoutDebtItemDto> { new() { PaymentRecordId = record.Id } }
            }, canChargeDebts: true, canSellProducts: false);
            return Ok(new { paid = true, alreadyPaid = false, coveredByCredit = record.SessionCreditApplied > 0, charge, receipt });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
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
