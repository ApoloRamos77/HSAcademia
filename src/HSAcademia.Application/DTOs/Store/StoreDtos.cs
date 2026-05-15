using System;

namespace HSAcademia.Application.DTOs.Store;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProductCategory { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; }
}

public class CreateProductDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProductCategory { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

public class ProductSaleDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    
    public Guid? StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public bool IsGift { get; set; }
    public decimal DiscountAmount { get; set; }
    public DateTime SaleDate { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
    
    /// <summary>Populated when a monthly fee was also paid in the same receipt.</summary>
    public string? CombinedMonthlyDescription { get; set; }
    public decimal? CombinedMonthlyAmount { get; set; }
}

public class CreateProductSaleDto
{
    public Guid ProductId { get; set; }
    public Guid? StudentId { get; set; }
    public int Quantity { get; set; }
    public bool IsGift { get; set; }

    /// <summary>Optional override for the sale timestamp. Defaults to UTC now.</summary>
    public DateTime? SaleDate { get; set; }

    // Optional payment context (from mobile POS)
    public string? PaymentMethod { get; set; }
    public string? OperationNumber { get; set; }
    public string? VoucherUrl { get; set; }
    public string? Notes { get; set; }
    
    public decimal? CustomDiscount { get; set; }

    /// <summary>
    /// Optional: If provided, also registers a payment installment for this monthly fee record,
    /// combining store product + monthly payment into the same receipt number.
    /// </summary>
    public Guid? PaymentRecordId { get; set; }
    /// <summary>Amount to pay toward the monthly fee (required when PaymentRecordId is set).</summary>
    public decimal? MonthlyAmountPaid { get; set; }
}
