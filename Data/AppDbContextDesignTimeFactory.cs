using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Alpha.API.Data;

/// <summary>
/// Design-time only factory for reproducible migration generation.
/// Does not contact a database; no production configuration or credentials required.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=5432;Database=alpha_design_time;Username=alpha_ci;Password=synthetic_not_for_use")
            .Options;
        return new AppDbContext(options);
    }
}
