using Alpha.API.Data;
using Alpha.API.Models.Entrepreneur;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services.Entrepreneur;

public class EntrepreneurPayoutService
{
    private readonly AppDbContext _context;

    public EntrepreneurPayoutService(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task MarkPaidAsync(
        Guid earningId,
        string payoutReference,
        Guid processedByUserId,
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

        if (!string.Equals(
                earning.EarningStatus,
                "APPROVED",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only approved Entrepreneur earnings can be paid.");
        }

        var config =
            await _context
                .EntrepreneurProgramConfigurations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    cancellationToken);

        var minimum =
            config?.MinimumPayoutThreshold ?? 0m;

        if (earning.EntrepreneurEarningsAmount <
            minimum)
        {
            throw new InvalidOperationException(
                $"Earning is below the minimum payout threshold of {minimum}.");
        }

        var batch =
            new EntrepreneurPayoutBatch
            {
                Id = Guid.NewGuid(),

                SettlementDate =
                    DateTime.UtcNow,

                Currency =
                    earning.Currency,

                TotalAmount =
                    earning.EntrepreneurEarningsAmount,

                EarningCount =
                    1,

                Status =
                    "PAID",

                PayoutReference =
                    payoutReference,

                CreatedAt =
                    DateTime.UtcNow,

                PaidAt =
                    DateTime.UtcNow,

                ProcessedByUserId =
                    processedByUserId
            };

        _context
            .EntrepreneurPayoutBatches
            .Add(batch);

        earning.PayoutBatchId =
            batch.Id;

        earning.PayoutDate =
            DateTime.UtcNow;

        earning.PayoutReference =
            payoutReference;

        earning.EarningStatus =
            "PAID";

        earning.UpdatedAt =
            DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);
    }
}