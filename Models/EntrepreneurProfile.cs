using System;

namespace Alpha.API.Models;

public class EntrepreneurProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string City { get; set; } = string.Empty;

    public string? State { get; set; }

    public string Country { get; set; } = "MX";

    public string PreferredLanguage { get; set; } = "es";

    public string? BusinessName { get; set; }

    public string? EntrepreneurialGoal { get; set; }

    public string OnboardingStatus { get; set; } =
        "roles_selected";

    public DateTime? TermsAcceptedAt { get; set; }

    public DateTime? RewardsPolicyAcceptedAt { get; set; }

    public DateTime CreatedAt { get; set; } =
        DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } =
        DateTime.UtcNow;
}