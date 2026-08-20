using Alpha.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Controllers.Entrepreneur;

[ApiController]
[Route("api/entrepreneur/payouts")]
[Authorize]
public class EntrepreneurPayoutController
    : ControllerBase
{
    private readonly AppDbContext _context;

    public EntrepreneurPayoutController(
        AppDbContext context)
    {
        _context = context;
    }

    private Guid? CurrentUserId()
    {
        var claim =
            User.FindFirst(
                ClaimTypes.NameIdentifier);

        return claim != null &&
               Guid.TryParse(
                   claim.Value,
                   out var id)
            ? id
            : null;
    }

    [HttpGet]
    public async Task<IActionResult> GetPayouts(
        CancellationToken cancellationToken)
    {
        var userId =
            CurrentUserId();

        if (!userId.HasValue)
            return Unauthorized();

        var earnings =
            await _context
                .EntrepreneurEarnings
                .AsNoTracking()
                .Where(
                    x =>
                        x.EntrepreneurUserId ==
                        userId.Value)
                .OrderByDescending(
                    x => x.TransactionDate)
                .Select(x => new
                {
                    id = x.Id,

                    entrepreneurId =
        x.EntrepreneurUserId,

                    periodStart =
        x.TransactionDate,

                    periodEnd =
        x.TransactionDate,

                    eligibleRevenue =
        x.EligibleNetPlatformRevenue,

                    rewardRate =
        x.EntrepreneurPercentage,

                    rewardAmount =
        x.EntrepreneurEarningsAmount,

                    status =
        x.EarningStatus,

                    approvedAt =
        x.EarningStatus == "APPROVED"
            ? (DateTime?)x.UpdatedAt
            : null,

                    paidAt =
        x.PayoutDate,

                    createdAt =
        x.CreatedAt,

                    payoutReference =
        x.PayoutReference,

                    currency =
        x.Currency
                })
                .ToListAsync(
                    cancellationToken);

        return Ok(earnings);
    }

    [HttpGet("summary")]
    public async Task<IActionResult>
        GetSummary(
            CancellationToken cancellationToken)
    {
        var userId =
            CurrentUserId();

        if (!userId.HasValue)
            return Unauthorized();

        var earnings =
            await _context
                .EntrepreneurEarnings
                .AsNoTracking()
                .Where(
                    x =>
                        x.EntrepreneurUserId ==
                        userId.Value)
                .ToListAsync(
                    cancellationToken);

        var config =
            await _context
                .EntrepreneurProgramConfigurations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    cancellationToken);

        var pending =
            earnings
                .Where(x =>
                    x.EarningStatus ==
                    "PENDING")
                .Sum(x =>
                    x.EntrepreneurEarningsAmount);

        var approved =
            earnings
                .Where(x =>
                    x.EarningStatus ==
                    "APPROVED")
                .Sum(x =>
                    x.EntrepreneurEarningsAmount);

        var paid =
            earnings
                .Where(x =>
                    x.EarningStatus ==
                    "PAID")
                .Sum(x =>
                    x.EntrepreneurEarningsAmount);

        var total =
            earnings.Sum(
                x =>
                    x.EntrepreneurEarningsAmount);

        return Ok(new
        {
            pendingAmount = pending,

            approvedAmount = approved,

            paidAmount = paid,

            totalEarned = total,

            minimumPayout =
                config?.MinimumPayoutThreshold
                ?? 0m,

            rewardRate =
                config?.DefaultCommissionRate
                ?? 0.05m,

            currency =
                earnings
                    .Select(x => x.Currency)
                    .FirstOrDefault()
                ?? "USD"
        });
    }
}