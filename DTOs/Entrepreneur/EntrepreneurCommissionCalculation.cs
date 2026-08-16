namespace Alpha.API.DTOs.Entrepreneur;

public class EntrepreneurCommissionCalculation
{
    public decimal AlphaGrossPlatformCommission { get; set; }

    public decimal DirectTransactionCosts { get; set; }

    public decimal EligibleNetPlatformRevenue { get; set; }

    public decimal EntrepreneurRate { get; set; }

    public decimal EntrepreneurCommission { get; set; }

    public decimal AlphaRetainedRevenue { get; set; }

    public bool Eligible { get; set; }

    public string? Reason { get; set; }
}