using Alpha.API.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services.Entrepreneur;

public class DirectTransactionCostService
{
    private readonly AppDbContext _context;

    public DirectTransactionCostService(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> CalculateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var total =
            await _context
                .EntrepreneurTransactionCosts
                .Where(x => x.OrderId == orderId)
                .SumAsync(
                    x => (decimal?)x.Amount,
                    cancellationToken)
                ?? 0m;

        return Math.Max(0m, total);
    }

    public async Task AddAsync(
        Guid orderId,
        Guid? paymentId,
        string costType,
        decimal amount,
        string currency,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0m)
            return;

        _context
            .EntrepreneurTransactionCosts
            .Add(
                new Models.Entrepreneur.EntrepreneurTransactionCost
                {
                    Id = Guid.NewGuid(),

                    OrderId =
                        orderId,

                    PaymentId =
                        paymentId,

                    CostType =
                        costType,

                    Amount =
                        decimal.Round(
                            amount,
                            2,
                            MidpointRounding.AwayFromZero),

                    Currency =
                        currency.ToUpperInvariant(),

                    Description =
                        description,

                    CreatedAt =
                        DateTime.UtcNow
                });

        await _context.SaveChangesAsync(
            cancellationToken);
    }
}