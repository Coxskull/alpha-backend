using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("auto_parts_commission_tiers")]
public class AutoPartsCommissionTier
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("policy_id")]
    public Guid PolicyId { get; set; }

    [Column("tier_order")]
    public int TierOrder { get; set; }

    [Column("minimum_amount")]
    public decimal MinimumAmount { get; set; }

    [Column("maximum_amount")]
    public decimal? MaximumAmount { get; set; }

    [Column("commission_percentage")]
    public decimal CommissionPercentage { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    public AutoPartsCommissionPolicy? Policy { get; set; }
}