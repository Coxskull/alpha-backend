using Alpha.API.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services.Entrepreneur;

public class EntrepreneurLedgerService
{
    private readonly AppDbContext _context;

    public EntrepreneurLedgerService(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task ApproveEligibleEarningsAsync(
        CancellationToken cancellationToken = default)
    {
        var config =
            await _context
                .EntrepreneurProgramConfigurations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (config == null ||
            !config.ProgramEnabled)
        {
            return;
        }

        var cutoff =
            DateTime.UtcNow.AddDays(
                -config.HoldingPeriodDays);

        var earnings =
            await _context
                .EntrepreneurEarnings
                .Where(
                    x =>
                        x.EarningStatus ==
                            "PENDING"
                        &&
                        x.TransactionDate <=
                            cutoff)
                .ToListAsync(
                    cancellationToken);

        foreach (var earning in earnings)
        {
            earning.EarningStatus =
                "APPROVED";

            earning.UpdatedAt =
                DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(
            cancellationToken);
    }
}