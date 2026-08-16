using Alpha.API.Data;
using Alpha.API.Models.Entrepreneur;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace Alpha.API.Services.Entrepreneur;

public class EntrepreneurConfigurationService
{
    private readonly AppDbContext _context;

    public EntrepreneurConfigurationService(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task<EntrepreneurProgramConfiguration?>
        GetAsync(
            CancellationToken cancellationToken = default)
    {
        return await _context
            .EntrepreneurProgramConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                cancellationToken);
    }

    public async Task<decimal>
        GetRateAsync(
            string countryCode,
            string currency,
            CancellationToken cancellationToken = default)
    {
        var market =
            await _context
                .EntrepreneurMarketConfigurations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.CountryCode ==
                            countryCode.ToUpper()
                        &&
                        x.Currency ==
                            currency.ToUpper()
                        &&
                        x.IsActive,
                    cancellationToken);

        if (market != null)
            return market.CommissionRate;

        var config =
            await GetAsync(cancellationToken);

        return config?.DefaultCommissionRate ?? 0m;
    }
}