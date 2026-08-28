using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models.Entrepreneur;

[Table("entrepreneur_market_configurations")]
public class EntrepreneurMarketConfiguration
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("country_code")]
    public string CountryCode { get; set; } = string.Empty;

    [Column("currency")]
    public string Currency { get; set; } = string.Empty;

    [Column("commission_rate")]
    public decimal CommissionRate { get; set; } = 0.05m;

    [Column("minimum_payout_threshold")]
    public decimal MinimumPayoutThreshold { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}