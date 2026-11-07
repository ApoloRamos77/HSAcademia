using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HSAcademia.API.Authorization;
using HSAcademia.Application.DTOs.Finances;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

/// <summary>
/// Comprobantes con varios ítems: mensualidades de hermanos, productos de tienda y otros cobros
/// bajo un solo número de recibo. También reimpresión y anulación completa.
/// </summary>
[ApiController]
[Route("api/receipts")]
[Authorize(Roles = "AcademyAdmin,Staff")]
public class ReceiptsController : ControllerBase
{
    private readonly ReceiptService _receipts;

    public ReceiptsController(ReceiptService receipts) => _receipts = receipts;

    /// <summary>Apoderado, hermanos inscritos y todas sus deudas pendientes (para armar el cobro).</summary>
    [HttpGet("family/{studentId:guid}")]
    [RequirePermission(Permissions.ManageFinances, Permissions.ManageStore)]
    public Task<IActionResult> Family(Guid studentId) =>
        Run(academyId => _receipts.GetFamilyAccountAsync(academyId, studentId));

    /// <summary>Registra el cobro y emite un solo comprobante con todos los ítems.</summary>
    [HttpPost]
    [RequirePermission(Permissions.ManageFinances, Permissions.ManageStore)]
    public async Task<IActionResult> Checkout([FromBody] CheckoutDto dto)
    {
        var canDebts = await PermissionChecker.HasAsync(HttpContext, Permissions.ManageFinances);
        var canProducts = canDebts || await PermissionChecker.HasAsync(HttpContext, Permissions.ManageStore);
        return await Run(academyId => _receipts.CheckoutAsync(academyId, dto, canDebts, canProducts));
    }

    /// <summary>Calcula el comprobante con sus descuentos (pronto pago, promoción) sin registrar nada.</summary>
    [HttpPost("preview")]
    [RequirePermission(Permissions.ManageFinances, Permissions.ManageStore)]
    public async Task<IActionResult> Preview([FromBody] CheckoutDto dto)
    {
        var canDebts = await PermissionChecker.HasAsync(HttpContext, Permissions.ManageFinances);
        var canProducts = canDebts || await PermissionChecker.HasAsync(HttpContext, Permissions.ManageStore);
        return await Run(academyId => _receipts.PreviewAsync(academyId, dto, canDebts, canProducts));
    }

    [HttpGet]
    [RequirePermission(Permissions.ManageFinances, Permissions.ManageStore)]
    public Task<IActionResult> List([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? search, [FromQuery] Guid? studentId) =>
        Run(academyId => _receipts.ListAsync(academyId, from, to, search, studentId));

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.ManageFinances, Permissions.ManageStore)]
    public Task<IActionResult> Get(Guid id) => Run(academyId => _receipts.GetAsync(academyId, id));

    /// <summary>Busca por número (incluye recibos anteriores a esta versión, para reimprimirlos).</summary>
    [HttpGet("by-number/{number}")]
    [RequirePermission(Permissions.ManageFinances, Permissions.ManageStore)]
    public Task<IActionResult> ByNumber(string number) => Run(academyId => _receipts.GetByNumberAsync(academyId, number));

    /// <summary>Anula el comprobante completo: revierte los abonos, las ventas y el stock.</summary>
    [HttpPost("{id:guid}/void")]
    [RequirePermission(Permissions.ManageFinances)]
    public Task<IActionResult> Void(Guid id, [FromBody] VoidReceiptDto dto) =>
        Run(academyId => _receipts.VoidAsync(academyId, id, dto?.Reason ?? ""));

    private async Task<IActionResult> Run<T>(Func<Guid, Task<T>> action)
    {
        var academyId = User.GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try
        {
            return Ok(await action(academyId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex) when (ex is not Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
