using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Alpha.API.Controllers;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Alpha.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace Alpha.API.Integration.Tests;

[Collection("Disposable PostgreSQL")]
public sealed class PaymentLifecycleTests(DatabaseFixture fixture)
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static ClaimsPrincipal Principal(Guid id, params string[] roles) => new(new ClaimsIdentity(
        roles.Select(role => new Claim(ClaimTypes.Role, role)).Append(new Claim(ClaimTypes.NameIdentifier, id.ToString())), "Test"));
    private static PayPalController Controller(Alpha.API.Data.AppDbContext db, ProviderHandler handler, ClaimsPrincipal user)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["PayPal:BaseUrl"] = "https://provider.invalid", ["PayPal:ClientId"] = "synthetic", ["PayPal:ClientSecret"] = "synthetic" }).Build();
        return new(db, new PayPalService(new HttpClient(handler), config), new SettlementService(db, new TaxEngineService(db), null!),
            new ReferralCommissionService(db)) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } } };
    }

    [Fact]
    public async Task ConcurrentRefundReservationsCannotOverdrawAvailableBalance()
    {
        var seed = await fixture.SeedAsync();
        async Task<bool> Reserve(string key)
        {
            using var db = fixture.Context();
            var payment = await db.Payments.SingleAsync(p => p.Id == seed.PaymentId);
            try { await new PaymentOperationService(db).ReserveAsync(seed.PaymentId, "refund", key, 60m, "USD", payment.TransactionReference!, None); return true; }
            catch (InvalidOperationException) { return false; }
        }
        var results = await Task.WhenAll(Reserve("request-1"), Reserve("request-2"));
        Assert.Single(results, success => success);
        using var verify = fixture.Context();
        Assert.Equal(60m, await verify.Set<PaymentOperation>().Where(o => o.PaymentId == seed.PaymentId).SumAsync(o => o.Amount));
        Assert.Equal("on_hold", (await verify.OrderFinancials.SingleAsync(f => f.OrderId == seed.OrderId)).PayoutStatus);
    }

    [Fact]
    public async Task SameKeyReservesOnceAndRejectsChangedAmount()
    {
        var seed = await fixture.SeedAsync();
        using var db = fixture.Context(); var service = new PaymentOperationService(db);
        var payment = await db.Payments.SingleAsync(p => p.Id == seed.PaymentId);
        var first = await service.ReserveAsync(seed.PaymentId, "refund", "same-key", 10m, "USD", payment.TransactionReference!, None);
        var again = await service.ReserveAsync(seed.PaymentId, "refund", "same-key", 10m, "USD", payment.TransactionReference!, None);
        Assert.True(first.IsNew); Assert.False(again.IsNew); Assert.Equal(first.Operation.Id, again.Operation.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReserveAsync(seed.PaymentId, "refund", "same-key", 11m, "USD", payment.TransactionReference!, None));
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("FAILED")]
    [InlineData("CANCELLED")]
    [InlineData("MALFORMED")]
    public async Task UnconfirmedRefundDoesNotAdvanceLedgerAndRetryMakesNoSecondProviderCall(string status)
    {
        var seed = await fixture.SeedAsync(); using var db = fixture.Context();
        var payment = await db.Payments.SingleAsync(p => p.Id == seed.PaymentId);
        var handler = new ProviderHandler { ResponseBody = status == "MALFORMED" ? "{}" : RefundJson(status, "10.00", payment.TransactionReference!) };
        var controller = Controller(db, handler, Principal(Guid.NewGuid(), "admin"));
        var dto = new RefundPaymentDto { OrderId = seed.OrderId, Amount = 10m, IdempotencyKey = "pending-refund" };
        Assert.IsType<AcceptedResult>(await controller.Refund(dto, None));
        Assert.IsType<ConflictObjectResult>(await controller.Refund(dto, None));
        Assert.Equal(1, handler.ProviderPosts);
        using var verify = fixture.Context();
        Assert.Equal(0m, (await verify.Payments.SingleAsync(p => p.Id == seed.PaymentId)).RefundedAmount);
        Assert.Equal("unknown", (await verify.Set<PaymentOperation>().SingleAsync(o => o.PaymentId == seed.PaymentId)).State);
    }

    [Fact]
    public async Task CompletedRefundIsAppliedOnceAndHoldsUnadjustedPayouts()
    {
        var seed = await fixture.SeedAsync(); using var db = fixture.Context();
        var payment = await db.Payments.SingleAsync(p => p.Id == seed.PaymentId);
        var handler = new ProviderHandler { ResponseBody = RefundJson("COMPLETED", "10.00", payment.TransactionReference!) };
        var controller = Controller(db, handler, Principal(Guid.NewGuid(), "admin"));
        var dto = new RefundPaymentDto { OrderId = seed.OrderId, Amount = 10m, IdempotencyKey = "completed-refund" };
        Assert.IsType<OkObjectResult>(await controller.Refund(dto, None));
        Assert.IsType<OkObjectResult>(await controller.Refund(dto, None));
        Assert.Equal(1, handler.ProviderPosts);
        using var verify = fixture.Context();
        Assert.Equal(10m, (await verify.Payments.SingleAsync(p => p.Id == seed.PaymentId)).RefundedAmount);
        Assert.Equal(10m, (await verify.OrderFinancials.SingleAsync(f => f.OrderId == seed.OrderId)).RefundAmount);
        Assert.Equal("on_hold", (await verify.OrderFinancials.SingleAsync(f => f.OrderId == seed.OrderId)).PayoutStatus);
        Assert.Single(await verify.AuditLogs.Where(a => a.OrderId == seed.OrderId).ToListAsync());
    }

    [Fact]
    public async Task ProviderTimeoutRetainsReservationAndDoesNotRepeatRefund()
    {
        var seed = await fixture.SeedAsync(); using var db = fixture.Context();
        var handler = new ProviderHandler { FailProvider = true };
        var controller = Controller(db, handler, Principal(Guid.NewGuid(), "admin"));
        var dto = new RefundPaymentDto { OrderId = seed.OrderId, Amount = 10m, IdempotencyKey = "timeout-refund" };
        Assert.Equal(503, Assert.IsType<ObjectResult>(await controller.Refund(dto, None)).StatusCode);
        Assert.IsType<ConflictObjectResult>(await controller.Refund(dto, None));
        Assert.Equal(1, handler.ProviderPosts);
        using var verify = fixture.Context();
        Assert.Equal("unknown", (await verify.Set<PaymentOperation>().SingleAsync(o => o.PaymentId == seed.PaymentId)).State);
    }

    [Fact]
    public async Task CrossOrderCaptureAndCustomerRefundAreDeniedBeforeProviderCalls()
    {
        var seed = await fixture.SeedAsync(false); using var db = fixture.Context(); var handler = new ProviderHandler();
        var controller = Controller(db, handler, Principal(Guid.NewGuid(), "customer"));
        Assert.IsType<ForbidResult>(await controller.CapturePayPalOrder(new CapturePayPalOrderDto { OrderId = seed.OrderId, PayPalOrderId = "untrusted" }, None));
        Assert.IsType<ForbidResult>(await controller.Refund(new RefundPaymentDto { OrderId = seed.OrderId, Amount = 10m, IdempotencyKey = "denied" }, None));
        Assert.Equal(0, handler.ProviderPosts);
        Assert.Empty(await db.Set<PaymentOperation>().Where(o => o.PaymentId == seed.PaymentId).ToListAsync());
    }

    [Fact]
    public async Task DuplicateCaptureAppliesOnePaidTransitionAndOneSetOfAuditEffects()
    {
        var seed = await fixture.SeedAsync(false); using var db = fixture.Context();
        var payment = await db.Payments.SingleAsync(p => p.Id == seed.PaymentId);
        var providerOrder = payment.TransactionReference!;
        var handler = new ProviderHandler { ResponseBody = CaptureJson(providerOrder, seed.OrderId) };
        var controller = Controller(db, handler, Principal(seed.CustomerId, "customer"));
        var dto = new CapturePayPalOrderDto { OrderId = seed.OrderId, PayPalOrderId = providerOrder };
        Assert.IsType<OkObjectResult>(await controller.CapturePayPalOrder(dto, None));
        Assert.IsType<OkObjectResult>(await controller.CapturePayPalOrder(dto, None));
        Assert.Equal(1, handler.ProviderPosts);
        using var verify = fixture.Context();
        Assert.Equal("paid", (await verify.Payments.SingleAsync(p => p.Id == seed.PaymentId)).PaymentStatus);
        Assert.Equal(2, await verify.StatusHistory.CountAsync(h => h.OrderId == seed.OrderId));
        Assert.Single(await verify.AuditLogs.Where(a => a.OrderId == seed.OrderId).ToListAsync());
    }

    [Fact]
    public async Task CaptureUnknownOutcomeIsReconciledByReadWithoutRepeatingProviderPost()
    {
        var seed = await fixture.SeedAsync(false); using var db = fixture.Context();
        var payment = await db.Payments.SingleAsync(p => p.Id == seed.PaymentId);
        var providerOrder = payment.TransactionReference!;
        var handler = new ProviderHandler { FailProvider = true, ResponseBody = CaptureJson(providerOrder, seed.OrderId) };
        var controller = Controller(db, handler, Principal(Guid.NewGuid(), "admin"));
        Assert.Equal(503, Assert.IsType<ObjectResult>(await controller.CapturePayPalOrder(new CapturePayPalOrderDto { OrderId = seed.OrderId, PayPalOrderId = providerOrder }, None)).StatusCode);
        var operation = await db.Set<PaymentOperation>().SingleAsync(o => o.PaymentId == seed.PaymentId);
        handler.FailProvider = false;
        Assert.IsType<OkObjectResult>(await controller.Reconcile(operation.Id, None));
        Assert.Equal(1, handler.ProviderPosts); Assert.Equal(1, handler.ProviderGets);
        Assert.Equal("paid", (await db.Payments.SingleAsync(p => p.Id == seed.PaymentId)).PaymentStatus);
    }


    [Fact]
    public async Task ProviderSuccessThenLocalFailureRollsBackAndReconcilesExactlyOnce()
    {
        var seed = await fixture.SeedAsync(); using var db = fixture.Context();
        var payment = await db.Payments.SingleAsync(p => p.Id == seed.PaymentId);
        var handler = new ProviderHandler { ResponseBody = RefundJson("COMPLETED", "10.00", payment.TransactionReference!) };
        var controller = Controller(db, handler, Principal(Guid.NewGuid(), "admin"));
        // Fixed DDL identifiers in a serialized disposable-DB collection; no user values enter SQL.
        await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION alpha_test_fail_audit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'synthetic local failure'; END; $$");
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER alpha_test_fail_trigger BEFORE INSERT ON audit_logs FOR EACH ROW EXECUTE FUNCTION alpha_test_fail_audit()");
        try
        {
            var result = await controller.Refund(new RefundPaymentDto { OrderId = seed.OrderId, Amount = 10m, IdempotencyKey = "rollback-refund" }, None);
            Assert.Equal(503, Assert.IsType<ObjectResult>(result).StatusCode);
            using var verify = fixture.Context();
            Assert.Equal(0m, (await verify.Payments.SingleAsync(p => p.Id == seed.PaymentId)).RefundedAmount);
            var operation = await verify.Set<PaymentOperation>().SingleAsync(o => o.PaymentId == seed.PaymentId);
            Assert.Equal("unknown", operation.State); Assert.NotNull(operation.ProviderReference);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER alpha_test_fail_trigger ON audit_logs");
            await db.Database.ExecuteSqlRawAsync("DROP FUNCTION alpha_test_fail_audit()");
        }
        var pending = await db.Set<PaymentOperation>().SingleAsync(o => o.PaymentId == seed.PaymentId);
        Assert.IsType<OkObjectResult>(await controller.Reconcile(pending.Id, None));
        Assert.IsType<OkObjectResult>(await controller.Reconcile(pending.Id, None));
        Assert.Equal(1, handler.ProviderPosts); Assert.Equal(1, handler.ProviderGets);
        using var final = fixture.Context();
        Assert.Equal(10m, (await final.Payments.SingleAsync(p => p.Id == seed.PaymentId)).RefundedAmount);
        Assert.Single(await final.AuditLogs.Where(a => a.OrderId == seed.OrderId).ToListAsync());
    }


    [Fact]
    public async Task ConcurrentCaptureRequestsOnlyChargeAndCompleteOnce()
    {
        var seed = await fixture.SeedAsync(false); using var initial = fixture.Context();
        var providerOrder = (await initial.Payments.SingleAsync(p => p.Id == seed.PaymentId)).TransactionReference!;
        var handler = new ProviderHandler { ResponseBody = CaptureJson(providerOrder, seed.OrderId) };
        async Task<IActionResult> Capture()
        {
            using var db = fixture.Context();
            return await Controller(db, handler, Principal(seed.CustomerId, "customer")).CapturePayPalOrder(
                new CapturePayPalOrderDto { OrderId = seed.OrderId, PayPalOrderId = providerOrder }, None);
        }
        var results = await Task.WhenAll(Capture(), Capture());
        Assert.Contains(results, result => result is OkObjectResult);
        Assert.All(results, result => Assert.True(result is OkObjectResult or ConflictObjectResult));
        Assert.Equal(1, handler.ProviderPosts);
        using var verify = fixture.Context();
        Assert.Single(await verify.AuditLogs.Where(a => a.OrderId == seed.OrderId).ToListAsync());
        Assert.Equal(2, await verify.StatusHistory.CountAsync(h => h.OrderId == seed.OrderId));
    }

    [Fact]
    public async Task LegacyUnassociatedPaymentIsReadableButCannotReserveProviderOperation()
    {
        using var db = fixture.Context(); var payment = new Payment { Id = Guid.NewGuid(), OrderId = null, Amount = 10m, Currency = "USD", PaymentStatus = "paid" };
        db.Add(payment); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PaymentOperationService(db).ReserveAsync(payment.Id,
            "refund", "orphan-payment", 10m, "USD", "CAPTURE", None));
        Assert.Empty(await db.Set<PaymentOperation>().Where(o => o.PaymentId == payment.Id).ToListAsync());
    }

    [Fact]
    public async Task FullRefundReplayDoesNotRepeatFinancialOrReferralEffects()
    {
        var seed = await fixture.SeedAsync(); using var db = fixture.Context();
        var payment = await db.Payments.SingleAsync(p => p.Id == seed.PaymentId);
        var handler = new ProviderHandler { ResponseBody = RefundJson("COMPLETED", "100.00", payment.TransactionReference!) };
        var controller = Controller(db, handler, Principal(Guid.NewGuid(), "admin"));
        var dto = new RefundPaymentDto { OrderId = seed.OrderId, Amount = 100m, IdempotencyKey = "full-refund" };
        Assert.IsType<OkObjectResult>(await controller.Refund(dto, None));
        Assert.IsType<OkObjectResult>(await controller.Refund(dto, None));
        Assert.Equal(1, handler.ProviderPosts);
        using var verify = fixture.Context();
        var result = await verify.Payments.SingleAsync(p => p.Id == seed.PaymentId);
        Assert.Equal(100m, result.RefundedAmount); Assert.Equal("refunded", result.PaymentStatus);
        Assert.Equal("refunded", (await verify.Orders.SingleAsync(o => o.Id == seed.OrderId)).Status);
        Assert.Single(await verify.AuditLogs.Where(a => a.OrderId == seed.OrderId).ToListAsync());
    }


    [Fact]
    public async Task ReorderedSupplierAndDriverRolesOnlyReturnOwnAssignments()
    {
        var supplierOrder = await fixture.SeedAsync(); var driverOrder = await fixture.SeedAsync();
        using var db = fixture.Context();
        var supplier = new Supplier { Id = Guid.NewGuid(), UserId = supplierOrder.CustomerId, Name = "Synthetic supplier", CreatedAt = DateTime.UtcNow };
        var driver = new Driver { Id = Guid.NewGuid(), UserId = supplierOrder.CustomerId, FullName = "Synthetic driver" };
        db.AddRange(supplier, driver); await db.SaveChangesAsync();
        await db.Orders.Where(o => o.Id == supplierOrder.OrderId).ExecuteUpdateAsync(s => s.SetProperty(o => o.SupplierId, supplier.Id));
        await db.Orders.Where(o => o.Id == driverOrder.OrderId).ExecuteUpdateAsync(s => s.SetProperty(o => o.DriverId, driver.Id));
        foreach (var roles in new[] { new[] { "supplier", "driver" }, new[] { "driver", "supplier" } })
        {
            var user = Principal(supplierOrder.CustomerId, roles);
            var controller = new OrdersController(db, null!, null!, null!, null!, null!, null!, null!)
                { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } } };
            var result = Assert.IsType<OkObjectResult>(await controller.GetMyAssignedOrders(None));
            Assert.Equal(2, Assert.IsAssignableFrom<System.Collections.IEnumerable>(result.Value).Cast<object>().Count());
            var financial = new FinancialsController(db, null!, null!)
                { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } } };
            Assert.IsType<ForbidResult>(await financial.GetSupplierEarnings(Guid.NewGuid(), None));
        }
    }

    private static string RefundJson(string status, string amount, string capture) => JsonSerializer.Serialize(new {
        id = "REFUND-" + Guid.NewGuid().ToString("N"), status, amount = new { value = amount, currency_code = "USD" },
        links = new[] { new { rel = "up", href = "https://provider.invalid/v2/payments/captures/" + capture } } });
    private static string CaptureJson(string providerOrder, Guid order) => JsonSerializer.Serialize(new {
        id = providerOrder, status = "COMPLETED", purchase_units = new[] { new { reference_id = order.ToString(), custom_id = order.ToString(),
            payments = new { captures = new[] { new { id = "CAPTURE-" + order.ToString("N"), status = "COMPLETED", amount = new { value = "100.00", currency_code = "USD" } } } } } } });

    private sealed class ProviderHandler : HttpMessageHandler
    {
        public string ResponseBody = "{}"; public bool FailProvider; public int ProviderPosts, ProviderGets;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.Contains("oauth2"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"synthetic\"}") });
            if (request.Method == HttpMethod.Post) { Interlocked.Increment(ref ProviderPosts); if (FailProvider) throw new HttpRequestException("Synthetic unknown outcome"); }
            else ProviderGets++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ResponseBody) });
        }
    }
}
