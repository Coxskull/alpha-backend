using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services;

public class TaxEngineService
{
    private readonly AppDbContext _context;

    public TaxEngineService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<TaxCalculation>> CalculateOrderTaxes(
        Guid orderId,
        string country,
        string? region,
        string currency,
        CancellationToken cancellationToken = default)
    {
        var normalizedCountry = NormalizeCountry(country);
        var normalizedRegion = NormalizeRegion(region);
        var normalizedCurrency = NormalizeCurrency(
            currency,
            normalizedCountry
        );

        var existingCalculations =
            await _context.TaxCalculations
                .Where(x => x.OrderId == orderId)
                .ToListAsync(cancellationToken);

        /*
         * Return existing calculations when taxes were already
         * calculated successfully.
         */
        if (
            existingCalculations.Count > 0 &&
            existingCalculations.Any(x => x.TaxAmount > 0)
        )
        {
            return existingCalculations;
        }

        /*
         * Remove incomplete or zero-value calculations before
         * generating a fresh tax breakdown.
         */
        if (existingCalculations.Count > 0)
        {
            _context.TaxCalculations.RemoveRange(
                existingCalculations
            );
        }

        var financial =
            await _context.OrderFinancials
                .FirstOrDefaultAsync(
                    x => x.OrderId == orderId,
                    cancellationToken
                );

        if (financial == null)
        {
            throw new InvalidOperationException(
                $"Financial record for order {orderId} was not found."
            );
        }

        var components =
            new Dictionary<string, decimal>(
                StringComparer.OrdinalIgnoreCase
            )
            {
                ["product"] =
                    financial.ItemSubtotal,

                ["delivery"] =
                    financial.DeliveryFee,

                ["alpha_service_fee"] =
                    financial.ServiceFee,

                ["mechanic"] =
                    financial.MechanicAmount
            };

        var results =
            new List<TaxCalculation>();

        decimal exclusiveTaxTotal = 0m;
        decimal inclusiveTaxTotal = 0m;

        foreach (
            var component in components
                .Where(x => x.Value > 0m)
        )
        {
            var normalizedComponent =
                component.Key
                    .Trim()
                    .ToLowerInvariant();

            var rule = await GetOrCreateTaxRule(
                normalizedCountry,
                normalizedRegion,
                normalizedComponent,
                cancellationToken
            );

            var taxableBase = Math.Round(
                component.Value,
                2,
                MidpointRounding.AwayFromZero
            );

            var taxAmount = CalculateTaxAmount(
                taxableBase,
                rule.TaxRate,
                rule.IsTaxInclusive
            );

            var withholdingAmount =
                rule.WithholdingRequired
                    ? Math.Round(
                        taxableBase *
                        rule.WithholdingRate,
                        2,
                        MidpointRounding.AwayFromZero
                    )
                    : 0m;

            var calculation =
                new TaxCalculation
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,

                    Country =
                        normalizedCountry,

                    Region =
                        normalizedRegion,

                    Currency =
                        normalizedCurrency,

                    Component =
                        normalizedComponent,

                    TaxType =
                        rule.TaxType,

                    TaxRate =
                        rule.TaxRate,

                    TaxableBase =
                        taxableBase,

                    TaxAmount =
                        taxAmount,

                    RevenueRecipient =
                        rule.ResponsibleParty,

                    TaxResponsibleParty =
                        rule.ResponsibleParty,

                    WithholdingRequired =
                        rule.WithholdingRequired,

                    WithholdingAmount =
                        withholdingAmount,

                    TaxRuleId =
                        rule.Id,

                    TaxRuleVersion =
                        rule.Version,

                    CreatedAt =
                        DateTime.UtcNow
                };

            _context.TaxCalculations.Add(
                calculation
            );

            results.Add(
                calculation
            );

            /*
             * These totals must be updated inside the loop because
             * rule and taxAmount only exist within this scope.
             */
            if (rule.IsTaxInclusive)
            {
                inclusiveTaxTotal +=
                    taxAmount;
            }
            else
            {
                exclusiveTaxTotal +=
                    taxAmount;
            }
        }

        /*
         * Tax includes both inclusive and exclusive taxes for
         * reporting purposes.
         */
        financial.Tax = Math.Round(
            exclusiveTaxTotal +
            inclusiveTaxTotal,
            2,
            MidpointRounding.AwayFromZero
        );

