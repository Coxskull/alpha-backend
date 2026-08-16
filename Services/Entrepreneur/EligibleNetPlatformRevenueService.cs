using System;

namespace Alpha.API.Services.Entrepreneur;

public class EligibleNetPlatformRevenueService
{
    public decimal Calculate(
        decimal alphaGrossPlatformCommission,
        decimal directTransactionCosts)
    {
        if (alphaGrossPlatformCommission <= 0m)
            return 0m;

        var result =
            alphaGrossPlatformCommission -
            Math.Max(0m, directTransactionCosts);

        return Math.Max(0m, result);
    }
}