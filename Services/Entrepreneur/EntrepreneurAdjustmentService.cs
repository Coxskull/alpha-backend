using Alpha.API.Data;
using Alpha.API.Models.Entrepreneur;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services.Entrepreneur;

public class EntrepreneurAdjustmentService
{
    private readonly AppDbContext _context;

    public EntrepreneurAdjustmentService(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task ApplyRefundAsync(
        Guid earningId,
        decimal refundImpact,
        string reason,
        Guid? relatedPaymentId = null,
        CancellationToken cancellationToken = default)
    {
        var earning =
            await _context
                .EntrepreneurEarnings
                .FirstOrDefaultAsync(
                    x => x.id == earningId,
                    cancellationToken);

        if (earning == null)
        {
            throw new InvalidOperationException(
                "Entrepreneur earning not found.");
        }

        if (refundImpact <= 0)
        {
            throw new ArgumentException(
                "Refund impact must be greater than zero.");
        }

        var adjustment =
            decimal.Round(
                -Math.Abs(refundImpact),
                2,
                MidpointRounding.AwayFromZero);

        _context
            .EntrepreneurEarningAdjustments
            .Add(
                new EntrepreneurEarningAdjustment
                {
                    Id = Guid.NewGuid(),

                    EntrepreneurEarningId =
                        earning.Id,

                    AdjustmentType =
                        "REFUND",

                    Amount =
                        adjustment,

                    Currency =
                        earning.Currency,

                    Reason =
                        reason,

                    RelatedPaymentId =
                        relatedPaymentId,

                    CreatedAt =
                        DateTime.UtcNow
                });

        earning.RefundAdjustment +=
            adjustment;

        if (earning.EarningStatus == "PAID")
        {
            earning.EarningStatus =
                "ADJUSTED";
        }
        else
        {
            earning.EarningStatus =
                "ADJUSTED";
        }

        earning.UpdatedAt =
            DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);
    }
}