using System;
using System.Threading.Tasks;
using Alpha.API.Data;
using Alpha.API.Models.Entrepreneur;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Alpha.API.Controllers.Entrepreneur;

[ApiController]
[Route("api/admin/entrepreneur")]
[Authorize(Roles = "admin")]
public class EntrepreneurAdminController : ControllerBase
{
    private readonly AppDbContext _context;

    public EntrepreneurAdminController(
        AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("configuration")]
    public async Task<IActionResult> GetConfiguration()
    {
        var config =
            await _context
                .EntrepreneurProgramConfigurations
                .FirstOrDefaultAsync();

        return Ok(config);
    }

    [HttpPut("configuration")]
    public async Task<IActionResult> UpdateConfiguration(
        EntrepreneurProgramConfiguration request)
    {
        if (request == null)
        {
            return BadRequest(
                "Configuration request is required.");
        }

        if (request.DefaultCommissionRate < 0 ||
            request.DefaultCommissionRate > 1)
        {
            return BadRequest(
                "Commission rate must be between 0 and 1. " +
                "For 5%, use 0.05.");
        }

        var config =
            await _context
                .EntrepreneurProgramConfigurations
                .FirstOrDefaultAsync();

        if (config == null)
        {
            request.Id = Guid.NewGuid();

            request.MaximumReferralLevel = 1;

            request.UpdatedAt =
                DateTime.UtcNow;

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

            config.UpdatedAt =
                DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(config);
    }
}