        financial.TaxCollected =
            financial.Tax;

        /*
         * Inclusive tax is already contained within the component
         * price, so only exclusive tax is added to TotalAmount.
         */
        financial.TotalAmount = Math.Round(
            financial.ItemSubtotal +
            financial.DeliveryFee +
            financial.ServiceFee +
            financial.MechanicAmount +
            exclusiveTaxTotal -
            financial.Discount,
            2,
            MidpointRounding.AwayFromZero
        );

        await _context.SaveChangesAsync(
            cancellationToken
        );

        return results;
    }

    private async Task<TaxRule> GetOrCreateTaxRule(
        string country,
        string? region,
        string component,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var rule =
            await _context.TaxRules
                .Where(x =>
                    x.Enabled &&
                    x.Country.ToUpper() ==
                        country &&
                    x.Component.ToLower() ==
                        component &&
                    x.EffectiveFrom <= now &&
                    (
                        x.ExpiresAt == null ||
                        x.ExpiresAt > now
                    ) &&
                    (
                        x.Region == null ||
                        x.Region == region
                    )
                )
                .OrderByDescending(
                    x => x.Region != null
                )
                .ThenByDescending(
                    x => x.Version
                )
                .FirstOrDefaultAsync(
                    cancellationToken
                );

        if (rule != null)
        {
            return rule;
        }

        var defaultRate =
            country switch
            {
                "MX" => 0.16m,
                "PH" => 0.12m,
                _ => 0m
            };

        var defaultTaxType =
            country switch
            {
                "MX" => "IVA",
                "PH" => "VAT",
                _ => "TAX"
            };

        var fallbackRule =
            new TaxRule
            {
                Id = Guid.NewGuid(),

                Country =
                    country,

                Region =
                    null,

                TaxType =
                    defaultTaxType,

                TaxRate =
                    defaultRate,

                Component =
                    component,

                ResponsibleParty =
                    GetResponsibleParty(
                        component
                    ),

                IsTaxInclusive =
                    false,

                WithholdingRequired =
                    false,

                WithholdingRate =
                    0m,

                EffectiveFrom =
                    now,

                ExpiresAt =
                    null,

                Enabled =
                    true,

                Version =
                    1
            };

        _context.TaxRules.Add(
            fallbackRule
        );

        /*
         * Save immediately because TaxCalculation references
         * fallbackRule.Id as a foreign key.
         */
        await _context.SaveChangesAsync(
            cancellationToken
        );

        return fallbackRule;
    }

    private static decimal CalculateTaxAmount(
        decimal taxableBase,
        decimal taxRate,
        bool isTaxInclusive)
    {
        if (
            taxableBase <= 0m ||
            taxRate <= 0m
        )
        {
            return 0m;
        }

        decimal taxAmount;

        if (isTaxInclusive)
        {
            taxAmount =
                taxableBase -
                (
                    taxableBase /
                    (1m + taxRate)
                );
        }
        else
        {
            taxAmount =
                taxableBase *
                taxRate;
        }

        return Math.Round(
            taxAmount,
            2,
            MidpointRounding.AwayFromZero
        );
    }

    private static string GetResponsibleParty(
        string component)
    {
        return component switch
        {
            "product" =>
                "supplier",

            "delivery" =>
                "driver",

            "mechanic" =>
                "mechanic",

            "alpha_service_fee" =>
                "alpha",

            _ =>
                "alpha"
        };
    }

    private static string NormalizeCountry(
        string country)
    {
        return string.IsNullOrWhiteSpace(
            country
        )
            ? "MX"
            : country
                .Trim()
                .ToUpperInvariant();
    }

    private static string? NormalizeRegion(
        string? region)
    {
        return string.IsNullOrWhiteSpace(
            region
        )
            ? null
            : region
                .Trim()
                .ToUpperInvariant();
    }

    private static string NormalizeCurrency(
        string currency,
        string country)
    {
        if (!string.IsNullOrWhiteSpace(currency))
        {
            return currency
                .Trim()
                .ToUpperInvariant();
        }

        return country switch
        {
            "PH" => "PHP",
            "MX" => "MXN",
            "US" => "USD",
            _ => "USD"
        };
    }
}