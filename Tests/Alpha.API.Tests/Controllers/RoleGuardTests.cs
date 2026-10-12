using System.Security.Claims;
using Alpha.API.Controllers;
using Alpha.API.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alpha.API.Tests.Controllers;

public sealed class RoleGuardTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("customer")]
    public async Task AssignedOrdersRejectsMissingOrUnsupportedRoleBeforeQuerying(string? role)
    {
        // No provider is configured: accessing the database would fail this test.
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        var controller = new OrdersController(
            context, null!, null!, null!, null!, null!, null!, null!)
        {
            ControllerContext = CreateControllerContext(role)
        };

        var result = await controller.GetMyAssignedOrders(CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("driver")]
    public async Task SupplierEarningsRejectsMissingOrUnsupportedRoleBeforeQuerying(string? role)
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        var controller = new FinancialsController(context, null!, null!)
        {
            ControllerContext = CreateControllerContext(role)
        };

        var result = await controller.GetSupplierEarnings(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    private static ControllerContext CreateControllerContext(string? role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };

        if (role != null)
            claims.Add(new Claim(ClaimTypes.Role, role));

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };
    }
}
