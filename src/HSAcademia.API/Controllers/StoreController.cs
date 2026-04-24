using System;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Store;
using HSAcademia.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSAcademia.API.Controllers;

[ApiController]
[Route("api/store")]
[Authorize(Roles = "AcademyAdmin,Staff")]
public class StoreController : ControllerBase
{
    private readonly StoreService _storeService;

    public StoreController(StoreService storeService)
    {
        _storeService = storeService;
    }

    private Guid GetAcademyId()
    {
        var idStr = User.FindFirst("academyId")?.Value;
        return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _storeService.GetProductsAsync(academyId));
    }

    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try 
        { 
            return Ok(await _storeService.CreateProductAsync(academyId, dto)); 
        }
        catch (Exception ex) 
        { 
            return BadRequest(new { message = ex.Message }); 
        }
    }

    [HttpPut("products/{id}")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] CreateProductDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try 
        { 
            return Ok(await _storeService.UpdateProductAsync(academyId, id, dto)); 
        }
        catch (Exception ex) 
        { 
            return BadRequest(new { message = ex.Message }); 
        }
    }

    [HttpGet("sales")]
    public async Task<IActionResult> GetSales()
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        return Ok(await _storeService.GetSalesAsync(academyId));
    }

    [HttpPost("sales")]
    public async Task<IActionResult> CreateSale([FromBody] CreateProductSaleDto dto)
    {
        var academyId = GetAcademyId();
        if (academyId == Guid.Empty) return Unauthorized();
        try 
        { 
            return Ok(await _storeService.CreateSaleAsync(academyId, dto)); 
        }
        catch (Exception ex) 
        { 
            return BadRequest(new { message = ex.Message }); 
        }
    }
}
