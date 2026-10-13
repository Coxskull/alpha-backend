using Alpha.API.Data;
using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace Alpha.API.Integration.Tests;

[CollectionDefinition("Disposable PostgreSQL")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }

public sealed class DatabaseFixture : IAsyncLifetime
{
    // Hardcoded ephemeral loopback target; never use production env connection strings.
    public AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=127.0.0.1;Port=5432;Database=alpha_integration_ci;Username=alpha_ci;Password=disposable-ci-only;SSL Mode=Disable").Options);
    public async Task InitializeAsync()
    {
        using var db = Context();
        await db.Database.MigrateAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;
    public async Task<(Guid OrderId, Guid PaymentId, Guid CustomerId)> SeedAsync(bool paid = true)
    {
        using var db = Context();
        var user = new User { Id = Guid.NewGuid(), FullName = "Synthetic customer", Email = Guid.NewGuid() + "@example.invalid", Role = "customer" };
        var order = new Order { Id = Guid.NewGuid(), CustomerId = user.Id, OrderNumber = Guid.NewGuid().ToString("N"),
            CustomerName = "Synthetic", PickupAddress = "Test", DeliveryAddress = "Test", ItemDescription = "Test",
            Zone = "Test", Status = paid ? "waiting_for_supplier" : "payment_pending", CountryCode = "US", Currency = "USD" };
        var payment = new Payment { Id = Guid.NewGuid(), OrderId = order.Id, Amount = 100m, Currency = "USD",
            PaymentMethod = "paypal", PaymentGateway = "paypal", PaymentStatus = paid ? "paid" : "paypal_created",
            TransactionReference = paid ? "CAPTURE-" + order.Id.ToString("N") : "PROVIDER-" + order.Id.ToString("N") };
        db.AddRange(user, order, payment, new OrderFinancial { Id = Guid.NewGuid(), OrderId = order.Id,
            TotalAmount = 100m, Currency = "USD", CustomerPaid = paid ? 100m : 0m });
        await db.SaveChangesAsync();
        return (order.Id, payment.Id, user.Id);
    }
}
