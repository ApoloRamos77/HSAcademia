using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Store;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

public class StoreService
{
    private readonly AppDbContext _context;
    private readonly FinancesService _financesService;
    private readonly FinancialPeriodGuard _periods;
    private readonly AuditService _audit;
    private readonly ReceiptRecorder? _receipts;
    private readonly ReceiptService? _receiptService;

    public StoreService(AppDbContext context, FinancesService financesService, FinancialPeriodGuard periods, AuditService audit,
        ReceiptRecorder? receipts = null, ReceiptService? receiptService = null)
    {
        _receipts = receipts;
        _receiptService = receiptService;
        _context = context;
        _financesService = financesService;
        _periods = periods;
        _audit = audit;
    }

    private static ProductDto ToDto(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        ProductCategory = p.ProductCategory,
        CostPrice = p.CostPrice,
        Price = p.Price,
        Stock = p.Stock,
        IsActive = p.IsActive
    };

    private static void ValidateProduct(CreateProductDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new Exception("El nombre del producto es obligatorio.");
        if (dto.Price < 0 || dto.CostPrice < 0) throw new Exception("Los precios no pueden ser negativos.");
        if (dto.Stock < 0) throw new Exception("El stock no puede ser negativo.");
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
        ValidateProduct(dto);
        var product = new Product
        {
            AcademyId = academyId,
            Name = dto.Name.Trim(),
            Description = dto.Description,
            ProductCategory = dto.ProductCategory,
            CostPrice = dto.CostPrice,
            Price = dto.Price,
            Stock = dto.Stock,
            IsActive = true
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return ToDto(product);
    }

    public async Task<ProductDto> UpdateProductAsync(Guid academyId, Guid id, CreateProductDto dto)
    {
        ValidateProduct(dto);
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && p.AcademyId == academyId);
        if (product == null) throw new Exception("Producto no encontrado.");

        if (product.Stock != dto.Stock)
            _audit.Add(academyId, "store.product.stock_adjust", nameof(Product), product.Id,
                new { before = product.Stock, after = dto.Stock });

        product.Name = dto.Name.Trim();
        product.Description = dto.Description;
        product.ProductCategory = dto.ProductCategory;
        product.CostPrice = dto.CostPrice;
        product.Price = dto.Price;
        product.Stock = dto.Stock;

        await _context.SaveChangesAsync();
        return ToDto(product);
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
                ReceiptNumber = s.ReceiptNumber,
                Notes = s.Notes
            })
            .ToListAsync();
    }

    public async Task VoidSaleAsync(Guid academyId, Guid saleId)
    {
        var sale = await _context.ProductSales
            .Include(s => s.Product)
            .FirstOrDefaultAsync(s => s.Id == saleId && s.AcademyId == academyId && !s.IsDeleted);

        if (sale == null) throw new Exception("Venta no encontrada o ya fue anulada.");

        await _periods.EnsureOpenAsync(academyId, AcademyClock.ToLocalDate(sale.SaleDate), "anular la venta");

        // Venta registrada dentro de un comprobante: si el recibo solo contiene esta venta se anula completo;
        // si tiene otros ítems (mensualidades, otros productos) se debe anular el recibo desde Finanzas.
        var receiptLine = await _context.ReceiptLines.Include(l => l.Receipt).ThenInclude(r => r.Lines)
            .FirstOrDefaultAsync(l => l.ProductSaleId == sale.Id && l.Receipt.AcademyId == academyId);
        if (receiptLine != null && receiptLine.Receipt.Status == ReceiptStatus.Issued && _receiptService != null)
        {
            if (receiptLine.Receipt.Lines.Count > 1)
                throw new Exception($"Esta venta es parte del recibo N° {receiptLine.Receipt.Number} junto con otros ítems. Anula el recibo completo desde Finanzas › Recibos.");
            await _receiptService.VoidLoadedAsync(academyId, receiptLine.Receipt, "Venta anulada desde Tienda");
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { throw new Exception("El stock del producto cambió mientras se anulaba la venta. Inténtalo nuevamente."); }
            return;
        }

        sale.IsDeleted = true;
        sale.DeletedAt = DateTime.UtcNow;

        if (sale.Product != null)
            sale.Product.Stock += sale.Quantity;

        // PaymentRecord exclusivo ligado a la venta (flujo antiguo)
        var paymentRecord = await _context.PaymentRecords
            .FirstOrDefaultAsync(pr => pr.ProductSaleId == sale.Id);
        if (paymentRecord != null)
        {
            paymentRecord.IsDeleted = true;
            paymentRecord.DeletedAt = DateTime.UtcNow;
        }

        _audit.Add(academyId, "store.sale.void", nameof(ProductSale), sale.Id,
            new { sale.ReceiptNumber, sale.Quantity, sale.TotalPrice, sale.StudentId });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new Exception("El stock del producto cambió mientras se anulaba la venta. Inténtalo nuevamente.");
        }
    }

    private static PaymentMethod ParseMethod(string? value) => value switch
    {
        "Yape"         => PaymentMethod.Yape,
        "Plin"         => PaymentMethod.Plin,
        "BankTransfer" => PaymentMethod.BankTransfer,
        "Card"         => PaymentMethod.Card,
        _              => PaymentMethod.Cash
    };

    public async Task<ProductSaleDto> CreateSaleAsync(Guid academyId, CreateProductSaleDto dto)
    {
        if (dto.Quantity <= 0) throw new Exception("La cantidad debe ser mayor a cero.");
        if (dto.CustomDiscount.HasValue && dto.CustomDiscount.Value < 0) throw new Exception("El descuento no puede ser negativo.");

        var saleTimestamp = dto.SaleDate.HasValue
            ? DateTime.SpecifyKind(dto.SaleDate.Value.ToUniversalTime(), DateTimeKind.Utc)
            : DateTime.UtcNow;
        await _periods.EnsureOpenAsync(academyId, AcademyClock.ToLocalDate(saleTimestamp), "registrar la venta");

        if (dto.StudentId.HasValue &&
            !await _context.Students.AnyAsync(s => s.Id == dto.StudentId.Value && s.AcademyId == academyId))
            throw new Exception("El alumno seleccionado no es válido.");

        try
        {
            return await _context.InTransactionAsync(async () =>
            {
                // 1) Número de recibo primero: si algo falla después, la transacción lo revierte (sin saltos).
                var receiptNumber = await _financesService.ReserveReceiptNumberAsync(academyId);

                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.AcademyId == academyId)
                    ?? throw new Exception("Producto no encontrado.");
                if (product.Stock < dto.Quantity) throw new Exception($"Stock insuficiente. Disponible: {product.Stock}.");

                var baseTotal = product.Price * dto.Quantity;
                var discount = dto.IsGift ? baseTotal : (dto.CustomDiscount ?? 0m);
                if (discount > baseTotal) throw new Exception("El descuento no puede ser mayor al precio total.");

                product.Stock -= dto.Quantity;

                var sale = new ProductSale
                {
                    AcademyId = academyId,
                    ProductId = dto.ProductId,
                    StudentId = dto.StudentId,
                    Quantity = dto.Quantity,
                    UnitPrice = product.Price,
                    TotalPrice = dto.IsGift ? 0 : (baseTotal - discount),
                    IsGift = dto.IsGift,
                    DiscountAmount = discount,
                    SaleDate = saleTimestamp,
                    ReceiptNumber = receiptNumber,
                    Notes = dto.Notes
                };
                _context.ProductSales.Add(sale);

                // Cabecera del comprobante con sus ítems (producto + mensualidad combinada si la hay)
                Receipt? receipt = null;
                if (_receipts != null)
                {
                    var buyer = dto.StudentId.HasValue
                        ? await _context.Students.Include(s => s.Guardian).FirstOrDefaultAsync(s => s.Id == dto.StudentId.Value)
                        : null;
                    var (payer, document, guardianId) = ReceiptRecorder.PayerOf(buyer);
                    receipt = await _receipts.CreateAsync(academyId, receiptNumber, saleTimestamp, payer, document, guardianId,
                        ParseMethod(dto.PaymentMethod), dto.OperationNumber, dto.VoucherUrl, dto.Notes, "tienda");
                    ReceiptRecorder.AddLine(receipt, new ReceiptLine
                    {
                        Type = ReceiptLineType.Product,
                        Description = dto.IsGift ? $"{product.Name} (obsequio)" : product.Name,
                        StudentId = buyer?.Id,
                        StudentName = buyer != null ? $"{buyer.FirstName} {buyer.LastName}".Trim() : null,
                        Quantity = dto.Quantity,
                        UnitPrice = product.Price,
                        Discount = discount,
                        Total = sale.TotalPrice,
                        ProductSaleId = sale.Id,
                        ProductId = product.Id
                    });
                }

                string? combinedMonthlyDesc = null;
                decimal? combinedMonthlyAmount = null;

                // Recibo combinado: mensualidad + producto con el mismo número de recibo
                if (dto.PaymentRecordId.HasValue && dto.MonthlyAmountPaid.HasValue && dto.MonthlyAmountPaid.Value > 0)
                {
                    var paymentRecord = await _context.PaymentRecords
                        .FirstOrDefaultAsync(pr => pr.Id == dto.PaymentRecordId.Value && pr.AcademyId == academyId)
                        ?? throw new Exception("La mensualidad seleccionada no existe.");

                    if (dto.StudentId.HasValue && paymentRecord.StudentId != dto.StudentId.Value)
                        throw new Exception("La mensualidad seleccionada no pertenece al alumno de la venta.");

                    if (!paymentRecord.IsPaid)
                    {
                        var pending = paymentRecord.Amount - paymentRecord.AmountPaid;
                        var toPay   = Math.Min(dto.MonthlyAmountPaid.Value, pending);

                        combinedMonthlyDesc = paymentRecord.Description;
                        combinedMonthlyAmount = toPay;

                        var combinedInstallment = new PaymentInstallment
                        {
                            PaymentRecordId = paymentRecord.Id,
                            AmountPaid      = toPay,
                            PaidAt          = saleTimestamp,
                            Method          = ParseMethod(dto.PaymentMethod),
                            OperationNumber = dto.OperationNumber,
                            VoucherUrl      = dto.VoucherUrl,
                            ReceiptNumber   = receiptNumber,
                            Notes           = $"Pago combinado — recibo #{receiptNumber}"
                        };
                        _context.PaymentInstallments.Add(combinedInstallment);
                        if (receipt != null)
                        {
                            var owner = await _context.Students.Where(s => s.Id == paymentRecord.StudentId)
                                .Select(s => s.FirstName + " " + s.LastName).FirstOrDefaultAsync();
                            ReceiptRecorder.AddLine(receipt, new ReceiptLine
                            {
                                Type = ReceiptLineType.Debt,
                                Description = paymentRecord.Description,
                                StudentId = paymentRecord.StudentId,
                                StudentName = owner,
                                Quantity = 1,
                                UnitPrice = toPay,
                                Total = toPay,
                                PaymentRecordId = paymentRecord.Id,
                                PaymentInstallmentId = combinedInstallment.Id
                            });
                        }

                        paymentRecord.AmountPaid += toPay;
                        if (paymentRecord.AmountPaid >= paymentRecord.Amount - 0.01m)
                        {
                            paymentRecord.AmountPaid = paymentRecord.Amount;
                            paymentRecord.IsPaid     = true;
                            paymentRecord.PaidDate   = saleTimestamp;
                        }
                    }
                }

                _audit.Add(academyId, "store.sale.create", nameof(ProductSale), sale.Id,
                    new { receiptNumber, product = product.Name, dto.Quantity, sale.TotalPrice, dto.IsGift, discount,
                          combinedPaymentRecordId = dto.PaymentRecordId, combinedMonthlyAmount });

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
                    IsGift = sale.IsGift,
                    DiscountAmount = sale.DiscountAmount,
                    SaleDate = sale.SaleDate,
                    ReceiptNumber = sale.ReceiptNumber,
                    Notes = sale.Notes,
                    CombinedMonthlyDescription = combinedMonthlyDesc,
                    CombinedMonthlyAmount = combinedMonthlyAmount
                };
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            throw new Exception("Otro usuario modificó el stock o la mensualidad mientras se registraba la venta. Inténtalo nuevamente.");
        }
    }
}
