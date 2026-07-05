using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("tax_calculations")]
public class TaxCalculation
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_id")]
    public Guid? OrderId { get; set; }

    [Column("payment_id")]
    public Guid? PaymentId { get; set; }

    [Column("country")]
    public string Country { get; set; } = "MX";

    [Column("region")]
    public string? Region { get; set; }

    [Column("currency")]
    public string Currency { get; set; } = "MXN";

    [Column("component")]
    public string Component { get; set; } = "";

    [Column("tax_type")]
    public string TaxType { get; set; } = "IVA";

    [Column("tax_rate")]
    public decimal TaxRate { get; set; }

    [Column("taxable_base")]
    public decimal TaxableBase { get; set; }

    [Column("tax_amount")]
    public decimal TaxAmount { get; set; }

    [Column("revenue_recipient")]
    public string RevenueRecipient { get; set; } = "";

    [Column("tax_responsible_party")]
    public string TaxResponsibleParty { get; set; } = "";

    [Column("withholding_required")]
    public bool WithholdingRequired { get; set; }

    [Column("withholding_amount")]
    public decimal WithholdingAmount { get; set; }

    [Column("tax_rule_id")]
    public Guid TaxRuleId { get; set; }

    [Column("tax_rule_version")]
    public int TaxRuleVersion { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}