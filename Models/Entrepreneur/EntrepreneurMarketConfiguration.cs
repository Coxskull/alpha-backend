using System;
namespace Alpha.API.Models.Entrepreneur;
public class EntrepreneurMarketConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string CountryCode { get; set; } = string.Empty;

    public string Currency { get; set; } = string.Empty;

    public decimal CommissionRate { get; set; }

    public decimal MinimumPayoutThreshold { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}