using Alpha.API.Data;
using Alpha.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Alpha.API.DTOs;
using Alpha.API.Constants;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReferralsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ReferralNetworkService _networkService;
    private readonly ReferralCodeService _codeService;
    private readonly CommunityBuilderDashboardService
    _communityBuilderDashboardService;

    public ReferralsController(
        AppDbContext context,
        ReferralNetworkService networkService,
        ReferralCodeService codeService,
        CommunityBuilderDashboardService
        communityBuilderDashboardService)
    {
        _context = context;
        _networkService = networkService;
        _codeService = codeService;
        _communityBuilderDashboardService =
        communityBuilderDashboardService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var dashboard =
            await _networkService.GetDashboardAsync(
                userId,
                cancellationToken
            );

        return Ok(dashboard);
    }

    [HttpGet("validate/{code}")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateCode(
        string code,
        CancellationToken cancellationToken)
    {
        var normalizedCode =
            code.Trim().ToUpperInvariant();

        var referrer = await _context.Users
            .AsNoTracking()
            .Where(user =>
                user.ReferralCode != null &&
                user.ReferralCode.ToUpper() == normalizedCode &&
                user.IsActive
            )
            .Select(user => new
            {
                valid = true,
                referrerName = user.FullName,
                referrerRole = user.Role
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (referrer == null)
        {
            return NotFound(new
            {
                valid = false,
                message = "Referral code was not found."
            });
        }

        return Ok(referrer);
    }

    [HttpPost("regenerate-code")]
    public async Task<IActionResult> RegenerateCode(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(
                item => item.Id == userId,
                cancellationToken
            );

        if (user == null)
        {
            return NotFound();
        }

        user.ReferralCode =
            await _codeService.GenerateUniqueCodeAsync(
                user.FullName,
                cancellationToken
            );

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            user.ReferralCode
        });
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId
        );
    }

    [HttpGet("admin/overview")]
    [Authorize(Roles = "admin,dispatcher")]
    public async Task<IActionResult> GetAdminOverview(
        CancellationToken cancellationToken)
    {
        var totalMembers = await _context.Users
            .CountAsync(
                user => user.ReferredByUserId != null,
                cancellationToken
            );

        var totalTransactions =
            await _context.ReferralTransactions
                .CountAsync(cancellationToken);

        var pendingCommissions =
            await _context.ReferralTransactions
                .Where(transaction =>
                    transaction.Status == "pending"
                )
                .SumAsync(
                    transaction =>
                        (decimal?)transaction.CommissionAmount,
                    cancellationToken
                ) ?? 0;

        var availableCommissions =
            await _context.ReferralTransactions
                .Where(transaction =>
                    transaction.Status == "available"
                )
                .SumAsync(
                    transaction =>
                        (decimal?)transaction.CommissionAmount,
                    cancellationToken
                ) ?? 0;

        var paidCommissions =
            await _context.ReferralTransactions
                .Where(transaction =>
                    transaction.Status == "paid"
                )
                .SumAsync(
                    transaction =>
                        (decimal?)transaction.CommissionAmount,
                    cancellationToken
                ) ?? 0;

        var topReferrers = await _context.Users
            .AsNoTracking()
            .Select(user => new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.Role,
                user.ReferralCode,

                DirectMembers = _context.Users.Count(
                    child =>
                        child.ReferredByUserId == user.Id
                ),

                ReferralEarnings =
                    _context.ReferralTransactions
                        .Where(transaction =>
                            transaction.BeneficiaryUserId == user.Id &&
                            transaction.Status != "cancelled" &&
                            transaction.Status != "reversed"
                        )
                        .Sum(transaction =>
                            (decimal?)transaction.CommissionAmount
                        ) ?? 0
            })
            .Where(item =>
                item.DirectMembers > 0 ||
                item.ReferralEarnings > 0
            )
            .OrderByDescending(item =>
                item.ReferralEarnings
            )
            .Take(50)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            totalMembers,
            totalTransactions,
            pendingCommissions,
            availableCommissions,
            paidCommissions,
            topReferrers
        });
    }

    [Authorize]
    [HttpGet("community-builder/dashboard")]
    public async Task<ActionResult<
     CommunityBuilderDashboardDto>>
     GetCommunityBuilderDashboard(
         CancellationToken cancellationToken)
    {
        var userIdValue =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(
            userIdValue,
            out var userId))
        {
            return Unauthorized(
                new
                {
                    message =
                        "Invalid authenticated user."
                });
        }

        var dashboard =
            await _communityBuilderDashboardService
                .GetDashboardAsync(
                    userId,
                    cancellationToken);

        if (dashboard is null)
        {
            return NotFound(
                new
                {
                    message =
                        "Community Builder dashboard not found."
                });
        }

        return Ok(dashboard);
    }
}