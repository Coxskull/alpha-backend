using System;

namespace Alpha.API.Models;

public class Product
{
    public Guid Id { get; set; }

    public Guid SupplierId { get; set; }

    public string Brand { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int QuantityAvailable { get; set; }

    public bool IsActive { get; set; } = true;
}