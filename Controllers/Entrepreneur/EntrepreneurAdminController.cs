using Alpha.API.Data;
using Alpha.API.Models.Entrepreneur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alpha.API.Services.Entrepreneur;

namespace Alpha.API.Controllers.Entrepreneur;

[ApiController]
[Route("api/admin/entrepreneur")]
[Authorize(Roles = "admin")]
public class EntrepreneurAdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly EntrepreneurLedgerService _ledgerService;
    private readonly EntrepreneurPayoutService _payoutService;

    public EntrepreneurAdminController(
    AppDbContext context,
    EntrepreneurLedgerService ledgerService,
    EntrepreneurPayoutService payoutService)
    {
        _context = context;
        _ledgerService = ledgerService;
        _payoutService = payoutService;
    }

    public class PayEntrepreneurEarningRequest
    {
        public string PayoutReference { get; set; }
            = string.Empty;
    }

    [HttpPost("earnings/{earningId:guid}/pay")]
    public async Task<IActionResult> PayEarning(
    Guid earningId,
    [FromBody] PayEntrepreneurEarningRequest request,
    CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(
                request.PayoutReference))
        {
            return BadRequest(new
            {
                message =
                    "Payout reference is required."
            });
        }

        var claim =
            User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier);

        if (claim == null ||
            !Guid.TryParse(
                claim.Value,
                out var adminUserId))
        {
            return Unauthorized();
        }

        await _payoutService.MarkPaidAsync(
            earningId,
            request.PayoutReference.Trim(),
            adminUserId,
            cancellationToken);

        return Ok(new
        {
            success = true,
            earningId,
            status = "PAID",
            payoutReference =
                request.PayoutReference.Trim()
        });
    }

    // ============================================================
    // GET CONFIGURATION
    // ============================================================

    [HttpGet("configuration")]
    public async Task<IActionResult> GetConfiguration()
    {
        var config = await _context
            .EntrepreneurProgramConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return Ok(config);
    }

    // ============================================================
    // UPDATE CONFIGURATION
    // ============================================================

    [HttpPut("configuration")]
    public async Task<IActionResult> UpdateConfiguration(
        [FromBody] EntrepreneurProgramConfiguration request)
    {
        if (request == null)
        {
            return BadRequest(
                "Configuration request is required.");
        }

        // Rate is stored as decimal:
        // 5% = 0.05
        if (request.DefaultCommissionRate < 0 ||
            request.DefaultCommissionRate > 1)
        {
            return BadRequest(
                "Commission rate must be between 0 and 1. " +
                "For 5%, use 0.05.");
        }

        var config = await _context
            .EntrepreneurProgramConfigurations
            .FirstOrDefaultAsync();

        if (config == null)
        {
            // C# property is id, not id.
            request.id = Guid.NewGuid();

            // Entrepreneur Network is permanently one level.
            request.MaximumReferralLevel = 1;

            request.UpdatedAt = DateTime.UtcNow;

            _context
                .EntrepreneurProgramConfigurations
                .Add(request);
        }
        else
        {
            config.ProgramEnabled =
                request.ProgramEnabled;

            config.DefaultCommissionRate =
                request.DefaultCommissionRate;

            config.MinimumPayoutThreshold =
                request.MinimumPayoutThreshold;

            config.PayoutFrequency =
                request.PayoutFrequency;

            config.QualifyingProviderRoles =
                request.QualifyingProviderRoles;

            config.QualifyingTransactionTypes =
                request.QualifyingTransactionTypes;

            config.HoldingPeriodDays =
                request.HoldingPeriodDays;

            // Entrepreneur Network is permanently one level.
            config.MaximumReferralLevel = 1;

            config.ProgramStartDate =
                request.ProgramStartDate;

            config.ProgramEndDate =
                request.ProgramEndDate;

            config.UpdatedAt =
                DateTime.UtcNow;

            config.UpdatedByUserId =
                request.UpdatedByUserId;
        }

        await _context.SaveChangesAsync();

        return Ok(config);
    }

    // ============================================================
    // GET REFERRALS
    // ============================================================

    [HttpGet("referrals")]
    public async Task<IActionResult> GetReferrals()
    {
        var referrals = await _context
            .EntrepreneurReferrals
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                // C# property is id.
                // EF mapping should map this to PostgreSQL "id".
                x.id,

                x.EntrepreneurUserId,
                x.RecruitedUserId,
                x.ReferralCode,
                x.ReferralDate,
                x.ProviderActivationDate,
                x.ReferralStatus,
                x.IsDirectReferral,
                x.EndedAt,
                x.CreatedAt
            })
            .ToListAsync();

        return Ok(referrals);
    }

    // ============================================================
    // GET EARNINGS
    // ============================================================

    [HttpGet("earnings")]
    public async Task<IActionResult> GetEarnings()
    {
        var earnings = await _context
            .EntrepreneurEarnings
            .AsNoTracking()
            .OrderByDescending(x => x.TransactionDate)
            .Select(x => new
            {
                // C# property is id.
                x.id,

                x.EntrepreneurUserId,
                x.RecruiterId,
                x.RecruitedProviderId,
                x.ProviderRole,
                x.OrderId,
                x.TransactionId,
                x.PaymentId,
                x.TransactionDate,

                x.AlphaGrossPlatformCommission,
                x.DirectTransactionCosts,
                x.EligibleNetPlatformRevenue,

                x.EntrepreneurPercentage,
                x.EntrepreneurEarningsAmount,

                x.Currency,
                x.EarningStatus,

                x.RefundAdjustment,
                x.ChargebackAdjustment,

                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync();

        return Ok(earnings);
    }

    // ============================================================
    // GET SUMMARY
    // ============================================================

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var referrals = await _context
            .EntrepreneurReferrals
            .AsNoTracking()
            .ToListAsync();

        var earnings = await _context
            .EntrepreneurEarnings
            .AsNoTracking()
            .ToListAsync();

        var summary = new
        {
            TotalReferrals =
                referrals.Count,

            DirectReferrals =
                referrals.Count(x =>
                    x.IsDirectReferral),

            ActiveReferrals =
                referrals.Count(x =>
                    x.EndedAt == null),

            TotalEarnings =
                earnings.Sum(x =>
                    x.EntrepreneurEarningsAmount),

            PendingEarnings =
                earnings
                    .Where(x =>
                        x.EarningStatus == "PENDING")
                    .Sum(x =>
                        x.EntrepreneurEarningsAmount),

            ApprovedEarnings =
                earnings
                    .Where(x =>
                        x.EarningStatus == "APPROVED")
                    .Sum(x =>
                        x.EntrepreneurEarningsAmount),

            PaidEarnings =
                earnings
                    .Where(x =>
                        x.EarningStatus == "PAID")
                    .Sum(x =>
                        x.EntrepreneurEarningsAmount),

            AdjustedEarnings =
                earnings
                    .Where(x =>
                        x.EarningStatus == "ADJUSTED")
                    .Sum(x =>
                        x.EntrepreneurEarningsAmount)
        };

        return Ok(summary);
    }

    [HttpPost("approve-eligible")]
    public async Task<IActionResult>
    ApproveEligible(
        CancellationToken cancellationToken)
    {
        await _ledgerService
            .ApproveEligibleEarningsAsync(
                cancellationToken);

        return Ok(new
        {
            success = true,
            message =
                "Eligible Entrepreneur earnings approved."
        });
    }
}