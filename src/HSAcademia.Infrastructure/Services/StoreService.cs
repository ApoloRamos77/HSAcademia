using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Store;
using HSAcademia.Domain.Entities;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

public class StoreService
{
    private readonly AppDbContext _context;

    public StoreService(AppDbContext context)
    {
        _context = context;
    }

    // --- Products ---
    public async Task<List<ProductDto>> GetProductsAsync(Guid academyId)
    {
        return await _context.Products
            .Where(p => p.AcademyId == academyId)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                ProductCategory = p.ProductCategory,
                Price = p.Price,
                Stock = p.Stock,
                IsActive = p.IsActive
            })
            .ToListAsync();
    }

    public async Task<ProductDto> CreateProductAsync(Guid academyId, CreateProductDto dto)
    {
        var product = new Product
        {
            AcademyId = academyId,
            Name = dto.Name,
            Description = dto.Description,
            ProductCategory = dto.ProductCategory,
            Price = dto.Price,
            Stock = dto.Stock,
            IsActive = true
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            ProductCategory = product.ProductCategory,
            Price = product.Price,
            Stock = product.Stock,
            IsActive = product.IsActive
        };
    }

    public async Task<ProductDto> UpdateProductAsync(Guid academyId, Guid id, CreateProductDto dto)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.AcademyId == academyId);
        if (product == null) throw new Exception("Producto no encontrado.");

        product.Name = dto.Name;
        product.Description = dto.Description;
        product.ProductCategory = dto.ProductCategory;
        product.Price = dto.Price;
        product.Stock = dto.Stock;

        await _context.SaveChangesAsync();

        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            ProductCategory = product.ProductCategory,
            Price = product.Price,
            Stock = product.Stock,
            IsActive = product.IsActive
        };
    }

    // --- Sales ---
    public async Task<List<ProductSaleDto>> GetSalesAsync(Guid academyId)
    {
        return await _context.ProductSales
            .Include(s => s.Product)
            .Include(s => s.Student)
            .Where(s => s.AcademyId == academyId)
            .OrderByDescending(s => s.SaleDate)
            .Select(s => new ProductSaleDto
            {
                Id = s.Id,
                ProductId = s.ProductId,
                ProductName = s.Product.Name,
                StudentId = s.StudentId,
                StudentName = s.Student != null ? s.Student.FirstName + " " + s.Student.LastName : "Público General",
                Quantity = s.Quantity,
                UnitPrice = s.UnitPrice,
                TotalPrice = s.TotalPrice,
                SaleDate = s.SaleDate
            })
            .ToListAsync();
    }

    public async Task<ProductSaleDto> CreateSaleAsync(Guid academyId, CreateProductSaleDto dto)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.AcademyId == academyId);
        if (product == null) throw new Exception("Producto no encontrado.");

        if (product.Stock < dto.Quantity) throw new Exception("Stock insuficiente.");

        // Deduct stock
        product.Stock -= dto.Quantity;

        var sale = new ProductSale
        {
            AcademyId = academyId,
            ProductId = dto.ProductId,
            StudentId = dto.StudentId,
            Quantity = dto.Quantity,
            UnitPrice = product.Price,
            TotalPrice = product.Price * dto.Quantity,
            SaleDate = DateTime.UtcNow
        };

        _context.ProductSales.Add(sale);
        await _context.SaveChangesAsync();

        return new ProductSaleDto
        {
            Id = sale.Id,
            ProductId = sale.ProductId,
            ProductName = product.Name,
            StudentId = sale.StudentId,
            Quantity = sale.Quantity,
            UnitPrice = sale.UnitPrice,
            TotalPrice = sale.TotalPrice,
            SaleDate = sale.SaleDate
        };
    }
}
