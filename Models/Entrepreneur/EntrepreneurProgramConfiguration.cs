using System;

namespace Alpha.API.Models.Entrepreneur;

public class EntrepreneurProgramConfiguration
{
    public Guid id { get; set; } = Guid.NewGuid();

    public bool ProgramEnabled { get; set; } = true;

    public decimal DefaultCommissionRate { get; set; } = 0.05m;

    public decimal MinimumPayoutThreshold { get; set; } = 0m;

    public string PayoutFrequency { get; set; } = "TWICE_MONTHLY";

    public string QualifyingProviderRoles { get; set; }
        = "driver,mechanic,supplier";

    public string QualifyingTransactionTypes { get; set; }
        = "marketplace_order";

    public int HoldingPeriodDays { get; set; } = 7;

    public DateTime? ProgramStartDate { get; set; }

    public DateTime? ProgramEndDate { get; set; }

    public int MaximumReferralLevel { get; set; } = 1;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Guid? UpdatedByUserId { get; set; }
}