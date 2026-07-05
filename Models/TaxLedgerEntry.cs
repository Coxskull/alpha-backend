using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("tax_ledger_entries")]
public class TaxLedgerEntry
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_id")]
    public Guid? OrderId { get; set; }

    [Column("payment_id")]
    public Guid? PaymentId { get; set; }

    [Column("settlement_id")]
    public Guid? SettlementId { get; set; }

    [Column("provider_id")]
    public Guid? ProviderId { get; set; }

    [Column("entry_type")]
    public string EntryType { get; set; } = "calculation";

    [Column("tax_type")]
    public string TaxType { get; set; } = "IVA";

    [Column("component")]
    public string Component { get; set; } = "";

    [Column("tax_rate")]
    public decimal TaxRate { get; set; }

    [Column("taxable_base")]
    public decimal TaxableBase { get; set; }

    [Column("tax_collected")]
    public decimal TaxCollected { get; set; }

    [Column("tax_withheld")]
    public decimal TaxWithheld { get; set; }

    [Column("tax_refunded")]
    public decimal TaxRefunded { get; set; }

    [Column("responsible_party")]
    public string ResponsibleParty { get; set; } = "";

    [Column("tax_rule_version")]
    public int TaxRuleVersion { get; set; }

    [Column("actor")]
    public string Actor { get; set; } = "system";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}