using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Alpha.API.Data;

// EF tooling builds metadata without booting the application or reading hosted credentials.
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=127.0.0.1;Database=alpha_metadata_only;Username=unused;Password=unused;SSL Mode=Disable").Options);
}
