using System;

namespace Alpha.API.DTOs.AutoPartsCommission.Admin;

public class PreviewAutoPartsCommissionDto
{
    public decimal Subtotal { get; set; }

    public string Currency { get; set; } = "USD";

    public DateTime? CalculationDate { get; set; }
}