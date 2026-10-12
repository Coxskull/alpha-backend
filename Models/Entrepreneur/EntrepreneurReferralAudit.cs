using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models.Entrepreneur;

[Table("entrepreneur_referral_audits")]
public class EntrepreneurReferralAudit
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("entrepreneur_referral_id")]
    public Guid EntrepreneurReferralId { get; set; }

    [Column("old_entrepreneur_user_id")]
    public Guid? OldEntrepreneurUserId { get; set; }

    [Column("new_entrepreneur_user_id")]
    public Guid? NewEntrepreneurUserId { get; set; }

    [Column("reason")]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [Column("changed_by_user_id")]
    public Guid ChangedByUserId { get; set; }

    [Column("changed_at")]
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
