using System;

namespace Alpha.API.Models.Entrepreneur;

public class EntrepreneurReferral
{
    public Guid id { get; set; } = Guid.NewGuid();

    public Guid EntrepreneurUserId { get; set; }

    public Guid RecruitedUserId { get; set; }

    public string ReferralCode { get; set; } = string.Empty;

    public DateTime ReferralDate { get; set; } = DateTime.UtcNow;

    public DateTime? ProviderActivationDate { get; set; }

    public string ReferralStatus { get; set; } = "pending";

    public string EligibilityStatus { get; set; } = "pending";

    public bool IsDirectReferral { get; set; } = true;

    public DateTime? EndedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string EntrepreneurEligibilityStatus { get; set; } = "pending";
}