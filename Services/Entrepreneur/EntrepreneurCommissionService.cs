using Alpha.API.Data;
using Alpha.API.Models.Entrepreneur;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using Alpha.API.Models;

namespace Alpha.API.Services.Entrepreneur;

public class EntrepreneurCommissionService
{
    private readonly AppDbContext _context;
    private readonly EntrepreneurEligibilityService _eligibility;
    private readonly DirectTransactionCostService _costService;
    private readonly EligibleNetPlatformRevenueService _netRevenue;
    private readonly EntrepreneurConfigurationService _configuration;

    public EntrepreneurCommissionService(
        AppDbContext context,
        EntrepreneurEligibilityService eligibility,
        DirectTransactionCostService costService,
        EligibleNetPlatformRevenueService netRevenue,
        EntrepreneurConfigurationService configuration)
    {
        _context = context;
        _eligibility = eligibility;
        _costService = costService;
        _netRevenue = netRevenue;
        _configuration = configuration;
    }

    public async Task<EntrepreneurEarning?>
        GenerateAsync(
            Guid recruitedUserId,
            Guid entrepreneurUserId,
            Guid orderId,
            Guid paymentId,
            decimal alphaGrossPlatformCommission,
            string currency,
            string providerRole,
            CancellationToken cancellationToken = default)
    {
        if (alphaGrossPlatformCommission <= 0m)
            return null;

        var order =
            await _context.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == orderId,
                    cancellationToken);

        if (order == null)
            return null;

