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
        string country,
        string? region,
        string currency)
    {
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
                throw new Exception($"No tax rule found for {component.Key}.");

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

            _context.TaxCalculations.Add(calculation);
            results.Add(calculation);
        }

        financial.Tax = results.Sum(x => x.TaxAmount);
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
}