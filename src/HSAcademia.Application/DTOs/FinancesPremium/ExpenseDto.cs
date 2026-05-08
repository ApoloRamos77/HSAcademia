using System;
using System.Collections.Generic;
using HSAcademia.Domain.Enums;

namespace HSAcademia.Application.DTOs.FinancesPremium;

public class ExpenseDto
{
    public Guid Id { get; set; }
    public ExpenseType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Supplier { get; set; }
    public string? VoucherUrl { get; set; }
    /// <summary>Products created from this purchase (if any).</summary>
    public List<PurchaseProductDto> Products { get; set; } = new();
}

public class CreateExpenseDto
{
    public ExpenseType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Supplier { get; set; }
    public string? VoucherUrl { get; set; }
    /// <summary>Optional products to create/add to inventory from this purchase.</summary>
    public List<CreatePurchaseProductDto> Products { get; set; } = new();
}

/// <summary>Product item linked to a purchase, returned in expense detail.</summary>
public class PurchaseProductDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProductCategory { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal SalePrice { get; set; }
    public bool ForSale { get; set; }
}

/// <summary>Product item to create from a purchase.</summary>
public class CreatePurchaseProductDto
{
    public Guid? ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProductCategory { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    /// <summary>Sale price (0 = pending; product won't appear as available in store).</summary>
    public decimal SalePrice { get; set; }
    /// <summary>True = add to store inventory for sale.</summary>
    public bool ForSale { get; set; }
}

