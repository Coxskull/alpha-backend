using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Alpha.API.Models;

[Table("entrepreneur_profiles")]
public class EntrepreneurProfile
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("city")]
    public string? City { get; set; }

    [Column("state")]
    public string? State { get; set; }

    [Column("country")]
    public string Country { get; set; } = "MX";

    [Column("preferred_language")]
    public string PreferredLanguage { get; set; } = "es";

    [Column("business_name")]
    public string? BusinessName { get; set; }

    [Column("entrepreneurial_goal")]
    public string? EntrepreneurialGoal { get; set; }

    [Column("onboarding_status")]
    public string OnboardingStatus { get; set; } = "started";

    [Column("terms_accepted_at")]
    public DateTime? TermsAcceptedAt { get; set; }

    [Column("rewards_policy_accepted_at")]
    public DateTime? RewardsPolicyAcceptedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}