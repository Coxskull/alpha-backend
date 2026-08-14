using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Alpha.API.Data;
using Alpha.API.DTOs;
using Alpha.API.DTOs.AutoPartsCommission.Admin;
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

        // =============================================================
        // CALCULATE AUTO PARTS COMMISSION
        // =============================================================

        public async Task<AutoPartsCommissionResultDtos> CalculateAsync(
            decimal subtotal,
            string currency,
            DateTime calculationDate,
            CancellationToken cancellationToken = default)
        {
            if (subtotal <= 0)
            {
                return new AutoPartsCommissionResultDtos
                {
                    Currency =
                        currency?.Trim().ToUpperInvariant() ?? "USD",

                    PartsSubtotal = subtotal,

                    TotalCommission = 0,

                    EffectiveCommissionRate = 0,

                    SupplierNet = subtotal,

                    Lines =
                        new List<AutoPartsCommissionLineResultDtos>()
                };
            }

            currency =
                currency?.Trim().ToUpperInvariant() ?? "USD";

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

            var tiers = policy.Tiers
                .Where(x => x.IsActive)
                .OrderBy(x => x.TierOrder)
                .ToList();

            if (!tiers.Any())
            {
                throw new InvalidOperationException(
                    $"Commission policy '{policy.PolicyName}' version {policy.Version} has no active tiers.");
            }

            ValidateTiers(tiers);

            decimal remaining = subtotal;

            decimal totalCommission = 0m;

            var lines =
                new List<AutoPartsCommissionLineResultDtos>();

            foreach (var tier in tiers)
            {
                if (remaining <= 0)
                    break;

                decimal tierWidth;

                if (tier.MaximumAmount.HasValue)
                {
                    tierWidth =
                        tier.MaximumAmount.Value -
                        tier.MinimumAmount;
                }
                else
                {
                    tierWidth = decimal.MaxValue;
                }

                if (tierWidth <= 0)
                    continue;

                decimal amountInTier =
                    Math.Min(remaining, tierWidth);

                if (amountInTier <= 0)
                    continue;

                decimal commission =
                    decimal.Round(
                        amountInTier *
                        (tier.CommissionPercentage / 100m),
                        2,
                        MidpointRounding.AwayFromZero);

                lines.Add(
                    new AutoPartsCommissionLineResultDtos
                    {
                        TierId = tier.Id,

                        TierOrder = tier.TierOrder,

                        TierMinimum =
                            tier.MinimumAmount,

                        TierMaximum =
                            tier.MaximumAmount,

                        TierPercentage =
                            tier.CommissionPercentage,

                        AmountInTier =
                            amountInTier,

                        CommissionAmount =
                            commission
                    });

                totalCommission += commission;

                remaining -= amountInTier;
            }

            if (remaining > 0)
            {
                throw new InvalidOperationException(
                    $"Commission tiers did not cover the full subtotal. " +
                    $"Unprocessed amount: {remaining:0.00} {currency}.");
            }

            totalCommission =
                decimal.Round(
                    totalCommission,
                    2,
                    MidpointRounding.AwayFromZero);

            decimal effectiveRate =
                subtotal == 0
                    ? 0
                    : (totalCommission / subtotal) * 100m;

            effectiveRate =
                decimal.Round(
                    effectiveRate,
                    6,
                    MidpointRounding.AwayFromZero);

            decimal supplierNet =
                subtotal - totalCommission;

            supplierNet =
                decimal.Round(
                    supplierNet,
                    2,
                    MidpointRounding.AwayFromZero);

            return new AutoPartsCommissionResultDtos
            {
                PolicyId = policy.Id,

                PolicyVersion = policy.Version,

                Currency = currency,

                PartsSubtotal = subtotal,

                TotalCommission =
                    totalCommission,

                EffectiveCommissionRate =
                    effectiveRate,

                SupplierNet =
                    supplierNet,

                Lines = lines
            };
        }

        // =============================================================
        // VALIDATE TIERS
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

                if (tier.MinimumAmount < 0)
                {
                    throw new InvalidOperationException(
                        $"Commission tier {tier.TierOrder} has a negative minimum amount.");
                }

                if (
                    tier.MaximumAmount.HasValue &&
                    tier.MaximumAmount.Value <=
                    tier.MinimumAmount)
                {
                    throw new InvalidOperationException(
                        $"Commission tier {tier.TierOrder} has an invalid maximum amount.");
                }

                if (
                    tier.CommissionPercentage < 0 ||
                    tier.CommissionPercentage > 100)
                {
                    throw new InvalidOperationException(
                        $"Commission tier {tier.TierOrder} has an invalid commission percentage.");
                }

                if (
                    tier.MaximumAmount == null &&
                    i != tiers.Count - 1)
                {
                    throw new InvalidOperationException(
                        $"Commission tier {tier.TierOrder} is unlimited " +
                        $"but is not the final tier.");
                }

                if (i > 0)
                {
                    var previousTier = tiers[i - 1];

                    if (
                        previousTier.MaximumAmount.HasValue &&
                        tier.MinimumAmount !=
                        previousTier.MaximumAmount.Value)
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