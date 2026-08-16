using System;
using System.Threading.Tasks;

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
    public async Task<IActionResult>
        GetConfiguration()
    {
        var config =
            await _context
                .EntrepreneurProgramConfigurations
                .FirstOrDefaultAsync();

        return Ok(config);
    }

    [HttpPut("configuration")]
    public async Task<IActionResult>
        UpdateConfiguration(
            EntrepreneurProgramConfiguration request)
    {
        var config =
            await _context
                .EntrepreneurProgramConfigurations
                .FirstOrDefaultAsync();

        if (config == null)
        {
            request.Id = Guid.NewGuid();

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

            config.MaximumReferralLevel =
                1;

            config.UpdatedAt =
                DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok();
    }
}