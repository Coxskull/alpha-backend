using System.Text.Json;
using Alpha.API.Models;
using Xunit;
namespace Alpha.API.Tests.Security;
public sealed class PrivateProfileTests
{
    [Fact]
    public void UserAndCustomerResponsesNeverSerializePasswordHashes()
    {
        Assert.DoesNotContain("password", JsonSerializer.Serialize(new User { PasswordHash = "synthetic-hash" }), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("synthetic-hash", JsonSerializer.Serialize(new Customer { PasswordHash = "synthetic-hash" }));
    }
}
