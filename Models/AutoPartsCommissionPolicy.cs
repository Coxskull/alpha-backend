using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("auto_parts_commission_policies")]
public class AutoPartsCommissionPolicy
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("policy_name")]
    public string PolicyName { get; set; } = string.Empty;

    [Column("currency")]
    public string Currency { get; set; } = "USD";

    [Column("version")]
    public int Version { get; set; }

    [Column("effective_from")]
    public DateTime EffectiveFrom { get; set; }

    [Column("effective_to")]
    public DateTime? EffectiveTo { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_by_user_id")]
    public Guid? CreatedByUserId { get; set; }

    [Column("updated_by_user_id")]
    public Guid? UpdatedByUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    public ICollection<AutoPartsCommissionTier> Tiers { get; set; }
        = new List<AutoPartsCommissionTier>();
}