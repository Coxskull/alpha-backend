using System;

namespace Alpha.API.Models;

public class AutoPartsCommissionCalculationLine
{
    public Guid Id { get; set; }

    public Guid CalculationId { get; set; }

    public Guid TierId { get; set; }

    public int TierOrder { get; set; }

    public decimal TierMinimum { get; set; }

    public decimal? TierMaximum { get; set; }

    public decimal TierPercentage { get; set; }

    public decimal AmountInTier { get; set; }

    public decimal CommissionAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public AutoPartsCommissionCalculation? Calculation { get; set; }

    public AutoPartsCommissionTier? Tier { get; set; }
}