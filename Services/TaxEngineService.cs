using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
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
        string currency)
    {
        var normalizedCountry = string.IsNullOrWhiteSpace(country)
            ? "MX"
            : country.Trim().ToUpper();

        var normalizedRegion = string.IsNullOrWhiteSpace(region)
            ? null
            : region.Trim();

        var normalizedCurrency = string.IsNullOrWhiteSpace(currency)
            ? "MXN"
            : currency.Trim().ToUpper();

        var existing = await _context.TaxCalculations
            .Where(x => x.OrderId == orderId)
            .ToListAsync();

        if (existing.Any())
            return existing;

        var financial = await _context.OrderFinancials
            .FirstOrDefaultAsync(x => x.OrderId == orderId);

        if (financial == null)
            throw new Exception("Financial record not found.");

        var components = new Dictionary<string, decimal>
        {
            { "product", financial.ItemSubtotal },
            { "delivery", financial.DeliveryFee },
            { "alpha_service_fee", financial.ServiceFee },
            { "mechanic", financial.MechanicAmount }
        };

        var results = new List<TaxCalculation>();

        foreach (var component in components.Where(x => x.Value > 0))
        {
            var normalizedComponent = component.Key.Trim().ToLower();

            var rule = await GetOrCreateTaxRule(
                normalizedCountry,
                normalizedRegion,
                normalizedComponent
            );

            var taxableBase = Math.Round(component.Value, 2);

            var taxAmount = rule.IsTaxInclusive
                ? Math.Round(taxableBase - taxableBase / (1 + rule.TaxRate), 2)
                : Math.Round(taxableBase * rule.TaxRate, 2);

            var withholdingAmount = rule.WithholdingRequired
                ? Math.Round(taxableBase * rule.WithholdingRate, 2)
                : 0;

            var calculation = new TaxCalculation
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                Country = normalizedCountry,
                Region = normalizedRegion,
                Currency = normalizedCurrency,
                Component = normalizedComponent,
                TaxType = rule.TaxType,
                TaxRate = rule.TaxRate,
                TaxableBase = taxableBase,
                TaxAmount = taxAmount,
                RevenueRecipient = rule.ResponsibleParty,
                TaxResponsibleParty = rule.ResponsibleParty,
                WithholdingRequired = rule.WithholdingRequired,
                WithholdingAmount = withholdingAmount,
                TaxRuleId = rule.Id,
                TaxRuleVersion = rule.Version,
                CreatedAt = DateTime.UtcNow
            };

            _context.TaxCalculations.Add(calculation);
            results.Add(calculation);
        }

        financial.Tax = results.Sum(x => x.TaxAmount);
        financial.TaxCollected = financial.Tax;
        financial.TaxWithheld = results.Sum(x => x.WithholdingAmount);

        financial.TotalAmount =
            financial.ItemSubtotal +
            financial.DeliveryFee +
            financial.ServiceFee +
            financial.MechanicAmount +
            financial.Tax -
            financial.Discount;

        await _context.SaveChangesAsync();

        return results;
    }

    private async Task<TaxRule> GetOrCreateTaxRule(
        string country,
        string? region,
        string component)
    {
        var rule = await _context.TaxRules
            .Where(x =>
                x.Enabled &&
                x.Country.ToUpper() == country &&
                x.Component.ToLower() == component &&
                x.EffectiveFrom <= DateTime.UtcNow &&
                (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow) &&
                (x.Region == null || x.Region == region))
            .OrderByDescending(x => x.Region != null)
            .ThenByDescending(x => x.Version)
            .FirstOrDefaultAsync();

        if (rule != null)
            return rule;

        var fallbackRule = new TaxRule
        {
            Id = Guid.NewGuid(),
            Country = country,
            Region = null,
            TaxType = country == "MX" ? "IVA" : "TAX",
            TaxRate = country == "MX" ? 0.16m : 0m,
            Component = component,
            ResponsibleParty = component switch
            {
                "product" => "supplier",
                "delivery" => "driver",
                "mechanic" => "mechanic",
                "alpha_service_fee" => "alpha",
                _ => "alpha"
            },
            IsTaxInclusive = false,
            WithholdingRequired = false,
            WithholdingRate = 0,
            EffectiveFrom = DateTime.UtcNow,
            ExpiresAt = null,
            Enabled = true,
            Version = 1
        };

        _context.TaxRules.Add(fallbackRule);
        await _context.SaveChangesAsync();

        return fallbackRule;
    }
}