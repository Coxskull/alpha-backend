using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("tax_rules")]
public class TaxRule
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("country")]
    public string Country { get; set; } = "MX";

    [Column("region")]
    public string? Region { get; set; }

    [Column("tax_type")]
    public string TaxType { get; set; } = "IVA";

    [Column("tax_rate")]
    public decimal TaxRate { get; set; }

    [Column("component")]
    public string Component { get; set; } = "";

    [Column("responsible_party")]
    public string ResponsibleParty { get; set; } = "";

    [Column("is_tax_inclusive")]
    public bool IsTaxInclusive { get; set; }

    [Column("withholding_required")]
    public bool WithholdingRequired { get; set; }

    [Column("withholding_rate")]
    public decimal WithholdingRate { get; set; }

    [Column("effective_from")]
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; } = true;

    [Column("version")]
    public int Version { get; set; } = 1;
}