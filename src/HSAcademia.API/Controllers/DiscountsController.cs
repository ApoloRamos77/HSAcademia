using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HSAcademia.API.Authorization;
using HSAcademia.Application.DTOs.Finances;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

/// <summary>Descuentos y promociones: hermanos, pronto pago y promociones con vigencia.</summary>
[ApiController]
[Route("api/discounts")]
[Authorize(Roles = "AcademyAdmin,Staff")]
public class DiscountsController : ControllerBase
{
    private readonly DiscountService _discounts;
    public DiscountsController(DiscountService discounts) => _discounts = discounts;

    /// <summary>Todas las reglas (configuración). ?active=true para solo las activas (cobro).</summary>
    [HttpGet]
    [RequirePermission(Permissions.CollectionsCollect, Permissions.StoreSell, Permissions.FinancesConfig)]
    public Task<IActionResult> List([FromQuery] bool active = false) => Run(a => _discounts.ListAsync(a, active));

    [HttpPost]
    [RequirePermission(Permissions.FinancesConfig, AcademyWide = true)]
    public Task<IActionResult> Create([FromBody] SaveDiscountRuleDto dto) => Run(a => _discounts.SaveAsync(a, null, dto));

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.FinancesConfig, AcademyWide = true)]
    public Task<IActionResult> Update(Guid id, [FromBody] SaveDiscountRuleDto dto) => Run(a => _discounts.SaveAsync(a, id, dto));

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.FinancesConfig, AcademyWide = true)]
    public Task<IActionResult> Delete(Guid id) => Run(async a => { await _discounts.DeleteAsync(a, id); return new { ok = true }; });

    private async Task<IActionResult> Run<T>(Func<Guid, Task<T>> action)
    {
        var academyId = User.GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try { return Ok(await action(academyId)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (Exception ex) when (ex is not Microsoft.EntityFrameworkCore.DbUpdateException) { return BadRequest(new { message = ex.Message }); }
    }
}
