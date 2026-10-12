using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Cryptography;
using Alpha.API.Services;
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
    private const string StripeSecret = "synthetic-webhook-secret-never-for-deployment-20261012";
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
            ["Supabase__ServiceRoleKey"] = "synthetic", ["STRIPE_WEBHOOK_SECRET"] = StripeSecret, ["PayPal__BaseUrl"] = "http://127.0.0.1:59999"
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
        Assert.Equal(HttpStatusCode.Forbidden, (await Request("/api/refund-allocations/recovery-balances", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Request("/api/refund-allocations/" + Guid.NewGuid(), token, HttpMethod.Post)).StatusCode);
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

    private async Task<(Guid OrderId, Guid PaymentId, Guid CustomerId, string Intent, string Charge)> StripeSeed()
    {
        var seed = await fixture.SeedAsync(); using var db = fixture.Context();
        var payment = await db.Payments.SingleAsync(p => p.Id == seed.PaymentId);
        payment.PaymentGateway = "stripe"; payment.PaymentMethod = "stripe"; payment.GatewayPaymentId = "pi_" + Guid.NewGuid().ToString("N");
        var financial = await db.OrderFinancials.SingleAsync(f => f.OrderId == seed.OrderId);
        financial.FinancialStatus = "verified"; financial.PayoutStatus = "ready_for_payout";
        await db.SaveChangesAsync();
        return (seed.OrderId, seed.PaymentId, seed.CustomerId, payment.GatewayPaymentId, "ch_" + Guid.NewGuid().ToString("N"));
    }
    private static object StripeRefund(string id, long amount, string charge, string intent) => new
    { @object = "refund", id, amount, currency = "usd", status = "succeeded", charge, payment_intent = intent, created = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
    private static string StripePayload(string intent, string charge, string eventId, int mode = 0, long cumulative = 3000, object[]? items = null)
    {
        var list = new Dictionary<string, object> { ["object"] = "list", ["data"] = items ?? new[] { StripeRefund("re_" + charge[3..], cumulative, charge, intent) } };
        if (mode != 3) list["has_more"] = mode == 1;
        return JsonSerializer.Serialize(new { id = eventId, @object = "event", api_version = Stripe.StripeConfiguration.ApiVersion,
            type = "charge.refunded", livemode = false, created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new { @object = new { id = charge, @object = "charge", amount = 10000, amount_refunded = cumulative,
                currency = "usd", paid = true, status = "succeeded", livemode = false, payment_intent = intent,
                refunds = mode == 2 ? null : list } } });
    }
    private async Task<HttpResponseMessage> StripeWebhook(string payload, string? signedPayload = null)
    {
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(StripeSecret), Encoding.UTF8.GetBytes(stamp + "." + (signedPayload ?? payload)))).ToLowerInvariant();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe") { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        request.Headers.Add("Stripe-Signature", "t=" + stamp + ",v1=" + signature);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task SignedIndividualStripeRefundReachesAdminAllocationAndPreservesReplayIdentity()
    {
        var seed = await StripeSeed(); var payload = StripePayload(seed.Intent, seed.Charge, "evt_" + Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, (await StripeWebhook(payload)).StatusCode);
        using var db = fixture.Context();
        var operation = await db.Set<Alpha.API.Models.PaymentOperation>().SingleAsync(o => o.PaymentId == seed.PaymentId && o.Kind == "refund");
        Assert.Equal("completed", operation.State); Assert.Equal(30, operation.Amount); Assert.Equal(seed.Charge, operation.ProviderParentReference);
        var admin = new Alpha.API.Models.User { Id = Guid.NewGuid(), FullName = "Synthetic admin", Email = Guid.NewGuid() + "@example.invalid", Role = "admin" };
        db.Users.Add(admin); await db.SaveChangesAsync(); var token = Token(admin.Id, "admin");
        using var pending = await Request("/api/refund-allocations/pending-operations", token);
        Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
        using var rows = JsonDocument.Parse(await pending.Content.ReadAsStringAsync());
        Assert.Contains(rows.RootElement.EnumerateArray(), row => row.GetProperty("id").GetGuid() == operation.Id);
        using var allocate = new HttpRequestMessage(HttpMethod.Post, "/api/refund-allocations/" + operation.Id)
        { Content = JsonContent.Create(new Alpha.API.DTOs.RefundAllocationDto { Evidence = "Synthetic verified platform attribution", PlatformAmount = 30 }) };
        allocate.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(allocate)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await StripeWebhook(payload)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await StripeWebhook(StripePayload(seed.Intent, seed.Charge, "evt_" + Guid.NewGuid()))).StatusCode);
        using var verify = fixture.Context();
        Assert.Single(await verify.Set<Alpha.API.Models.PaymentOperation>().Where(o => o.PaymentId == seed.PaymentId).ToListAsync());
        Assert.NotNull((await verify.Set<Alpha.API.Models.PaymentOperation>().SingleAsync(o => o.Id == operation.Id)).AllocationFingerprint);
        Assert.Equal("refund_allocated_review", (await verify.OrderFinancials.SingleAsync(f => f.OrderId == seed.OrderId)).FinancialStatus);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task SignedIncompleteStripeEvidenceHoldsRefundWithoutCreatingOperations(int mode)
    {
        var seed = await StripeSeed(); Assert.Equal(HttpStatusCode.OK, (await StripeWebhook(StripePayload(seed.Intent, seed.Charge, "evt_" + Guid.NewGuid(), mode))).StatusCode);
        using var db = fixture.Context();
        Assert.False(await db.Set<Alpha.API.Models.PaymentOperation>().AnyAsync(o => o.PaymentId == seed.PaymentId));
        Assert.Equal(30, (await db.Payments.SingleAsync(p => p.Id == seed.PaymentId)).RefundedAmount);
        var financial = await db.OrderFinancials.SingleAsync(f => f.OrderId == seed.OrderId);
        Assert.Equal("on_hold", financial.PayoutStatus); Assert.Equal("refund_evidence_review", financial.FinancialStatus);
    }

    [Fact]
    public async Task LateCompleteStripeEvidenceCreatesOperationWithoutAddingToRefundTotalAgain()
    {
        var seed = await StripeSeed();
        Assert.Equal(HttpStatusCode.OK, (await StripeWebhook(StripePayload(seed.Intent, seed.Charge, "evt_" + Guid.NewGuid(), 2))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await StripeWebhook(StripePayload(seed.Intent, seed.Charge, "evt_" + Guid.NewGuid()))).StatusCode);
        using var db = fixture.Context(); Assert.Equal(30, (await db.Payments.SingleAsync(p => p.Id == seed.PaymentId)).RefundedAmount);
        Assert.Single(await db.Set<Alpha.API.Models.PaymentOperation>().Where(o => o.PaymentId == seed.PaymentId).ToListAsync());
    }

    [Fact]
    public async Task TamperedSignedStripePayloadIsRejectedBeforeAnyWebhookOrRefundWrite()
    {
        var seed = await StripeSeed(); var eventId = "evt_" + Guid.NewGuid(); var original = StripePayload(seed.Intent, seed.Charge, eventId);
        var tampered = original.Replace("\"amount_refunded\":3000", "\"amount_refunded\":3100"); Assert.NotEqual(original, tampered);
        Assert.Equal(HttpStatusCode.BadRequest, (await StripeWebhook(tampered, original)).StatusCode);
        using var db = fixture.Context(); Assert.Equal(0, (await db.Payments.SingleAsync(p => p.Id == seed.PaymentId)).RefundedAmount);
        Assert.False(await db.PaymentWebhookEvents.AnyAsync(e => e.GatewayEventId == eventId));
    }

    [Fact]
    public async Task ConcurrentStripeRefundsOnSeparatePaymentsPreserveOrderRefundTotal()
    {
        var first = await StripeSeed(); var second = await StripeSeed();
        using (var db = fixture.Context())
            await db.Payments.Where(p => p.Id == second.PaymentId).ExecuteUpdateAsync(s => s.SetProperty(p => p.OrderId, first.OrderId));
        var responses = await Task.WhenAll(
            StripeWebhook(StripePayload(first.Intent, first.Charge, "evt_" + Guid.NewGuid())),
            StripeWebhook(StripePayload(second.Intent, second.Charge, "evt_" + Guid.NewGuid())));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        using var verify = fixture.Context();
        Assert.Equal(60m, (await verify.OrderFinancials.SingleAsync(f => f.OrderId == first.OrderId)).RefundAmount);
        Assert.Equal(60m, await verify.Payments.Where(p => p.OrderId == first.OrderId).SumAsync(p => p.RefundedAmount));
        Assert.Equal(2, await verify.Set<Alpha.API.Models.PaymentOperation>().CountAsync(o => o.PaymentId == first.PaymentId || o.PaymentId == second.PaymentId));
    }

    [Fact]
    public async Task ConcurrentSignedStripeEventsCreateOneRefundOperation()
    {
        var seed = await StripeSeed();
        var responses = await Task.WhenAll(StripeWebhook(StripePayload(seed.Intent, seed.Charge, "evt_" + Guid.NewGuid())), StripeWebhook(StripePayload(seed.Intent, seed.Charge, "evt_" + Guid.NewGuid())));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        using var db = fixture.Context(); Assert.Single(await db.Set<Alpha.API.Models.PaymentOperation>().Where(o => o.PaymentId == seed.PaymentId).ToListAsync());
        Assert.Equal(30, (await db.Payments.SingleAsync(p => p.Id == seed.PaymentId)).RefundedAmount);
    }

    [Fact]
    public async Task HistoricalStripeSnapshotDoesNotRegressRefundTotalOrDuplicateKnownIds()
    {
        var seed = await StripeSeed(); var first = "re_first_" + Guid.NewGuid(); var second = "re_second_" + Guid.NewGuid();
        var current = StripePayload(seed.Intent, seed.Charge, "evt_" + Guid.NewGuid(), items: new[] { StripeRefund(first, 1000, seed.Charge, seed.Intent), StripeRefund(second, 2000, seed.Charge, seed.Intent) });
        Assert.Equal(HttpStatusCode.OK, (await StripeWebhook(current)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await StripeWebhook(StripePayload(seed.Intent, seed.Charge, "evt_" + Guid.NewGuid(), cumulative: 1000, items: new[] { StripeRefund(first, 1000, seed.Charge, seed.Intent) }))).StatusCode);
        using var db = fixture.Context(); Assert.Equal(30, (await db.Payments.SingleAsync(p => p.Id == seed.PaymentId)).RefundedAmount);
        Assert.Equal(2, await db.Set<Alpha.API.Models.PaymentOperation>().CountAsync(o => o.PaymentId == seed.PaymentId));
        Assert.Equal("refund_review", (await db.OrderFinancials.SingleAsync(f => f.OrderId == seed.OrderId)).FinancialStatus);
    }

    [Fact]
    public async Task ConflictingStripeRefundIdsKeepCustomerRefundHeldAndPersistNoCandidateOperations()
    {
        var seed = await StripeSeed(); var other = await fixture.SeedAsync(); var conflictId = "re_zzz_" + Guid.NewGuid(); var candidateId = "re_aaa_" + Guid.NewGuid();
        using (var db = fixture.Context())
        {
            db.Add(new Alpha.API.Models.PaymentOperation { Id = Guid.NewGuid(), PaymentId = other.PaymentId, Kind = "refund", State = "completed", Amount = 15, Currency = "USD",
                IdempotencyKey = "stripe:refund:" + conflictId, ProviderRequestId = Guid.NewGuid().ToString("N"), ProviderReference = conflictId, ProviderParentReference = "ch_other" });
            await db.SaveChangesAsync();
        }
        var payload = StripePayload(seed.Intent, seed.Charge, "evt_" + Guid.NewGuid(), items: new[] { StripeRefund(candidateId, 1500, seed.Charge, seed.Intent), StripeRefund(conflictId, 1500, seed.Charge, seed.Intent) });
        Assert.Equal(HttpStatusCode.OK, (await StripeWebhook(payload)).StatusCode);
        using var verify = fixture.Context(); Assert.False(await verify.Set<Alpha.API.Models.PaymentOperation>().AnyAsync(o => o.PaymentId == seed.PaymentId));
        Assert.Equal(30, (await verify.Payments.SingleAsync(p => p.Id == seed.PaymentId)).RefundedAmount);
        Assert.Equal("refund_evidence_conflict", (await verify.OrderFinancials.SingleAsync(f => f.OrderId == seed.OrderId)).FinancialStatus);
    }

    [Fact]
    public async Task StripeRefundCommitFailureRollsBackLedgerAndBlocksPayoutUntilWebhookRecovery()
    {
        var seed = await StripeSeed(); var eventId = "evt_" + Guid.NewGuid(); var payload = StripePayload(seed.Intent, seed.Charge, eventId);
        using var db = fixture.Context();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION stripe_evidence_review_failure() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'synthetic refund evidence failure'; END $$;
            CREATE TRIGGER stripe_evidence_review_failure BEFORE INSERT ON payment_operations
            FOR EACH ROW EXECUTE FUNCTION stripe_evidence_review_failure();
            """);
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await StripeWebhook(payload)).StatusCode);
            Assert.False(await db.Set<Alpha.API.Models.PaymentOperation>().AnyAsync(o => o.PaymentId == seed.PaymentId));
            Assert.Equal(0, (await db.Payments.SingleAsync(p => p.Id == seed.PaymentId)).RefundedAmount);
            Assert.False((await db.PaymentWebhookEvents.SingleAsync(e => e.GatewayEventId == eventId)).Processed);
            using var gate = fixture.Context(); await using var tx = await gate.Database.BeginTransactionAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => PayoutAccountingService.LockOrderAsync(gate, seed.OrderId, CancellationToken.None));
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER stripe_evidence_review_failure ON payment_operations; DROP FUNCTION stripe_evidence_review_failure();"); }
        Assert.Equal(HttpStatusCode.OK, (await StripeWebhook(payload)).StatusCode);
        using var verify = fixture.Context(); Assert.Equal(30, (await verify.Payments.SingleAsync(p => p.Id == seed.PaymentId)).RefundedAmount);
        Assert.True((await verify.PaymentWebhookEvents.SingleAsync(e => e.GatewayEventId == eventId)).Processed);
    }

    [Fact]
    public async Task AmbiguousStripePaymentMappingLeavesBothOrdersGatedForReview()
    {
        var first = await StripeSeed(); var second = await StripeSeed();
        using (var db = fixture.Context()) await db.Payments.Where(p => p.Id == second.PaymentId).ExecuteUpdateAsync(s => s.SetProperty(p => p.GatewayPaymentId, first.Intent));
        Assert.Equal(HttpStatusCode.InternalServerError, (await StripeWebhook(StripePayload(first.Intent, first.Charge, "evt_" + Guid.NewGuid()))).StatusCode);
        using var verify = fixture.Context();
        Assert.False(await verify.Set<Alpha.API.Models.PaymentOperation>().AnyAsync(o => o.PaymentId == first.PaymentId || o.PaymentId == second.PaymentId));
        Assert.True(await PayoutAccountingService.HasPendingStripeRefundAsync(verify, first.OrderId, CancellationToken.None));
        Assert.True(await PayoutAccountingService.HasPendingStripeRefundAsync(verify, second.OrderId, CancellationToken.None));
    }

    [Fact]
    public async Task StripeEvidenceConflictWaitsForExistingPayeesRecoveryLock()
    {
        var seed = await StripeSeed();
        Assert.Equal(HttpStatusCode.OK, (await StripeWebhook(StripePayload(seed.Intent, seed.Charge, "evt_" + Guid.NewGuid()))).StatusCode);
        using (var db = fixture.Context())
        {
            var batch = new Alpha.API.Models.Entrepreneur.EntrepreneurPayoutBatch { Id = Guid.NewGuid(), Currency = "USD", TotalAmount = 40, EarningCount = 1, Status = "PAID" };
            var earning = new Alpha.API.Models.Entrepreneur.EntrepreneurEarning { Id = Guid.NewGuid(), EntrepreneurUserId = seed.CustomerId, OrderId = seed.OrderId,
                PaymentId = seed.PaymentId, EntrepreneurEarningsAmount = 40, Currency = "USD", EarningStatus = "PAID", PayoutDate = DateTime.UtcNow, PayoutBatchId = batch.Id };
            db.AddRange(batch, earning); await db.SaveChangesAsync();
            var operation = await db.Set<Alpha.API.Models.PaymentOperation>().SingleAsync(o => o.PaymentId == seed.PaymentId);
            await new RefundAllocationService(db).AllocateAsync(operation.Id, new Alpha.API.DTOs.RefundAllocationDto { Evidence = "Synthetic attributable paid earning",
                Lines = new() { new() { SourceKind = "earning", SourceId = earning.Id, Amount = 30 } } }, Guid.NewGuid(), CancellationToken.None);
        }
        using var gate = fixture.Context(); await using var tx = await gate.Database.BeginTransactionAsync();
        await new RefundAllocationService(gate).LockPayeeAsync("entrepreneur", seed.CustomerId, "USD", CancellationToken.None);
        var payload = StripePayload(seed.Intent, seed.Charge, "evt_" + Guid.NewGuid(), items: new[] { StripeRefund("re_" + seed.Charge[3..], 3000, "ch_wrong_parent", seed.Intent) });
        var webhook = StripeWebhook(payload); var released = false;
        try
        {
            var waiting = false;
            for (var i = 0; i < 50; i++)
            {
                waiting = await gate.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype = 'advisory' AND NOT granted) AS \"Value\"").SingleAsync();
                if (waiting) break;
                await Task.Delay(100);
            }
            Assert.True(waiting, "Evidence processing must wait on the payee recovery lock.");
            Assert.False(webhook.IsCompleted);
            using (var snapshot = fixture.Context()) Assert.Equal("refund_allocated_review", (await snapshot.OrderFinancials.SingleAsync(f => f.OrderId == seed.OrderId)).FinancialStatus);
            await tx.CommitAsync(); released = true;
            Assert.Equal(HttpStatusCode.OK, (await webhook).StatusCode);
            using var verify = fixture.Context(); Assert.Equal("refund_evidence_conflict", (await verify.OrderFinancials.SingleAsync(f => f.OrderId == seed.OrderId)).FinancialStatus);
        }
        finally { if (!released) { await tx.RollbackAsync(); await webhook; } }
    }

    [Fact]
    public async Task AnonymousFinancialDiagnosticAndPaymentRequestsAreDenied()
    {
        foreach (var path in new[] { "/api/refund-allocations/recovery-balances", "/api/Financials/settlement/" + Guid.NewGuid() + "/payout-quote", "/api/Financials/settlement-queue", "/api/database-diagnostic", "/api/paypal/order/" + Guid.NewGuid() })
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