        var payment =
            await _context.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == paymentId &&
                        x.OrderId == orderId,
                    cancellationToken);

        if (payment == null)
            return null;

        if (!string.Equals(
                payment.PaymentStatus,
                "paid",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(
                order.Status,
                "cancelled",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var config =
            await _configuration.GetAsync(
                cancellationToken);

        if (config == null ||
            !config.ProgramEnabled)
        {
            return null;
        }

        var now =
            DateTime.UtcNow;

        if (config.ProgramStartDate.HasValue &&
            now < config.ProgramStartDate.Value)
        {
            return null;
        }

        if (config.ProgramEndDate.HasValue &&
            now > config.ProgramEndDate.Value)
        {
            return null;
        }

        if (config.MaximumReferralLevel != 1)
        {
            throw new InvalidOperationException(
                "Entrepreneur Network maximum referral level must be 1.");
        }

        var eligibility =
            await _eligibility.CheckAsync(
                recruitedUserId,
                entrepreneurUserId,
                orderId,
                providerRole,
                cancellationToken);

        if (!eligibility.Eligible)
            return null;

        var duplicate =
            await _context
                .EntrepreneurEarnings
                .AnyAsync(
                    x =>
                        x.OrderId ==
                            orderId
                        &&
                        x.RecruitedProviderId ==
                            recruitedUserId
                        &&
                        x.EntrepreneurUserId ==
                            entrepreneurUserId,
                    cancellationToken);

        if (duplicate)
            return null;

        var directCosts =
            await _costService.CalculateAsync(
                orderId,
                cancellationToken);

        var eligibleNetRevenue =
            _netRevenue.Calculate(
                alphaGrossPlatformCommission,
                directCosts);

        if (eligibleNetRevenue <= 0m)
            return null;

        var rate =
            await _configuration.GetRateAsync(
                order.CountryCode,
                currency,
                cancellationToken);

        if (rate <= 0m)
            return null;

        if (rate > 1m)
        {
            throw new InvalidOperationException(
                "Entrepreneur commission rate must be stored as a decimal fraction. Example: 5% = 0.05.");
        }

        var entrepreneurAmount =
            decimal.Round(
                eligibleNetRevenue * rate,
                2,
                MidpointRounding.AwayFromZero);

        if (entrepreneurAmount <= 0m)
            return null;

        var earning =
            new EntrepreneurEarning
            {
                Id = Guid.NewGuid(),

                EntrepreneurUserId =
                    entrepreneurUserId,

                RecruiterId =
                    entrepreneurUserId,

                RecruitedProviderId =
                    recruitedUserId,

                ProviderRole =
                    providerRole
                        .Trim()
                        .ToLowerInvariant(),

                OrderId =
                    orderId,

                TransactionId =
                    payment.TransactionReference
                    ?? orderId.ToString(),

                PaymentId =
                    paymentId,

                TransactionDate =
                    payment.PaidAt ??
                    now,

                AlphaGrossPlatformCommission =
                    decimal.Round(
                        alphaGrossPlatformCommission,
                        2),

                DirectTransactionCosts =
                    directCosts,

                EligibleNetPlatformRevenue =
                    eligibleNetRevenue,

                EntrepreneurPercentage =
                    rate,

                EntrepreneurEarningsAmount =
                    entrepreneurAmount,

                Currency =
                    currency
                        .Trim()
                        .ToUpperInvariant(),

                EarningStatus =
                    "PENDING",

                RefundAdjustment =
                    0m,

                ChargebackAdjustment =
                    0m,

                CreatedAt =
                    now,

                UpdatedAt =
                    now
            };

        _context
            .EntrepreneurEarnings
            .Add(earning);

        await _context.SaveChangesAsync(
            cancellationToken);

        return earning;
    }

    public async Task GenerateForOrderAsync(
    Guid orderId,
    CancellationToken cancellationToken = default)
    {
        var order =
            await _context.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == orderId,
                    cancellationToken);

        if (order == null)
            return;

        var financial =
            await _context.OrderFinancials
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.OrderId == orderId,
                    cancellationToken);

        if (financial == null)
            return;

        if (financial.AlphaGrossPlatformCommission <= 0)
            return;

        var payment =
            await _context.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.OrderId == orderId,
                    cancellationToken);

        if (payment == null)
            return;

        if (!string.Equals(
                payment.PaymentStatus,
                "paid",
                StringComparison.OrdinalIgnoreCase))
            return;

        var existingEarnings =
    await _context.EntrepreneurEarnings
        .AnyAsync(
            x => x.OrderId == orderId,
            cancellationToken);

        if (existingEarnings)
        {
            return;
        }


        // Supplier
        if (order.SupplierId.HasValue)
        {
            var supplier =
                await _context.Suppliers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Id == order.SupplierId.Value,
                        cancellationToken);

            if (supplier != null && supplier.UserId.HasValue)
            {
                await GenerateForProviderUserAsync(
                    supplier.UserId.Value,
                    order,
                    financial,
                    payment,
                    "supplier",
                    cancellationToken);
            }
        }

        // Driver
        if (order.DriverId.HasValue)
        {
            var driver =
                await _context.Drivers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Id == order.DriverId.Value,
                        cancellationToken);

            if (driver != null && driver.UserId.HasValue)
            {
                await GenerateForProviderUserAsync(
                    driver.UserId.Value,
                    order,
                    financial,
                    payment,
                    "driver",
                    cancellationToken);
            }
        }

       
    }
    public async Task GenerateForPaidSettlementAsync(
        SettlementQueue settlement,
        Order order,
        OrderFinancial financial,
        CancellationToken cancellationToken = default)
    {
        // ---------------------------------------------------------
        // 1. Only process supplier or driver settlements
        // ---------------------------------------------------------

        if (
            settlement.PayeeType != "supplier" &&
            settlement.PayeeType != "driver")
        {
            return;
        }

        if (!settlement.PayeeId.HasValue)
        {
            return;
        }

        if (settlement.Status != "paid")
        {
            return;
        }


        // ---------------------------------------------------------
        // 2. Determine the provider user
        // ---------------------------------------------------------

        Guid? recruitedUserId = null;
        string? providerRole = null;

        if (settlement.PayeeType == "supplier")
        {
            var supplier = await _context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == settlement.PayeeId.Value,
                    cancellationToken);

            if (supplier == null)
            {
                return;
            }

            recruitedUserId = supplier.UserId;
            providerRole = "supplier";
        }
        else if (settlement.PayeeType == "driver")
        {
            var driver = await _context.Drivers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == settlement.PayeeId.Value,
                    cancellationToken);

            if (driver == null)
            {
                return;
            }

            recruitedUserId = driver.UserId;
            providerRole = "driver";
        }


        // ---------------------------------------------------------
        // 3. Make sure the provider has a user account
        // ---------------------------------------------------------

        if (!recruitedUserId.HasValue)
        {
            return;
        }


        // ---------------------------------------------------------
        // 4. Find the direct Entrepreneur referral
        // ---------------------------------------------------------

        var referral = await _context.EntrepreneurReferrals
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.RecruitedUserId == recruitedUserId.Value &&
                    x.IsDirectReferral &&
                    x.EndedAt == null,
                cancellationToken);

        if (referral == null)
        {
            // Provider has no active Entrepreneur referral.
            return;
        }


        // ---------------------------------------------------------
        // 5. Determine the entrepreneur
        // ---------------------------------------------------------

        var entrepreneurUserId =
            referral.EntrepreneurUserId;

        if (entrepreneurUserId == Guid.Empty)
        {
            return;
        }


        // ---------------------------------------------------------
        // 6. Prevent duplicate earnings
        // ---------------------------------------------------------

        var alreadyExists =
            await _context.EntrepreneurEarnings
                .AnyAsync(
                    x =>
                        x.OrderId == order.Id &&
                        x.RecruitedProviderId ==
                            recruitedUserId.Value &&
                        x.EntrepreneurUserId ==
                            entrepreneurUserId,
                    cancellationToken);

        if (alreadyExists)
        {
            return;
        }


        // ---------------------------------------------------------
        // 7. Load Entrepreneur program configuration
        // ---------------------------------------------------------

        var configuration =
            await _context.EntrepreneurProgramConfigurations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (configuration == null)
        {
            return;
        }

        if (!configuration.ProgramEnabled)
        {
            return;
        }


        // ---------------------------------------------------------
        // 8. Make sure this provider role qualifies
        // ---------------------------------------------------------

        var qualifyingRoles =
            (configuration.QualifyingProviderRoles ?? "")
                .Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Select(x => x.ToLowerInvariant())
                .ToHashSet();

        if (!qualifyingRoles.Contains(
                providerRole.ToLowerInvariant()))
        {
            return;
        }


        // ---------------------------------------------------------
        // 9. Calculate the eligible revenue
        // ---------------------------------------------------------
        //
        // The Entrepreneur reward should be based on Alpha's
        // eligible platform revenue, NOT the supplier's gross
        // product value and NOT the driver's entire payout.
        //
        // For this implementation we use the platform revenue
        // associated with the settlement.
        //

        var eligibleRevenue =
            financial.AlphaNetRevenue;

        if (eligibleRevenue <= 0)
        {
            return;
        }


        // ---------------------------------------------------------
        // 10. Get Entrepreneur commission rate
        // ---------------------------------------------------------

        var commissionRate =
            configuration.DefaultCommissionRate;

        if (commissionRate <= 0)
        {
            return;
        }


        // ---------------------------------------------------------
        // 11. Calculate Entrepreneur earning
        // ---------------------------------------------------------

        var earningAmount =
            Math.Round(
                eligibleRevenue * commissionRate,
                2,
                MidpointRounding.AwayFromZero);

        if (earningAmount <= 0)
        {
            return;
        }


        // ---------------------------------------------------------
        // 12. Create Entrepreneur earning
        // ---------------------------------------------------------

        var earning = new EntrepreneurEarning
        {
            Id = Guid.NewGuid(),

            EntrepreneurUserId =
                entrepreneurUserId,

            RecruitedProviderId =
                recruitedUserId.Value,

            ProviderRole =
                providerRole,

            OrderId =
                order.Id,

            EligibleNetPlatformRevenue =
                eligibleRevenue,

            EntrepreneurPercentage =
                commissionRate,

            EntrepreneurEarningsAmount =
                earningAmount,

            EarningStatus =
                "pending",

            CreatedAt =
                DateTime.UtcNow
        };

        _context.EntrepreneurEarnings.Add(
            earning);

        await _context.SaveChangesAsync(
            cancellationToken);
    }
    private async Task GenerateForProviderUserAsync(
    Guid providerUserId,
    Order order,
    OrderFinancial financial,
    Payment payment,
    string providerRole,
    CancellationToken cancellationToken)
    {
        var referral =
            await _context
                .EntrepreneurReferrals
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.RecruitedUserId ==
                            providerUserId
                        &&
                        x.IsDirectReferral
                        &&
                        x.EndedAt == null,
                    cancellationToken);

        var existingProviderEarnings =
    await _context.EntrepreneurEarnings
        .AnyAsync(
            x =>
                x.OrderId == orderId &&
                x.RecruitedProviderId == providerUserId,
            cancellationToken);

        if (existingProviderEarnings)
        {
            return;
        }

        if (referral == null)
            return;

        await GenerateAsync(
            recruitedUserId:
                providerUserId,

            entrepreneurUserId:
                referral.EntrepreneurUserId,

            orderId:
                order.Id,

            paymentId:
                payment.Id,

            alphaGrossPlatformCommission:
                financial.AlphaGrossPlatformCommission,

            currency:
                financial.Currency,

            providerRole:
                providerRole,

            cancellationToken:
                cancellationToken);
    }

    public async Task GenerateForOrderAfterSettlementPaidAsync(
    Guid orderId,
    CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == orderId,
                cancellationToken);

        if (order == null)
            return;

        var financial = await _context.OrderFinancials
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.OrderId == orderId,
                cancellationToken);

        if (financial == null)
            return;

        if (!string.Equals(
                financial.SettlementStatus,
                "paid",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var settlements = await _context.SettlementQueue
            .AsNoTracking()
            .Where(x =>
                x.OrderFinancialId == financial.Id)
            .ToListAsync(cancellationToken);

        if (settlements.Any(x => x.Status != "paid"))
            return;

        var paidProviderSettlements =
            settlements
                .Where(x =>
                    x.PayeeType == "supplier" ||
                    x.PayeeType == "driver")
                .ToList();

        foreach (var settlement in paidProviderSettlements)
        {
            await GenerateForPaidSettlementAsync(
                settlement,
                order,
                financial,
                cancellationToken);
        }

        var entrepreneurEarnings =
            await _context.EntrepreneurEarnings
                .Where(x =>
                    x.OrderId == orderId &&
                    x.EarningStatus == "pending")
                .ToListAsync(cancellationToken);

        foreach (var earning in entrepreneurEarnings)
        {
            earning.EarningStatus = "AVAILABLE";
            earning.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(
            cancellationToken);
    }
}