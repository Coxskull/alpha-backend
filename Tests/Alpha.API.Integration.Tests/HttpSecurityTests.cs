using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Claims;
using System.Text;
using Alpha.API.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;
namespace Alpha.API.Integration.Tests;

[Collection("Disposable PostgreSQL")]
public sealed class HttpSecurityTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private const string Key = "synthetic-ci-signing-key-never-for-deployment-20261012";
    private readonly HttpClient client = new(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri("http://127.0.0.1:5085") };
    private Process? host;
    public async Task InitializeAsync()
    {
        var start = new ProcessStartInfo("dotnet", typeof(PayPalController).Assembly.Location) { UseShellExecute = false };
        foreach (var key in start.Environment.Keys.ToArray())
            if (new[] { "paypal", "paymongo", "stripe", "supabase", "database", "jwt", "connectionstrings" }.Any(s => key.Contains(s, StringComparison.OrdinalIgnoreCase)))
                start.Environment.Remove(key);
        foreach (var pair in new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development", ["ASPNETCORE_URLS"] = "http://127.0.0.1:5085", ["PORT"] = "",
            ["Jwt__Key"] = Key, ["Jwt__Issuer"] = "alpha-test", ["Jwt__Audience"] = "alpha-test",
            ["DATABASE_URL"] = "postgresql://alpha_ci:disposable-ci-only@127.0.0.1:5432/alpha_integration_ci",
            ["Database__AllowLocalPlaintext"] = "true", ["SUPABASE_URL"] = "http://127.0.0.1:59999",
            ["SUPABASE_SERVICE_ROLE_KEY"] = "synthetic", ["Supabase__Url"] = "http://127.0.0.1:59999",
            ["Supabase__ServiceRoleKey"] = "synthetic", ["PayPal__BaseUrl"] = "http://127.0.0.1:59999"
        }) start.Environment[pair.Key] = pair.Value;
        host = Process.Start(start) ?? throw new InvalidOperationException("Test host failed to start.");
        for (var i = 0; i < 100; i++)
        {
            if (host.HasExited) throw new InvalidOperationException("Test host exited before startup.");
            try { if ((await client.GetAsync("/health")).StatusCode == HttpStatusCode.OK) return; }
            catch (HttpRequestException) { }
            await Task.Delay(100);
        }
        throw new TimeoutException("Test host startup timeout.");
    }
    public async Task DisposeAsync()
    {
        client.Dispose();
        if (host is { HasExited: false })
        {
            var stop = new ProcessStartInfo("kill") { UseShellExecute = false };
            stop.ArgumentList.Add("-TERM"); stop.ArgumentList.Add(host.Id.ToString());
            using var signal = Process.Start(stop);
            if (signal != null) await signal.WaitForExitAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { await host.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { host.Kill(entireProcessTree: true); await host.WaitForExitAsync(); }
        }
        host?.Dispose();
    }
    private static string Token(Guid id, string role, DateTime? expiry = null, string issuer = "alpha-test")
        => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer, "alpha-test", new[] {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role) },
            expires: expiry ?? DateTime.UtcNow.AddMinutes(10), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256)));
    private async Task<HttpResponseMessage> Request(string path, string? token = null, HttpMethod? method = null)
    {
        var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (method == HttpMethod.Post) request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        return await client.SendAsync(request);
    }

    private static object Registration(string email, string[] roles, bool terms = true, string? referral = null) => new
    {
        FullName = "Synthetic Signup", Email = email, Password = "synthetic-password-123!",
        Phone = "+15555550123", City = "Test City", Country = "MX",
        BusinessName = "Synthetic Store", SelectedRoles = roles,
        AcceptTerms = terms, AcceptRewardsPolicy = true, ReferralCode = referral
    };

    [Theory]
    [InlineData("customer")]
    [InlineData("community_builder")]
    [InlineData("driver")]
    [InlineData("mechanic")]
    [InlineData("supplier")]
    public async Task PublicSignupPersistsProfilesAndLoginGrantsOnlyActiveRoles(string role)
    {
        var email = Guid.NewGuid() + "@example.invalid";
        var roles = role == "customer" ? new[] { role } : new[] { "customer", role };
        using var response = await client.PostAsJsonAsync("/api/Auth/register", Registration(email, roles));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        using var db = fixture.Context();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        Assert.True(BCrypt.Net.BCrypt.Verify("synthetic-password-123!", user.PasswordHash));
        var customer = await db.Customers.SingleAsync(c => c.Id == user.Id);
        Assert.Equal("+15555550123", customer.PhoneNumber);
        Assert.Empty(customer.PasswordHash);
        Assert.True(customer.IsActive);
        Assert.True(await db.EntrepreneurProfiles.AnyAsync(p => p.UserId == user.Id));
        Assert.True(await db.MemberBusinessActivities.AnyAsync(a => a.UserId == user.Id && !a.IsBusinessActive));
        if (role == "community_builder")
        {
            Assert.True(await db.CommunityBuilderProfiles.AnyAsync(p => p.UserId == user.Id));
            Assert.True(await db.CommunityBuilderCities.AnyAsync(c => c.BuilderUserId == user.Id && c.State == null));
            var duplicateCity = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO community_builder_cities
                    (id, builder_user_id, city, state, country, member_count, active_member_count, completed_transaction_count, connected_at)
                VALUES ({Guid.NewGuid()}, {user.Id}, {"Test City"}, NULL, {"MX"}, 0, 0, 0, {DateTime.UtcNow})
                ON CONFLICT (builder_user_id, city, state, country) DO NOTHING;
                """);
            Assert.Equal(0, duplicateCity);
            Assert.Equal(1, await db.CommunityBuilderCities.CountAsync(c => c.BuilderUserId == user.Id));
        }
        if (role == "driver") Assert.True(await db.Drivers.AnyAsync(d => d.UserId == user.Id));
        if (role == "mechanic") Assert.True(await db.Mechanics.AnyAsync(m => m.UserId == user.Id));
        if (role == "supplier") Assert.True(await db.Suppliers.AnyAsync(s => s.UserId == user.Id));
        using var login = await client.PostAsJsonAsync("/api/Auth/login", new { Email = email.ToUpperInvariant(), Password = "synthetic-password-123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var result = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = result.RootElement.GetProperty("token").GetString()!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var claimedRoles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "role").Select(c => c.Value).ToArray();
        Assert.Contains("customer", claimedRoles);
        if (role == "community_builder") Assert.Contains(role, claimedRoles);
        if (role is "driver" or "mechanic" or "supplier")
        {
            Assert.DoesNotContain(role, claimedRoles);
            Assert.Equal("profile_incomplete", (await db.UserRoles.SingleAsync(r => r.UserId == user.Id && r.RoleKey == role)).Status);
        }
        Assert.Equal(HttpStatusCode.OK, (await Request("/api/Cart/" + user.Id, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Request("/api/database-diagnostic", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/Auth/register", Registration(email.ToUpperInvariant(), roles))).StatusCode);
    }

    [Fact]
    public async Task ReferredOperationalSignupLinksAccountsWithoutCreatingRewards()
    {
        var referrerEmail = Guid.NewGuid() + "@example.invalid";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/Auth/register", Registration(referrerEmail, new[] { "community_builder" }))).StatusCode);
        using var db = fixture.Context();
        var referrer = await db.Users.SingleAsync(u => u.Email == referrerEmail);
        var email = Guid.NewGuid() + "@example.invalid";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/Auth/register", Registration(email, new[] { "customer", "driver" }, referral: referrer.ReferralCode))).StatusCode);
        var recruit = await db.Users.SingleAsync(u => u.Email == email);
        Assert.Equal(referrer.Id, recruit.ReferredByUserId);
        Assert.True(await db.EntrepreneurReferrals.AnyAsync(r => r.RecruitedUserId == recruit.Id && r.EntrepreneurUserId == referrer.Id));
        Assert.False(await db.ReferralTransactions.AnyAsync(r => r.SourceUserId == recruit.Id));
    }

    [Fact]
    public async Task LateSignupFailureRollsBackUserAndAllProfiles()
    {
        var email = Guid.NewGuid() + "@example.invalid";
        using var db = fixture.Context();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION signup_review_failure() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'synthetic signup failure'; END $$;
            CREATE TRIGGER signup_review_failure BEFORE INSERT ON member_business_activity
            FOR EACH ROW EXECUTE FUNCTION signup_review_failure();
            """);
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsJsonAsync("/api/Auth/register", Registration(email, new[] { "customer", "community_builder" }))).StatusCode);
            Assert.False(await db.Users.AnyAsync(u => u.Email == email));
            Assert.False(await db.Customers.AnyAsync(c => c.Email == email));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER signup_review_failure ON member_business_activity; DROP FUNCTION signup_review_failure();");
        }
    }

    [Theory]
    [InlineData("admin", true, null)]
    [InlineData("dispatcher", true, null)]
    [InlineData("customer", false, null)]
    [InlineData("customer", true, "NONEXISTENT-REFERRAL")]
    public async Task InvalidSignupDoesNotPersistAnAccount(string role, bool terms, string? referral)
    {
        var email = Guid.NewGuid() + "@example.invalid";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/Auth/register", Registration(email, new[] { role }, terms, referral))).StatusCode);
        using var db = fixture.Context();
        Assert.False(await db.Users.AnyAsync(u => u.Email == email));
    }

    [Fact]
    public async Task AnonymousFinancialDiagnosticAndPaymentRequestsAreDenied()
    {
        foreach (var path in new[] { "/api/Financials/settlement-queue", "/api/database-diagnostic", "/api/paypal/order/" + Guid.NewGuid() })
            Assert.Equal(HttpStatusCode.Unauthorized, (await Request(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Request("/api/paypal/refund", method: HttpMethod.Post)).StatusCode);
    }
    [Fact]
    public async Task AuthenticatedCustomerCanReadOwnPaymentButNotAnotherCustomersPayment()
    {
        var own = await fixture.SeedAsync(); var other = await fixture.SeedAsync();
        var token = Token(own.CustomerId, "customer");
        Assert.Equal(HttpStatusCode.OK, (await Request("/api/paypal/order/" + own.OrderId, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Request("/api/paypal/order/" + other.OrderId, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Request("/api/database-diagnostic", token)).StatusCode);
    }

    [Fact]
    public async Task CustomerCannotReadAnotherCustomersCartAddressesOrVehicles()
    {
        var own = await fixture.SeedAsync(); var other = await fixture.SeedAsync(); var token = Token(own.CustomerId, "customer");
        Assert.Equal(HttpStatusCode.OK, (await Request("/api/Cart/" + own.CustomerId, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Request("/api/Cart/" + other.CustomerId, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Request("/api/Addresses/" + other.CustomerId, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Request("/api/Vehicles/customer/" + other.CustomerId, token)).StatusCode);
    }

    [Fact]
    public async Task DeactivatedUserCannotContinueUsingExistingToken()
    {
        var own = await fixture.SeedAsync(); var token = Token(own.CustomerId, "customer");
        Assert.Equal(HttpStatusCode.OK, (await Request("/api/paypal/order/" + own.OrderId, token)).StatusCode);
        using var db = fixture.Context();
        await db.Users.Where(u => u.Id == own.CustomerId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Request("/api/paypal/order/" + own.OrderId, token)).StatusCode);
    }
    [Fact]
    public async Task SuspendedRoleCannotBeRecoveredFromLegacyPrimaryRoleOrTokenClaims()
    {
        var own = await fixture.SeedAsync(); using var db = fixture.Context();
        if (!await db.EntrepreneurRoles.AnyAsync(r => r.RoleKey == "customer"))
            db.EntrepreneurRoles.Add(new Alpha.API.Models.EntrepreneurRole { Id = Guid.NewGuid(), RoleKey = "customer", DisplayName = "Customer" });
        db.UserRoles.Add(new Alpha.API.Models.UserRole { Id = Guid.NewGuid(), UserId = own.CustomerId, RoleKey = "customer", Status = "suspended" });
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Request("/api/paypal/order/" + own.OrderId, Token(own.CustomerId, "customer"))).StatusCode);
    }
    [Fact]
    public async Task ExpiredAndWrongIssuerTokensAreDenied()
    {
        var own = await fixture.SeedAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Request("/api/paypal/order/" + own.OrderId, Token(own.CustomerId, "customer", DateTime.UtcNow.AddMinutes(-10)))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Request("/api/paypal/order/" + own.OrderId, Token(own.CustomerId, "customer", issuer: "other-issuer"))).StatusCode);
    }
    [Fact]
    public async Task AuthenticationRequestsAreRateLimited()
    {
        HttpStatusCode last = default;
        for (var i = 0; i < 21; i++) last = (await Request("/api/Auth/login", method: HttpMethod.Post)).StatusCode;
        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }
}
