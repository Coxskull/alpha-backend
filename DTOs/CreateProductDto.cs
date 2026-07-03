using System;

namespace Alpha.API.DTOs;

public class CreateProductDto
{
    public Guid SupplierId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int QuantityAvailable { get; set; }
}