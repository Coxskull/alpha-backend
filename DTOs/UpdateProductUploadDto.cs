using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Alpha.API.DTOs;

public class UpdateProductUploadDto
{
    [Required]
    public Guid SupplierId { get; set; }

    public string? PartNumber { get; set; }

    [Required]
    public string Brand { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int QuantityAvailable { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Currency { get; set; }

    public string? CountryCode { get; set; }

    public IFormFile? Image { get; set; }
}