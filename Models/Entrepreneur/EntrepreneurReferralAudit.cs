using System;

namespace Alpha.API.Models.Entrepreneur;

public class EntrepreneurReferralAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EntrepreneurReferralId { get; set; }

    public Guid? OldEntrepreneurUserId { get; set; }

    public Guid? NewEntrepreneurUserId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid ChangedByUserId { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}