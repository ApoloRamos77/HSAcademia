using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Store;
using HSAcademia.Domain.Entities;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static HSAcademia.Domain.Entities.PaymentMethod;

namespace HSAcademia.Infrastructure.Services;

public class StoreService
{
    private readonly AppDbContext _context;
    private readonly FinancesService _financesService;

    public StoreService(AppDbContext context, FinancesService financesService)
    {
        _context = context;
        _financesService = financesService;
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
                CostPrice = p.CostPrice,
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
            CostPrice = dto.CostPrice,
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
            CostPrice = product.CostPrice,
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
        product.CostPrice = dto.CostPrice;
        product.Price = dto.Price;
        product.Stock = dto.Stock;

        await _context.SaveChangesAsync();

        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            ProductCategory = product.ProductCategory,
            CostPrice = product.CostPrice,
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
                ProductName = s.Product != null ? s.Product.Name : "Producto No Disponible",
                StudentId = s.StudentId,
                StudentName = s.Student != null ? s.Student.FirstName + " " + s.Student.LastName : "Público General",
                Quantity = s.Quantity,
                UnitPrice = s.UnitPrice,
                TotalPrice = s.TotalPrice,
                IsGift = s.IsGift,
                DiscountAmount = s.DiscountAmount,
                SaleDate = s.SaleDate,
                ReceiptNumber = s.ReceiptNumber
            })
            .ToListAsync();
    }

    public async Task VoidSaleAsync(Guid academyId, Guid saleId)
    {
        var sale = await _context.ProductSales
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.Id == saleId && s.AcademyId == academyId && !s.IsDeleted);

        if (sale == null) throw new Exception("Venta no encontrada o ya fue anulada.");

        // Soft delete the sale
        sale.IsDeleted = true;
        sale.DeletedAt = DateTime.UtcNow;

        // Restore stock
        if (sale.Product != null)
        {
            sale.Product.Stock += sale.Quantity;
        }

        // Check if there is an exclusive PaymentRecord linked to this sale (Legacy flow)
        var paymentRecord = await _context.PaymentRecords
            .FirstOrDefaultAsync(pr => pr.ProductSaleId == sale.Id && !pr.IsDeleted);
        
        if (paymentRecord != null)
        {
            paymentRecord.IsDeleted = true;
            paymentRecord.DeletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<ProductSaleDto> CreateSaleAsync(Guid academyId, CreateProductSaleDto dto)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.AcademyId == academyId);
        if (product == null) throw new Exception("Producto no encontrado.");

        if (product.Stock < dto.Quantity) throw new Exception("Stock insuficiente.");

        // Deduct stock
        product.Stock -= dto.Quantity;

        // Correct date: if a custom date is provided convert to UTC properly;
        // otherwise use UTC now so the value is always set (no DB default).
        var saleTimestamp = dto.SaleDate.HasValue
            ? DateTime.SpecifyKind(dto.SaleDate.Value.ToUniversalTime(), DateTimeKind.Utc)
            : DateTime.UtcNow;

        // Generate receipt number from the centralized counter
        var receiptNumber = await _financesService.GenerateNextReceiptNumberAsync(academyId);

        var sale = new ProductSale
        {
            AcademyId = academyId,
            ProductId = dto.ProductId,
            StudentId = dto.StudentId,
            Quantity = dto.Quantity,
            UnitPrice = product.Price,
            TotalPrice = dto.IsGift ? 0 : (product.Price * dto.Quantity),
            IsGift = dto.IsGift,
            DiscountAmount = dto.IsGift ? (product.Price * dto.Quantity) : 0,
            SaleDate = saleTimestamp,
            ReceiptNumber = receiptNumber
        };

        _context.ProductSales.Add(sale);
        await _context.SaveChangesAsync();

        string? combinedMonthlyDesc = null;
        decimal? combinedMonthlyAmount = null;

        // ── Flujo 1: Recibo combinado (mensualidad + producto en un solo recibo) ──────
        // El frontend envía un PaymentRecordId explícito y el monto a abonar.
        if (dto.PaymentRecordId.HasValue && dto.MonthlyAmountPaid.HasValue && dto.MonthlyAmountPaid.Value > 0)
        {
            var paymentRecord = await _context.PaymentRecords
                .FirstOrDefaultAsync(pr => pr.Id == dto.PaymentRecordId.Value && pr.AcademyId == academyId);

            if (paymentRecord != null && !paymentRecord.IsPaid)
            {
                var method = (dto.PaymentMethod ?? "Cash") switch
                {
                    "Yape"         => PaymentMethod.Yape,
                    "Plin"         => PaymentMethod.Plin,
                    "BankTransfer" => PaymentMethod.BankTransfer,
                    "Card"         => PaymentMethod.Card,
                    _              => PaymentMethod.Cash
                };

                var pending = paymentRecord.Amount - paymentRecord.AmountPaid;
                var toPay   = Math.Min(dto.MonthlyAmountPaid.Value, pending);

                // Capture for response
                combinedMonthlyDesc = paymentRecord.Description;
                combinedMonthlyAmount = toPay;

                // Reuse the same receipt number so the receipt PDF covers both items
                _context.PaymentInstallments.Add(new PaymentInstallment
                {
                    PaymentRecordId = paymentRecord.Id,
                    AmountPaid      = toPay,
                    PaidAt          = saleTimestamp,
                    Method          = method,
                    OperationNumber = dto.OperationNumber,
                    VoucherUrl      = dto.VoucherUrl,
                    ReceiptNumber   = receiptNumber,
                    Notes           = $"Pago combinado — recibo #{receiptNumber}"
                });

                paymentRecord.AmountPaid += toPay;
                if (paymentRecord.AmountPaid >= paymentRecord.Amount - 0.01m)
                {
                    paymentRecord.AmountPaid = paymentRecord.Amount;
                    paymentRecord.IsPaid     = true;
                    paymentRecord.PaidDate   = saleTimestamp;
                }

                await _context.SaveChangesAsync();
            }
        }
        // ── Flujo 2: Pago móvil legacy (PaymentRecord ligado por ProductSaleId) ───────
        else if (!string.IsNullOrEmpty(dto.PaymentMethod))
        {
            var paymentRecord = await _context.PaymentRecords
                .FirstOrDefaultAsync(pr => pr.ProductSaleId == sale.Id && pr.AcademyId == academyId);

            if (paymentRecord != null)
            {
                var method = dto.PaymentMethod switch
                {
                    "Yape"         => PaymentMethod.Yape,
                    "Plin"         => PaymentMethod.Plin,
                    "BankTransfer" => PaymentMethod.BankTransfer,
                    "Card"         => PaymentMethod.Card,
                    _              => PaymentMethod.Cash
                };

                _context.PaymentInstallments.Add(new PaymentInstallment
                {
                    PaymentRecordId = paymentRecord.Id,
                    AmountPaid      = sale.TotalPrice,
                    PaidAt          = DateTime.UtcNow,
                    Method          = method,
                    OperationNumber = dto.OperationNumber,
                    VoucherUrl      = dto.VoucherUrl,
                    ReceiptNumber   = receiptNumber,
                    Notes           = $"Venta móvil — {product.Name} x{dto.Quantity}"
                });

                paymentRecord.IsPaid     = true;
                paymentRecord.PaidDate   = DateTime.UtcNow;
                paymentRecord.AmountPaid = sale.TotalPrice;

                await _context.SaveChangesAsync();
            }
        }


        return new ProductSaleDto
        {
            Id = sale.Id,
            ProductId = sale.ProductId,
            ProductName = product.Name,
            StudentId = sale.StudentId,
            Quantity = sale.Quantity,
            UnitPrice = sale.UnitPrice,
            TotalPrice = sale.TotalPrice,
            IsGift = sale.IsGift,
            DiscountAmount = sale.DiscountAmount,
            SaleDate = sale.SaleDate,
            ReceiptNumber = sale.ReceiptNumber,
            CombinedMonthlyDescription = combinedMonthlyDesc,
            CombinedMonthlyAmount = combinedMonthlyAmount
        };
    }
}
