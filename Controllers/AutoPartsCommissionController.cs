using Alpha.API.Data;
using Alpha.API.Models;
using Alpha.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/admin/auto-parts-commission")]
[Authorize]
public class AutoPartsCommissionController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AutoPartsCommissionService _commissionService;

    public AutoPartsCommissionController(
        AppDbContext context,
        AutoPartsCommissionService commissionService)
    {
        _context = context;
        _commissionService = commissionService;
    }

    // ============================================================
    // GET CURRENT POLICY
    // ============================================================

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentPolicy(
        [FromQuery] string currency = "USD")
    {
        currency = currency.Trim().ToUpperInvariant();

        var now = DateTime.UtcNow;

        var policy = await _context.AutoPartsCommissionPolicies
            .Include(x => x.Tiers)
            .Where(x =>
                x.Currency == currency &&
                x.IsActive &&
                x.EffectiveFrom <= now &&
                (x.EffectiveTo == null || x.EffectiveTo > now))
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync();

        if (policy == null)
        {
            return NotFound(new
            {
                message =
                    $"No active auto-parts commission policy found for {currency}."
            });
        }

        var result = new
        {
            id = policy.Id,
            policyName = policy.PolicyName,
            currency = policy.Currency,
            version = policy.Version,
            isActive = policy.IsActive,

            effectiveFrom = policy.EffectiveFrom,
            effectiveTo = policy.EffectiveTo,

            notes = policy.Notes,

            tiers = policy.Tiers
                .OrderBy(x => x.TierOrder)
                .Select(x => new
                {
                    id = x.Id,
                    tierOrder = x.TierOrder,

                    minimumAmount = x.MinimumAmount,
                    maximumAmount = x.MaximumAmount,

                    commissionPercentage =
                        x.CommissionPercentage,

                    isActive = x.IsActive
                })
                .ToList()
        };

        return Ok(result);
    }

    // ============================================================
    // CALCULATE COMMISSION
    // ============================================================

    [HttpPost("calculate")]
    public async Task<IActionResult> CalculateCommission(
        [FromBody] CalculateCommissionRequest request)
    {
        if (request.Subtotal < 0)
        {
            return BadRequest(new
            {
                message = "Subtotal cannot be negative."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            return BadRequest(new
            {
                message = "Currency is required."
            });
        }

        var currency =
            request.Currency.Trim().ToUpperInvariant();

        try
        {
            var result =
                await _commissionService.CalculateCommissionAsync(
                    request.Subtotal,
                    currency
                );

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // ============================================================
    // UPDATE TIER
    // ============================================================

    [HttpPut("tiers/{tierId:guid}")]
    public async Task<IActionResult> UpdateTier(
        Guid tierId,
        [FromBody] UpdateCommissionTierRequest request)
    {
        if (request.MinimumAmount < 0)
        {
            return BadRequest(new
            {
                message = "Minimum amount cannot be negative."
            });
        }

        if (request.MaximumAmount.HasValue &&
            request.MaximumAmount.Value <= request.MinimumAmount)
        {
            return BadRequest(new
            {
                message =
                    "Maximum amount must be greater than minimum amount."
            });
        }

        if (request.CommissionPercentage < 0 ||
            request.CommissionPercentage > 100)
        {
            return BadRequest(new
            {
                message =
                    "Commission percentage must be between 0 and 100."
            });
        }

        var tier = await _context.AutoPartsCommissionTiers
            .FirstOrDefaultAsync(x => x.Id == tierId);

        if (tier == null)
        {
            return NotFound(new
            {
                message = "Commission tier not found."
            });
        }

        // Prevent overlapping ranges with other tiers.
        var overlappingTier =
            await _context.AutoPartsCommissionTiers
                .Where(x =>
                    x.Id != tierId &&
                    x.PolicyId == tier.PolicyId &&
                    x.IsActive)
                .Where(x =>
                    request.MaximumAmount == null
                        ? x.MaximumAmount == null ||
                          x.MaximumAmount > request.MinimumAmount
                        : x.MinimumAmount < request.MaximumAmount &&
                          (x.MaximumAmount == null ||
                           x.MaximumAmount > request.MinimumAmount))
                .FirstOrDefaultAsync();

        if (overlappingTier != null)
        {
            return BadRequest(new
            {
                message =
                    "The commission range overlaps another active tier."
            });
        }

        tier.MinimumAmount = request.MinimumAmount;
        tier.MaximumAmount = request.MaximumAmount;
        tier.CommissionPercentage =
            request.CommissionPercentage;
        tier.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            id = tier.Id,
            tierOrder = tier.TierOrder,
            minimumAmount = tier.MinimumAmount,
            maximumAmount = tier.MaximumAmount,
            commissionPercentage =
                tier.CommissionPercentage,
            isActive = tier.IsActive
        });
    }

    // ============================================================
    // CREATE TIER
    // ============================================================

    [HttpPost("policies/{policyId:guid}/tiers")]
    public async Task<IActionResult> CreateTier(
        Guid policyId,
        [FromBody] CreateCommissionTierRequest request)
    {
        if (request.MinimumAmount < 0)
        {
            return BadRequest(new
            {
                message = "Minimum amount cannot be negative."
            });
        }

        if (request.MaximumAmount.HasValue &&
            request.MaximumAmount.Value <= request.MinimumAmount)
        {
            return BadRequest(new
            {
                message =
                    "Maximum amount must be greater than minimum amount."
            });
        }

        if (request.CommissionPercentage < 0 ||
            request.CommissionPercentage > 100)
        {
            return BadRequest(new
            {
                message =
                    "Commission percentage must be between 0 and 100."
            });
        }

        var policy =
            await _context.AutoPartsCommissionPolicies
                .Include(x => x.Tiers)
                .FirstOrDefaultAsync(x => x.Id == policyId);

        if (policy == null)
        {
            return NotFound(new
            {
                message = "Commission policy not found."
            });
        }

        var overlappingTier =
            policy.Tiers
                .Where(x => x.IsActive)
                .FirstOrDefault(x =>
                    request.MaximumAmount == null
                        ? x.MaximumAmount == null ||
                          x.MaximumAmount > request.MinimumAmount
                        : x.MinimumAmount < request.MaximumAmount &&
                          (x.MaximumAmount == null ||
                           x.MaximumAmount > request.MinimumAmount));

        if (overlappingTier != null)
        {
            return BadRequest(new
            {
                message =
                    "The commission range overlaps another active tier."
            });
        }

        var nextTierOrder =
            policy.Tiers
                .Select(x => x.TierOrder)
                .DefaultIfEmpty(0)
                .Max() + 1;

        var tier = new AutoPartsCommissionTier
        {
            Id = Guid.NewGuid(),

            PolicyId = policy.Id,

            TierOrder = nextTierOrder,

            MinimumAmount = request.MinimumAmount,

            MaximumAmount = request.MaximumAmount,

            CommissionPercentage =
                request.CommissionPercentage,

            IsActive = request.IsActive,

            CreatedAt = DateTime.UtcNow,

            UpdatedAt = DateTime.UtcNow
        };

        _context.AutoPartsCommissionTiers.Add(tier);

        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetCurrentPolicy),
            new
            {
                currency = policy.Currency
            },
            new
            {
                id = tier.Id,
                tierOrder = tier.TierOrder,
                minimumAmount = tier.MinimumAmount,
                maximumAmount = tier.MaximumAmount,
                commissionPercentage =
                    tier.CommissionPercentage,
                isActive = tier.IsActive
            }
        );
    }

    // ============================================================
    // DELETE / DEACTIVATE TIER
    // ============================================================

    [HttpDelete("tiers/{tierId:guid}")]
    public async Task<IActionResult> DeleteTier(
        Guid tierId)
    {
        var tier =
            await _context.AutoPartsCommissionTiers
                .FirstOrDefaultAsync(x => x.Id == tierId);

        if (tier == null)
        {
            return NotFound(new
            {
                message = "Commission tier not found."
            });
        }

        // Soft delete instead of physically deleting
        // financial configuration.
        tier.IsActive = false;
        tier.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Commission tier deactivated successfully."
        });
    }

    // ============================================================
    // ACTIVATE / DEACTIVATE TIER
    // ============================================================

    [HttpPatch("tiers/{tierId:guid}/status")]
    public async Task<IActionResult> UpdateTierStatus(
        Guid tierId,
        [FromBody] UpdateTierStatusRequest request)
    {
        var tier =
            await _context.AutoPartsCommissionTiers
                .FirstOrDefaultAsync(x => x.Id == tierId);

        if (tier == null)
        {
            return NotFound(new
            {
                message = "Commission tier not found."
            });
        }

        tier.IsActive = request.IsActive;
        tier.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            id = tier.Id,
            isActive = tier.IsActive
        });
    }

    // ============================================================
    // UPDATE POLICY
    // ============================================================

    [HttpPut("policies/{policyId:guid}")]
    public async Task<IActionResult> UpdatePolicy(
        Guid policyId,
        [FromBody] UpdateCommissionPolicyRequest request)
    {
        var policy =
            await _context.AutoPartsCommissionPolicies
                .FirstOrDefaultAsync(x => x.Id == policyId);

        if (policy == null)
        {
            return NotFound(new
            {
                message = "Commission policy not found."
            });
        }

        if (string.IsNullOrWhiteSpace(request.PolicyName))
        {
            return BadRequest(new
            {
                message = "Policy name is required."
            });
        }

        policy.PolicyName = request.PolicyName.Trim();

        policy.Notes = request.Notes;

        policy.IsActive = request.IsActive;

        policy.EffectiveFrom =
            request.EffectiveFrom;

        policy.EffectiveTo =
            request.EffectiveTo;

        policy.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            id = policy.Id,
            policyName = policy.PolicyName,
            currency = policy.Currency,
            version = policy.Version,
            isActive = policy.IsActive,
            effectiveFrom = policy.EffectiveFrom,
            effectiveTo = policy.EffectiveTo,
            notes = policy.Notes
        });
    }
}


// ================================================================
// REQUEST MODELS
// ================================================================

public sealed class CalculateCommissionRequest
{
    public decimal Subtotal { get; set; }

    public string Currency { get; set; } = "USD";
}


public sealed class UpdateCommissionTierRequest
{
    public decimal MinimumAmount { get; set; }

    public decimal? MaximumAmount { get; set; }

    public decimal CommissionPercentage { get; set; }

    public bool IsActive { get; set; } = true;
}


public sealed class CreateCommissionTierRequest
{
    public decimal MinimumAmount { get; set; }

    public decimal? MaximumAmount { get; set; }

    public decimal CommissionPercentage { get; set; }

    public bool IsActive { get; set; } = true;
}


public sealed class UpdateTierStatusRequest
{
    public bool IsActive { get; set; }
}


public sealed class UpdateCommissionPolicyRequest
{
    public string PolicyName { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public bool IsActive { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }
}