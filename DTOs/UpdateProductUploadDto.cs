using System;

namespace Alpha.API.DTOs;

public class UpdateProductUploadDto
{
    public Guid SupplierId { get; set; }
    public string? PartNumber { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int QuantityAvailable { get; set; }
    public bool IsActive { get; set; } = true;
    public IFormFile? Image { get; set; }
}