using System;
using System.Linq;
using System.Threading.Tasks;
using Alpha.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Alpha.API.Controllers.Entrepreneur;

[ApiController]
[Route("api/entrepreneur")]
[Authorize]
public class EntrepreneurController : ControllerBase
{
    private readonly AppDbContext _context;

    public EntrepreneurController(
        AppDbContext context)
    {
        _context = context;
    }

    private Guid? GetCurrentUserId()
    {
        var claim =
            User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier);

        if (claim == null)
            return null;

        return Guid.TryParse(
            claim.Value,
            out var userId)
            ? userId
            : null;
    }

    // =====================================================
    // PROGRAM
    // =====================================================

    [HttpGet("program")]
    public async Task<IActionResult> GetProgram()
    {
        var config =
            await _context
                .EntrepreneurProgramConfigurations
                .AsNoTracking()
                .FirstOrDefaultAsync();

        if (config == null)
        {
            return NotFound(
                "Entrepreneur Network configuration not found.");
        }

        return Ok(new
        {
            programEnabled =
                config.ProgramEnabled,

            defaultCommissionRate =
                config.DefaultCommissionRate,

            minimumPayoutThreshold =
                config.MinimumPayoutThreshold,

            payoutFrequency =
                config.PayoutFrequency,

            qualifyingProviderRoles =
                config.QualifyingProviderRoles,

            qualifyingTransactionTypes =
                config.QualifyingTransactionTypes,

            holdingPeriodDays =
                config.HoldingPeriodDays,

            maximumReferralLevel =
                config.MaximumReferralLevel,

            programStartDate =
                config.ProgramStartDate,

            programEndDate =
                config.ProgramEndDate
        });
    }

    // =====================================================
    // REFERRALS
    // =====================================================

    [HttpGet("referrals")]
    public async Task<IActionResult> GetReferrals()
    {
        var entrepreneurUserId =
            GetCurrentUserId();

        if (!entrepreneurUserId.HasValue)
        {
            return Unauthorized();
        }

        var referrals =
            await _context
                .EntrepreneurReferrals
                .AsNoTracking()
                .Where(x =>
                    x.EntrepreneurUserId ==
                    entrepreneurUserId.Value)
                .OrderByDescending(
                    x => x.ReferralDate)
                .Select(x => new
                {
                    x.id,
                    x.EntrepreneurUserId,
                    x.RecruitedUserId,

                    x.ReferralCode,

                    x.ReferralDate,

                    x.ProviderActivationDate,

                    x.ReferralStatus,

                    x.EntrepreneurEligibilityStatus,

                    x.IsDirectReferral,

                    x.EndedAt
                })
                .ToListAsync();

        return Ok(referrals);
    }

    // =====================================================
    // EARNINGS
    // =====================================================

    [HttpGet("earnings")]
    public async Task<IActionResult> GetEarnings()
    {
        var entrepreneurUserId =
            GetCurrentUserId();

        if (!entrepreneurUserId.HasValue)
        {
            return Unauthorized();
        }

        var earnings =
            await _context
                .EntrepreneurEarnings
                .AsNoTracking()
                .Where(x =>
                    x.EntrepreneurUserId ==
                    entrepreneurUserId.Value)
                .OrderByDescending(
                    x => x.TransactionDate)
                .Select(x => new
                {
                    x.Id,

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

                    x.PayoutBatchId,

                    x.PayoutDate,

                    x.PayoutReference,

                    x.CreatedAt,

                    x.UpdatedAt
                })
                .ToListAsync();

        return Ok(earnings);
    }

    // =====================================================
    // DASHBOARD
    // =====================================================

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var entrepreneurUserId = GetCurrentUserId();

        if (!entrepreneurUserId.HasValue)
        {
            return Unauthorized(new
            {
                message = "Authenticated user ID was not found."
            });
        }

        try
        {
            var userId = entrepreneurUserId.Value;

            // ---------------------------------------------------------
            // Get the entrepreneur's own referral code
            // ---------------------------------------------------------
            var entrepreneur = await _context
                .Users
                .AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => new
                {
                    x.Id,
                    x.ReferralCode
                })
                .FirstOrDefaultAsync();

            if (entrepreneur == null)
            {
                return NotFound(new
                {
                    message = "Entrepreneur user account was not found."
                });
            }

            var referralCode = entrepreneur.ReferralCode;

            // Build the registration/referral URL.
            var referralLink = !string.IsNullOrWhiteSpace(referralCode)
                ? $"https://alphaauto.app/register?ref={Uri.EscapeDataString(referralCode)}"
                : null;

            // ---------------------------------------------------------
            // Get direct entrepreneur referrals
            // ---------------------------------------------------------
            var referrals = await _context
                .EntrepreneurReferrals
                .AsNoTracking()
                .Where(x =>
                    x.EntrepreneurUserId == userId &&
                    x.IsDirectReferral &&
                    x.EndedAt == null)
                .ToListAsync();

            // ---------------------------------------------------------
            // Get entrepreneur earnings
            // ---------------------------------------------------------
            var earnings = await _context
                .EntrepreneurEarnings
                .AsNoTracking()
                .Where(x =>
                    x.EntrepreneurUserId == userId)
                .ToListAsync();

            // ---------------------------------------------------------
            // Program configuration
            // ---------------------------------------------------------
            var config = await _context
                .EntrepreneurProgramConfigurations
                .AsNoTracking()
                .FirstOrDefaultAsync();

            // ---------------------------------------------------------
            // Referral statistics
            // ---------------------------------------------------------
            var directRecruits = referrals.Count;

            var activeProviders = referrals.Count(x =>
                string.Equals(
                    x.ReferralStatus,
                    "active",
                    StringComparison.OrdinalIgnoreCase));

            // ---------------------------------------------------------
            // Transaction statistics
            // ---------------------------------------------------------
            var qualifyingTransactions = earnings
                .Select(x => x.OrderId)
                .Distinct()
                .Count();

            var eligibleNetPlatformRevenue =
                earnings.Sum(x => x.EligibleNetPlatformRevenue);

            // ---------------------------------------------------------
            // Earnings statistics
            // ---------------------------------------------------------
            var pendingEarnings = earnings
                .Where(x =>
                    string.Equals(
                        x.EarningStatus,
                        "PENDING",
                        StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.EntrepreneurEarningsAmount);

            var approvedEarnings = earnings
                .Where(x =>
                    string.Equals(
                        x.EarningStatus,
                        "APPROVED",
                        StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.EntrepreneurEarningsAmount);

            var paidEarnings = earnings
                .Where(x =>
                    string.Equals(
                        x.EarningStatus,
                        "PAID",
                        StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.EntrepreneurEarningsAmount);

            // ---------------------------------------------------------
            // Return dashboard
            // ---------------------------------------------------------
            return Ok(new
            {
                directRecruits,
                activeProviders,
                qualifyingTransactions,
                eligibleNetPlatformRevenue,

                // IMPORTANT:
                // These now come from users.referral_code
                referralCode,
                referralLink,

                currentRate =
                    config?.DefaultCommissionRate ?? 0m,

                pendingEarnings,
                approvedEarnings,
                paidEarnings,

                currency =
                    earnings
                        .Select(x => x.Currency)
                        .FirstOrDefault()
                    ?? "USD",

                programEnabled =
                    config?.ProgramEnabled ?? false,

                nextPayoutDate = (DateTime?)null
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Entrepreneur dashboard failed for user {entrepreneurUserId}");

            Console.Error.WriteLine(ex.ToString());

            return StatusCode(500, new
            {
                message = "Failed to load Entrepreneur dashboard.",
                detail = ex.Message,
                innerDetail = ex.InnerException?.Message
            });
        }
    }
}