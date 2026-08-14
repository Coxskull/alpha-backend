using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Alpha.API.Data;
using Alpha.API.DTOs;

using Microsoft.EntityFrameworkCore;

namespace Alpha.API.Services
{
    public class AutoPartsCommissionService
    {
        private readonly AppDbContext _context;

        public AutoPartsCommissionService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AutoPartsCommissionResultDtos> CalculateAsync(
            decimal subtotal,
            string currency,
            DateTime calculationDate,
            CancellationToken cancellationToken = default)
        {
            // ---------------------------------------------------------
            // 1. Validate subtotal
            // ---------------------------------------------------------

            if (subtotal <= 0)
            {
                return new AutoPartsCommissionResultDtos
                {
                    Currency = currency?.Trim().ToUpperInvariant() ?? "USD",
                    PartsSubtotal = subtotal,
                    TotalCommission = 0,
                    EffectiveCommissionRate = 0,
                    SupplierNet = subtotal,
                    Lines = new List<AutoPartsCommissionLineResultDtos>()
                };
            }

            // ---------------------------------------------------------
            // 2. Normalize currency
            // ---------------------------------------------------------

            currency = currency?.Trim().ToUpperInvariant() ?? "USD";

            // ---------------------------------------------------------
            // 3. Find active commission policy
            // ---------------------------------------------------------

            var policy = await _context
                .AutoPartsCommissionPolicies
                .Include(x => x.Tiers)
                .Where(x =>
                    x.Currency == currency &&
                    x.IsActive &&
                    x.EffectiveFrom <= calculationDate &&
                    (
                        x.EffectiveTo == null ||
                        x.EffectiveTo > calculationDate
                    ))
                .OrderByDescending(x => x.Version)
                .FirstOrDefaultAsync(cancellationToken);

            if (policy == null)
            {
                throw new InvalidOperationException(
                    $"No active auto-parts commission policy exists for currency '{currency}'.");
            }

            // ---------------------------------------------------------
            // 4. Get active tiers
            // ---------------------------------------------------------

            var tiers = policy.Tiers
                .Where(x => x.IsActive)
                .OrderBy(x => x.TierOrder)
                .ToList();

            if (!tiers.Any())
            {
                throw new InvalidOperationException(
                    $"Commission policy '{policy.PolicyName}' version {policy.Version} has no active tiers.");
            }

            // ---------------------------------------------------------
            // 5. Validate tier configuration
            // ---------------------------------------------------------

            ValidateTiers(tiers);

            // ---------------------------------------------------------
            // 6. Progressive calculation
            // ---------------------------------------------------------

            decimal remaining = subtotal;

            decimal totalCommission = 0m;

            var lines =
                new List<AutoPartsCommissionLineResultDtos>();

            foreach (var tier in tiers)
            {
                if (remaining <= 0)
                    break;

                // -----------------------------------------------------
                // Determine how much money belongs to this tier
                // -----------------------------------------------------

                decimal tierWidth;

                if (tier.MaximumAmount.HasValue)
                {
                    tierWidth =
                        tier.MaximumAmount.Value -
                        tier.MinimumAmount;
                }
                else
                {
                    // Unlimited final tier
                    tierWidth = decimal.MaxValue;
                }

                if (tierWidth <= 0)
                    continue;

                // -----------------------------------------------------
                // Calculate amount falling inside this tier
                // -----------------------------------------------------

                decimal amountInTier =
                    Math.Min(remaining, tierWidth);

                if (amountInTier <= 0)
                    continue;

                // -----------------------------------------------------
                // Calculate commission
                // -----------------------------------------------------

                decimal commission =
                    decimal.Round(
                        amountInTier *
                        (tier.CommissionPercentage / 100m),
                        2,
                        MidpointRounding.AwayFromZero);

                // -----------------------------------------------------
                // Save calculation line
                // -----------------------------------------------------

                lines.Add(
                    new AutoPartsCommissionLineResultDtos
                    {
                        TierId = tier.Id,

                        TierOrder = tier.TierOrder,

                        TierMinimum = tier.MinimumAmount,

                        TierMaximum = tier.MaximumAmount,

                        TierPercentage =
                            tier.CommissionPercentage,

                        AmountInTier =
                            amountInTier,

                        CommissionAmount =
                            commission
                    });

                // -----------------------------------------------------
                // Update totals
                // -----------------------------------------------------

                totalCommission += commission;

                remaining -= amountInTier;
            }

            // ---------------------------------------------------------
            // 7. Make sure the complete subtotal was processed
            // ---------------------------------------------------------

            if (remaining > 0)
            {
                throw new InvalidOperationException(
                    $"Commission tiers did not cover the full subtotal. " +
                    $"Unprocessed amount: {remaining:0.00} {currency}.");
            }

            // ---------------------------------------------------------
            // 8. Round total commission
            // ---------------------------------------------------------

            totalCommission =
                decimal.Round(
                    totalCommission,
                    2,
                    MidpointRounding.AwayFromZero);

            // ---------------------------------------------------------
            // 9. Calculate effective commission rate
            // ---------------------------------------------------------

            decimal effectiveRate =
                subtotal == 0
                    ? 0
                    : (totalCommission / subtotal) * 100m;

            effectiveRate =
                decimal.Round(
                    effectiveRate,
                    6,
                    MidpointRounding.AwayFromZero);

            // ---------------------------------------------------------
            // 10. Calculate supplier net
            // ---------------------------------------------------------

            decimal supplierNet =
                subtotal - totalCommission;

            supplierNet =
                decimal.Round(
                    supplierNet,
                    2,
                    MidpointRounding.AwayFromZero);

            // ---------------------------------------------------------
            // 11. Return DTO
            // ---------------------------------------------------------

            return new AutoPartsCommissionResultDtos
            {
                PolicyId = policy.Id,

                PolicyVersion = policy.Version,

                Currency = currency,

                PartsSubtotal = subtotal,

                TotalCommission = totalCommission,

                EffectiveCommissionRate = effectiveRate,

                SupplierNet = supplierNet,

                Lines = lines
            };
        }

