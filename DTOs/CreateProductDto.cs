using System;
using System.ComponentModel.DataAnnotations;

namespace Alpha.API.DTOs;

public class CreateProductDto
{
    [Required]
    public Guid SupplierId { get; set; }

    public string? PartNumber { get; set; }

    [Required]
    public string Brand { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int QuantityAvailable { get; set; }

    public string Currency { get; set; } = "MXN";

    public string CountryCode { get; set; } = "MX";
}