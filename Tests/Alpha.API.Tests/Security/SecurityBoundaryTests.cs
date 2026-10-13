using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Alpha.API.Controllers;
using Alpha.API.Data;
using Alpha.API.Models;
using Alpha.API.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace Alpha.API.Tests.Security;

public sealed class SecurityBoundaryTests
{
    public static ClaimsPrincipal Principal(Guid id, params string[] roles) => new(new ClaimsIdentity(
        roles.Select(role => new Claim(ClaimTypes.Role, role)).Append(new Claim(ClaimTypes.NameIdentifier, id.ToString())), "Test"));
    private static Order Order(Guid customerId) => new() { Id = Guid.NewGuid(), CustomerId = customerId,
        OrderNumber = "TEST", CustomerName = "Synthetic", PickupAddress = "Test", DeliveryAddress = "Test",
        ItemDescription = "Test", Zone = "Test", Status = "pending" };

    [Fact]
    public void PaymentOwnershipRejectsAnotherCustomerAndAnonymousIdentity()
    {
        var id = Guid.NewGuid(); var order = Order(id);
        Assert.True(OrderAccess.CanPay(Principal(id, "customer"), order));
        Assert.False(OrderAccess.CanPay(Principal(Guid.NewGuid(), "customer"), order));
        Assert.False(OrderAccess.CanPay(Principal(id, "supplier"), order));
        Assert.False(OrderAccess.CanPay(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "admin") })), order));
    }

    [Theory]
    [InlineData("suspended")]
    [InlineData("pending")]
    [InlineData("rejected")]
    public void InactiveMembershipCannotRegainLegacyPrimaryRole(string status)
    {
        var selected = ActiveRoles.Select("admin", new[] { new UserRole { RoleKey = "admin", Status = status },
            new UserRole { RoleKey = "customer", Status = "active" } });
        Assert.Equal(new[] { "customer" }, selected);
    }

    [Fact]
    public void LegacyFallbackIsLimitedToAccountsWithoutMemberships()
    {
        Assert.Equal(new[] { "customer" }, ActiveRoles.Select("customer", Array.Empty<UserRole>()));
        Assert.Empty(ActiveRoles.Select("admin", new[] { new UserRole { RoleKey = "admin", Status = "suspended" } }));
    }

    [Fact]
    public void ReorderedMultipleRolesNeverChooseAnUnfilteredRole()
    {
        var id = Guid.NewGuid();
        Assert.Equal(string.Empty, AuthUser.GetRole(Principal(id, "driver", "supplier")));
        Assert.Equal(string.Empty, AuthUser.GetRole(Principal(id, "supplier", "driver")));
        Assert.Equal("admin", AuthUser.GetRole(Principal(id, "supplier", "admin")));
        Assert.Equal("admin", AuthUser.GetRole(Principal(id, "admin", "supplier")));
    }

    [Fact]
    public void DirectPaymentConfirmationIsGoneBeforeAnyDatabaseAccess()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        var controller = new OrdersController(db, null!, null!, null!, null!, null!, null!, null!);
        var result = Assert.IsType<ObjectResult>(controller.ConfirmPayment(Guid.NewGuid(), "unverified"));
        Assert.Equal(410, result.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("short")]
    public void StartupRejectsMissingOrWeakRuntimeSigningKey(string? key)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Jwt:Key"] = key, ["DATABASE_URL"] = "postgresql://synthetic:synthetic@127.0.0.1/test" }).Build();
        Assert.Throws<InvalidOperationException>(() => RuntimeSecurity.Validate(config));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WebhookSignatureRequiresExpectedModeOriginalBodyAndRecentTimestamp(bool live)
    {
        const string body = "{\"event\":\"synthetic\"}"; const string secret = "synthetic-webhook-secret";
        var now = DateTimeOffset.FromUnixTimeSeconds(1800000000); var t = now.ToUnixTimeSeconds().ToString();
        var hash = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(t + "." + body)));
        var header = "t=" + t + ",te=" + (live ? "" : hash) + ",li=" + (live ? hash : "");
        Assert.True(PayMongoSignature.Verify(body, header, secret, live, now));
        Assert.False(PayMongoSignature.Verify(body + " ", header, secret, live, now));
        Assert.False(PayMongoSignature.Verify(body, header, secret, !live, now));
        Assert.False(PayMongoSignature.Verify(body, header, secret, live, now.AddMinutes(6)));
        Assert.False(PayMongoSignature.Verify(body, header, secret, live, now.AddMinutes(-6)));
        Assert.False(PayMongoSignature.Verify(body, header + ",t=" + t, secret, live, now));
    }
}