        // =============================================================
        // Tier Validation
        // =============================================================

        private static void ValidateTiers(
            List<Models.AutoPartsCommissionTier> tiers)
        {
            if (tiers.Count == 0)
            {
                throw new InvalidOperationException(
                    "At least one commission tier is required.");
            }

            for (int i = 0; i < tiers.Count; i++)
            {
                var tier = tiers[i];

                // -----------------------------------------------------
                // Minimum validation
                // -----------------------------------------------------

                if (tier.MinimumAmount < 0)
                {
                    throw new InvalidOperationException(
                        $"Commission tier {tier.TierOrder} has a negative minimum amount.");
                }

                // -----------------------------------------------------
                // Maximum validation
                // -----------------------------------------------------

                if (
                    tier.MaximumAmount.HasValue &&
                    tier.MaximumAmount.Value <= tier.MinimumAmount
                )
                {
                    throw new InvalidOperationException(
                        $"Commission tier {tier.TierOrder} has an invalid maximum amount.");
                }

                // -----------------------------------------------------
                // Percentage validation
                // -----------------------------------------------------

                if (
                    tier.CommissionPercentage < 0 ||
                    tier.CommissionPercentage > 100
                )
                {
                    throw new InvalidOperationException(
                        $"Commission tier {tier.TierOrder} has an invalid commission percentage.");
                }

                // -----------------------------------------------------
                // Make sure there is only one unlimited tier
                // -----------------------------------------------------

                if (
                    tier.MaximumAmount == null &&
                    i != tiers.Count - 1
                )
                {
                    throw new InvalidOperationException(
                        $"Commission tier {tier.TierOrder} is unlimited " +
                        $"but is not the final tier.");
                }

                // -----------------------------------------------------
                // Check that tiers connect correctly
                // -----------------------------------------------------

                if (i > 0)
                {
                    var previousTier = tiers[i - 1];

                    if (
                        previousTier.MaximumAmount.HasValue &&
                        tier.MinimumAmount !=
                        previousTier.MaximumAmount.Value
                    )
                    {
                        throw new InvalidOperationException(
                            $"Commission tier {tier.TierOrder} does not " +
                            $"connect correctly to tier {previousTier.TierOrder}.");
                    }
                }
            }
        }
    }
}