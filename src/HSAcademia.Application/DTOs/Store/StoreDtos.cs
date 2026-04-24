using System;

namespace HSAcademia.Application.DTOs.Store;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProductCategory { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; }
}

public class CreateProductDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProductCategory { get; set; } = string.Empty;
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
    public DateTime SaleDate { get; set; }
}

public class CreateProductSaleDto
{
    public Guid ProductId { get; set; }
    public Guid? StudentId { get; set; }
    public int Quantity { get; set; }
}
