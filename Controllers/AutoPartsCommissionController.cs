using Alpha.API.Data;
using Alpha.API.DTOs.AutoPartsCommission.Admin;
using Alpha.API.DTOs;
using Alpha.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Controllers;

[ApiController]
[Route("api/admin/auto-parts-commission")]
[Authorize(Roles = "admin")]
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

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentPolicy(
    [FromQuery] string currency = "USD")
    {
        currency = currency.Trim().ToUpperInvariant();

        var policy = await _context.AutoPartsCommissionPolicies
            .Include(x => x.Tiers)
            .Where(x =>
                x.Currency == currency &&
                x.IsActive)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync();

        if (policy == null)
            return NotFound(new
            {
                message = $"No active policy found for {currency}."
            });

        return Ok(policy);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
    [FromQuery] string currency = "USD")
    {
        currency = currency.Trim().ToUpperInvariant();

        var policies = await _context.AutoPartsCommissionPolicies
            .Include(x => x.Tiers)
            .Where(x => x.Currency == currency)
            .OrderByDescending(x => x.Version)
            .Select(x => new
            {
                x.Id,
                x.PolicyName,
                x.Currency,
                x.Version,
                x.EffectiveFrom,
                x.EffectiveTo,
                x.IsActive,
                x.Notes,
                x.CreatedAt,
                x.UpdatedAt,
                Tiers = x.Tiers
                    .OrderBy(t => t.TierOrder)
                    .Select(t => new
                    {
                        t.Id,
                        t.TierOrder,
                        t.MinimumAmount,
                        t.MaximumAmount,
                        t.CommissionPercentage,
                        t.IsActive
                    })
            })
            .ToListAsync();

        return Ok(policies);
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(
    [FromBody] PreviewAutoPartsCommissionDto dto,
    CancellationToken cancellationToken)
    {
        if (dto.Subtotal <= 0)
        {
            return BadRequest(new
            {
                message = "Subtotal must be greater than zero."
            });
        }

        var calculationDate =
            dto.CalculationDate ?? DateTime.UtcNow;

        var result =
            await _commissionService.CalculateAsync(
                dto.Subtotal,
                dto.Currency,
                calculationDate,
                cancellationToken);

        return Ok(result);
    }

    [HttpPost("policies")]
    public async Task<IActionResult> CreatePolicy(
    [FromBody] CreateAutoPartsCommissionPolicyDto dto,
    CancellationToken cancellationToken)
    {
        if (dto.Tiers == null || dto.Tiers.Count == 0)
        {
            return BadRequest(new
            {
                message = "At least one commission tier is required."
            });
        }

        var currency =
            dto.Currency.Trim().ToUpperInvariant();

        ValidateTiers(dto.Tiers);

        var latestVersion =
            await _context.AutoPartsCommissionPolicies
                .Where(x => x.Currency == currency)
                .Select(x => (int?)x.Version)
                .MaxAsync(cancellationToken)
                ?? 0;

        var policy = new AutoPartsCommissionPolicy
        {
            Id = Guid.NewGuid(),

            PolicyName = dto.PolicyName,

            Currency = currency,

            Version = latestVersion + 1,

            EffectiveFrom = dto.EffectiveFrom,

            IsActive = false,

            Notes = dto.Notes,

            CreatedAt = DateTime.UtcNow,

            UpdatedAt = DateTime.UtcNow
        };

        foreach (var tierDto in dto.Tiers.OrderBy(x => x.TierOrder))
        {
            policy.Tiers.Add(
                new AutoPartsCommissionTier
                {
                    Id = Guid.NewGuid(),

                    PolicyId = policy.Id,

                    TierOrder = tierDto.TierOrder,

                    MinimumAmount = tierDto.MinimumAmount,

                    MaximumAmount = tierDto.MaximumAmount,

                    CommissionPercentage =
                        tierDto.CommissionPercentage,

                    IsActive = tierDto.IsActive,

                    CreatedAt = DateTime.UtcNow,

                    UpdatedAt = DateTime.UtcNow
                });
        }

        _context.AutoPartsCommissionPolicies.Add(policy);

        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetCurrentPolicy),
            new { currency },
            policy);
    }

    private static void ValidateTiers(
    List<CreateAutoPartsCommissionTierDto> tiers)
    {
        var ordered = tiers
            .OrderBy(x => x.MinimumAmount)
            .ToList();

        if (ordered[0].MinimumAmount != 0)
        {
            throw new ArgumentException(
                "The first tier must start at 0.");
        }

        for (int i = 0; i < ordered.Count; i++)
        {
            var tier = ordered[i];

            if (tier.CommissionPercentage < 0 ||
                tier.CommissionPercentage > 100)
            {
                throw new ArgumentException(
                    $"Invalid commission percentage for tier {tier.TierOrder}.");
            }

            if (tier.MaximumAmount.HasValue &&
                tier.MaximumAmount.Value <=
                tier.MinimumAmount)
            {
                throw new ArgumentException(
                    $"Maximum amount must be greater than minimum amount for tier {tier.TierOrder}.");
            }

            if (i < ordered.Count - 1)
            {
                var next = ordered[i + 1];

                if (!tier.MaximumAmount.HasValue)
                {
                    throw new ArgumentException(
                        "Only the final tier may have no maximum amount.");
                }

                if (tier.MaximumAmount.Value !=
                    next.MinimumAmount)
                {
                    throw new ArgumentException(
                        "Commission tiers cannot contain gaps or overlaps.");
                }
            }
        }
    }

    [HttpPost("policies/{id:guid}/activate")]
    public async Task<IActionResult> ActivatePolicy(
    Guid id,
    CancellationToken cancellationToken)
    {
        var policy =
            await _context.AutoPartsCommissionPolicies
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);

        if (policy == null)
            return NotFound();

        var activePolicies =
            await _context.AutoPartsCommissionPolicies
                .Where(x =>
                    x.Currency == policy.Currency &&
                    x.IsActive &&
                    x.Id != policy.Id)
                .ToListAsync(cancellationToken);

        foreach (var active in activePolicies)
        {
            active.IsActive = false;
            active.EffectiveTo = policy.EffectiveFrom;
            active.UpdatedAt = DateTime.UtcNow;
        }

        policy.IsActive = true;
        policy.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Commission policy activated.",
            policyId = policy.Id,
            version = policy.Version
        });
    }


}