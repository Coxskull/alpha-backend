using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
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
        string country = "MX",
        string? region = null,
        string currency = "MXN")
    {
        var existing = await _context.TaxCalculations
            .AnyAsync(x => x.OrderId == orderId);

        if (existing)
            throw new Exception("Tax already calculated for this order.");

        var financial = await _context.OrderFinancials
            .FirstOrDefaultAsync(x => x.OrderId == orderId);

        if (financial == null)
            throw new Exception("Financial record not found.");

        var components = new Dictionary<string, decimal>
        {
            { "product", financial.ItemSubtotal },
            { "delivery", financial.DeliveryFee },
            { "mechanic", financial.MechanicAmount },
            { "alpha_service_fee", financial.ServiceFee }
        };

        var calculations = new List<TaxCalculation>();

        foreach (var component in components.Where(x => x.Value > 0))
        {
            var rule = await _context.TaxRules
                .Where(x =>
                    x.Enabled &&
                    x.Country == country &&
                    x.Component == component.Key &&
                    x.EffectiveFrom <= DateTime.UtcNow &&
                    (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow) &&
                    (x.Region == null || x.Region == region))
                .OrderByDescending(x => x.Region != null)
                .ThenByDescending(x => x.Version)
                .FirstOrDefaultAsync();

            if (rule == null)
                throw new Exception($"No tax rule configured for {component.Key}.");

            var taxableBase = Math.Round(component.Value, 2);
            var taxAmount = rule.IsTaxInclusive
                ? Math.Round(taxableBase - (taxableBase / (1 + rule.TaxRate)), 2)
                : Math.Round(taxableBase * rule.TaxRate, 2);

            var withholdingAmount = rule.WithholdingRequired
                ? Math.Round(taxableBase * rule.WithholdingRate, 2)
                : 0;

            var calc = new TaxCalculation
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                Country = country,
                Region = region,
                Currency = currency,
                Component = component.Key,
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

            calculations.Add(calc);
            _context.TaxCalculations.Add(calc);

            _context.TaxLedgerEntries.Add(new TaxLedgerEntry
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                EntryType = "calculation",
                TaxType = calc.TaxType,
                Component = calc.Component,
                TaxRate = calc.TaxRate,
                TaxableBase = calc.TaxableBase,
                TaxCollected = calc.TaxAmount,
                TaxWithheld = calc.WithholdingAmount,
                ResponsibleParty = calc.TaxResponsibleParty,
                TaxRuleVersion = calc.TaxRuleVersion,
                Actor = "tax_engine",
                CreatedAt = DateTime.UtcNow
            });
        }

        financial.Tax = calculations.Sum(x => x.TaxAmount);
        financial.TaxCollected = calculations.Sum(x => x.TaxAmount);
        financial.TaxWithheld = calculations.Sum(x => x.WithholdingAmount);

        financial.TotalAmount =
            financial.ItemSubtotal +
            financial.DeliveryFee +
            financial.ServiceFee +
            financial.MechanicAmount +
            financial.Tax -
            financial.Discount;

        await _context.SaveChangesAsync();

        return calculations;
    }
}