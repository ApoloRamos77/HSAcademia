using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HSAcademia.API.Authorization;
using HSAcademia.Application.DTOs.Finances;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

/// <summary>Cuadre de caja: lo cobrado por cada cobrador, rendiciones y recepción del efectivo.</summary>
[ApiController]
[Route("api/cash")]
[Authorize(Roles = "AcademyAdmin,Staff")]
public class CashController : ControllerBase
{
    private readonly CashService _cash;
    public CashController(CashService cash) => _cash = cash;

    /// <summary>Mi caja: recibos que cobré y aún no rindo.</summary>
    [HttpGet("mine")]
    [RequirePermission(Permissions.CollectionsCollect, Permissions.StoreSell)]
    public Task<IActionResult> Mine() => Run(a => _cash.MyCashAsync(a, User.GetUserId()));

    /// <summary>Rendir caja: declaro el efectivo que entrego.</summary>
    [HttpPost("handovers")]
    [RequirePermission(Permissions.CollectionsCollect, Permissions.StoreSell)]
    public Task<IActionResult> Declare([FromBody] DeclareHandoverDto dto) => Run(a => _cash.DeclareAsync(a, User.GetUserId(), dto));

    /// <summary>Cuadre del día (fecha local; por defecto hoy).</summary>
    [HttpGet("summary")]
    [RequirePermission(Permissions.CashReceive, AcademyWide = true)]
    public Task<IActionResult> Summary([FromQuery] DateTime? date) => Run(a => _cash.DaySummaryAsync(a, date ?? AcademyClock.Today));

    [HttpGet("handovers")]
    [RequirePermission(Permissions.CashReceive, AcademyWide = true)]
    public Task<IActionResult> List([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? status) =>
        Run(a => _cash.ListAsync(a, from, to, status));

    [HttpGet("handovers/{id:guid}/receipts")]
    [RequirePermission(Permissions.CashReceive, AcademyWide = true)]
    public Task<IActionResult> Receipts(Guid id) => Run(a => _cash.HandoverReceiptsAsync(a, id));

    /// <summary>Finanzas recibe y cuenta el efectivo de una rendición.</summary>
    [HttpPost("handovers/{id:guid}/receive")]
    [RequirePermission(Permissions.CashReceive, AcademyWide = true)]
    public Task<IActionResult> Receive(Guid id, [FromBody] ReceiveHandoverDto dto) =>
        Run(a => _cash.ReceiveAsync(a, User.GetUserId(), User.IsInRole("AcademyAdmin"), id, dto));

    private async Task<IActionResult> Run<T>(Func<Guid, Task<T>> action)
    {
        var academyId = User.GetAcademyId();
        if (academyId == Guid.Empty || User.GetUserId() == Guid.Empty) return Unauthorized();
        try { return Ok(await action(academyId)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex) when (ex is not Microsoft.EntityFrameworkCore.DbUpdateException) { return BadRequest(new { message = ex.Message }); }
    }
}
