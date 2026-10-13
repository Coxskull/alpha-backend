using System.Reflection;
using System.Text.Json;
using Alpha.API.Controllers;
using Alpha.API.DTOs;
using Alpha.API.Models;
using Alpha.API.Models.Entrepreneur;
using Alpha.API.Services;
using Alpha.API.Services.Entrepreneur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace Alpha.API.Integration.Tests;

[Collection("Disposable PostgreSQL")]
public sealed partial class RefundRecoveryTests(DatabaseFixture fixture)
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly Guid Admin = Guid.NewGuid();
    private static decimal Net(object quote) => JsonSerializer.SerializeToElement(quote).GetProperty("paidAmount").GetDecimal();
    private static RefundAllocationDto Plan(Guid source, decimal amount, string kind = "settlement", decimal platform = 0)
        => new() { Evidence = "Synthetic reviewed line-item attribution", PlatformAmount = platform,
            Lines = new() { new() { SourceKind = kind, SourceId = source, Amount = amount } } };
    private async Task<(Guid Order, Guid Payment, Guid Queue, Guid Payee)> Settlement(string type = "supplier", bool paid = true, decimal amount = 80, Guid? payee = null, string currency = "USD")
    {
        var seed = await fixture.SeedAsync(); using var db = fixture.Context(); var identity = payee ?? Guid.NewGuid();
        var financial = await db.OrderFinancials.SingleAsync(f => f.OrderId == seed.OrderId);
        financial.Currency = currency; financial.FinancialStatus = "verified"; financial.PayoutStatus = paid ? "paid" : "ready_for_payout";
        var payment = await db.Payments.SingleAsync(p => p.Id == seed.PaymentId); payment.Currency = currency;
        if (type == "supplier" && !await db.Suppliers.AnyAsync(s => s.Id == identity)) db.Suppliers.Add(new Supplier { Id = identity, Name = "Synthetic supplier" });
        if (type == "driver" && !await db.Drivers.AnyAsync(d => d.Id == identity)) db.Drivers.Add(new Driver { Id = identity, FullName = "Synthetic driver" });
        var queue = new SettlementQueue { Id = Guid.NewGuid(), OrderFinancialId = financial.Id, PayeeType = type, PayeeId = identity, Amount = amount, Status = paid ? "paid" : "ready_for_payout" };
        db.SettlementQueue.Add(queue);
        if (paid && type == "supplier") db.SupplierPayouts.Add(new SupplierPayout { Id = Guid.NewGuid(), SupplierId = identity, OrderId = seed.OrderId, Amount = amount, Currency = currency, PayoutStatus = "paid" });
        if (paid && type == "driver") db.DriverPayouts.Add(new DriverPayout { Id = Guid.NewGuid(), DriverId = identity, OrderId = seed.OrderId, Amount = amount, Currency = currency, PayoutStatus = "paid" });
        await db.SaveChangesAsync(); return (seed.OrderId, seed.PaymentId, queue.Id, identity);
    }
    private async Task<Guid> Refund(Guid paymentId, decimal amount, string state = "completed")
    {
        using var db = fixture.Context(); var payment = await db.Payments.SingleAsync(p => p.Id == paymentId);
        var operation = new PaymentOperation { Id = Guid.NewGuid(), PaymentId = paymentId, Kind = "refund", Amount = amount,
            Currency = payment.Currency, State = state, IdempotencyKey = Guid.NewGuid().ToString(), ProviderRequestId = Guid.NewGuid().ToString("N"), ProviderReference = "SYNTHETIC-" + Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        db.Add(operation); payment.RefundedAmount += amount;
        var financial = await db.OrderFinancials.SingleAsync(f => f.OrderId == payment.OrderId);
        financial.RefundAmount = payment.RefundedAmount; financial.FinancialStatus = "refund_review"; financial.PayoutStatus = "on_hold";
        foreach (var queue in await db.SettlementQueue.Where(q => q.OrderFinancialId == financial.Id && q.Status != "paid").ToListAsync()) queue.Status = "on_hold";
        await db.SaveChangesAsync(); return operation.Id;
    }
    private async Task Allocate(Guid operation, RefundAllocationDto plan)
    { using var db = fixture.Context(); await new RefundAllocationService(db).AllocateAsync(operation, plan, Admin, Ct); }
    private async Task<decimal> Pay(Guid queue, decimal confirmed, string currency = "USD")
    {
        using var db = fixture.Context(); return Net(await new PayoutAccountingService(db).SettlementAsync(queue,
            new PayoutConfirmationDto { PaidAmount = confirmed, Currency = currency, PayoutReference = "synthetic-reviewed-payment" }, Admin, null, Ct));
    }

    [Theory]
    [InlineData("supplier")]
    [InlineData("driver")]
    public async Task PaidAttributionRecoversOnlyFromSamePayeesFuturePayouts(string type)
    {
        var source = await Settlement(type); var refund = await Refund(source.Payment, 30);
        await Allocate(refund, Plan(source.Queue, 30));
        var future = await Settlement(type, false, 50, source.Payee);
        using (var db = fixture.Context())
        {
            Assert.Equal(20, Net(await new PayoutAccountingService(db).SettlementAsync(future.Queue, null, Guid.Empty, null, Ct)));
            Assert.Empty(await db.Set<PayoutRecoveryOffset>().Where(o => o.TargetId == future.Queue).ToListAsync());
        }
        Assert.Equal(20, await Pay(future.Queue, 20)); Assert.Equal(20, await Pay(future.Queue, 20));
        using var verify = fixture.Context();
        var claim = await verify.Set<RefundAllocation>().SingleAsync(a => a.OperationId == refund && a.FutureRecovery);
        Assert.Equal(30, claim.RecoveredAmount); Assert.Single(await verify.Set<PayoutRecoveryOffset>().Where(o => o.AllocationId == claim.Id).ToListAsync());
        Assert.Equal(80, (await verify.SettlementQueue.SingleAsync(q => q.Id == source.Queue)).Amount);
    }

    [Fact]
    public async Task FullyOffsetPayoutIsZeroAndRemainingDebtCarriesForward()
    {
        var source = await Settlement(); var refund = await Refund(source.Payment, 70); await Allocate(refund, Plan(source.Queue, 70));
        var first = await Settlement(paid: false, amount: 20, payee: source.Payee);
        Assert.Equal(0, await Pay(first.Queue, 0));
        var second = await Settlement(paid: false, amount: 80, payee: source.Payee);
        Assert.Equal(30, await Pay(second.Queue, 30));
        using var db = fixture.Context(); Assert.Equal(70, (await db.Set<RefundAllocation>().SingleAsync(a => a.OperationId == refund && a.FutureRecovery)).RecoveredAmount);
    }

    [Fact]
    public async Task OtherPayeesRolesAndCurrenciesAreNeverDebited()
    {
        var source = await Settlement(); var refund = await Refund(source.Payment, 30); await Allocate(refund, Plan(source.Queue, 30));
        var other = await Settlement(paid: false, amount: 50); Assert.Equal(50, await Pay(other.Queue, 50));
        var euro = await Settlement(paid: false, amount: 50, payee: source.Payee, currency: "EUR"); Assert.Equal(50, await Pay(euro.Queue, 50, "EUR"));
        var driver = await Settlement("driver", false, 50, source.Payee); Assert.Equal(50, await Pay(driver.Queue, 50));
        using var db = fixture.Context(); Assert.Equal(0, (await db.Set<RefundAllocation>().SingleAsync(a => a.OperationId == refund && a.FutureRecovery)).RecoveredAmount);
    }

    [Fact]
    public async Task ConcurrentFuturePayoutsCannotRecoverTheSameClaimTwice()
    {
        var source = await Settlement(); var refund = await Refund(source.Payment, 30); await Allocate(refund, Plan(source.Queue, 30));
        var one = await Settlement(paid: false, amount: 50, payee: source.Payee); var two = await Settlement(paid: false, amount: 50, payee: source.Payee);
        async Task<bool> TryPay(Guid id)
        { try { await Pay(id, 20); return true; } catch (InvalidOperationException) { return false; } }
        Assert.Single(await Task.WhenAll(TryPay(one.Queue), TryPay(two.Queue)), result => result);
        using var db = fixture.Context(); Assert.Equal(30, await db.Set<PayoutRecoveryOffset>().Where(o => db.Set<RefundAllocation>().Any(a => a.Id == o.AllocationId && a.OperationId == refund)).SumAsync(o => o.Amount));
    }

    [Fact]
    public async Task ConcurrentRefundsFromDifferentPaymentsCannotOverAllocateSharedOrderSource()
    {
        var source = await Settlement(amount: 30); Guid secondPayment;
        using (var db = fixture.Context())
        {
            var payment = new Payment { Id = Guid.NewGuid(), OrderId = source.Order, Amount = 100, Currency = "USD", PaymentStatus = "paid" };
            secondPayment = payment.Id; db.Add(payment); await db.SaveChangesAsync();
        }
        var one = await Refund(source.Payment, 20); var two = await Refund(secondPayment, 20);
        async Task<bool> TryAllocate(Guid id)
        { try { await Allocate(id, Plan(source.Queue, 20)); return true; } catch (InvalidOperationException) { return false; } }
        Assert.Single(await Task.WhenAll(TryAllocate(one), TryAllocate(two)), value => value);
        using var verify = fixture.Context();
        Assert.Equal(20, await verify.Set<RefundAllocation>().Where(a => a.SourceId == source.Queue).SumAsync(a => a.Amount));
    }

    [Fact]
    public async Task GrossConfirmationFailsWithoutConsumingRecovery()
    {
        var source = await Settlement(); var refund = await Refund(source.Payment, 30); await Allocate(refund, Plan(source.Queue, 30));
        var future = await Settlement(paid: false, amount: 50, payee: source.Payee);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Pay(future.Queue, 50));
        using var db = fixture.Context(); Assert.Equal(0, (await db.Set<RefundAllocation>().SingleAsync(a => a.OperationId == refund && a.FutureRecovery)).RecoveredAmount);
        Assert.Equal("ready_for_payout", (await db.SettlementQueue.SingleAsync(q => q.Id == future.Queue)).Status);
    }

    [Fact]
    public async Task UnpaidAllocationAdjustsOnlyTheAttributedSourceAndKeepsExceptionsHeld()
    {
        var source = await Settlement(paid: false); var refund = await Refund(source.Payment, 30);
        await Allocate(refund, Plan(source.Queue, 20, platform: 10));
        using var db = fixture.Context(); var queue = await db.SettlementQueue.SingleAsync(q => q.Id == source.Queue);
        Assert.Equal(80, queue.Amount); Assert.Equal(20, queue.RefundAdjustment); Assert.Equal("on_hold", queue.Status);
        Assert.False(await db.Set<RefundAllocation>().AnyAsync(a => a.OperationId == refund && a.FutureRecovery));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Pay(source.Queue, 60));
    }

    [Fact]
    public async Task AllocationReplayIsIdempotentAndChangedPlanIsRejected()
    {
        var source = await Settlement(); var refund = await Refund(source.Payment, 30); var plan = Plan(source.Queue, 30);
        await Task.WhenAll(Allocate(refund, plan), Allocate(refund, plan));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Allocate(refund, Plan(source.Queue, 20, platform: 10)));
        using var db = fixture.Context(); Assert.Equal(2, await db.Set<RefundAllocation>().CountAsync(a => a.OperationId == refund));
    }

    [Fact]
    public async Task InvalidLaterAllocationLineRollsBackEarlierSourceAdjustment()
    {
        var source = await Settlement(paid: false); var refund = await Refund(source.Payment, 40);
        var plan = Plan(source.Queue, 20);
        plan.Lines.Add(new RefundAllocationLineDto { SourceKind = "settlement", SourceId = Guid.NewGuid(), Amount = 20 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => Allocate(refund, plan));
        using var db = fixture.Context();
        Assert.Equal(0, (await db.SettlementQueue.SingleAsync(q => q.Id == source.Queue)).RefundAdjustment);
        Assert.Null((await db.Set<PaymentOperation>().SingleAsync(o => o.Id == refund)).AllocationFingerprint);
        Assert.False(await db.Set<RefundAllocation>().AnyAsync(a => a.OperationId == refund));
    }

    [Fact]
    public async Task AmbiguousHistoricalPaidSourceRemainsUnderManualReview()
    {
        var source = await Settlement(); var refund = await Refund(source.Payment, 30);
        using (var db = fixture.Context()) await db.SupplierPayouts.Where(p => p.OrderId == source.Order).ExecuteDeleteAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Allocate(refund, Plan(source.Queue, 30)));
    }

    [Theory]
    [InlineData("unknown", 30)]
    [InlineData("completed", 31)]
    [InlineData("completed", 80.01)]
    public async Task UnconfirmedUnbalancedOrExcessiveAttributionIsRejected(string state, decimal amount)
    {
        var source = await Settlement(); var refund = await Refund(source.Payment, 30, state);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Allocate(refund, Plan(source.Queue, amount)));
        using var db = fixture.Context(); Assert.False(await db.Set<RefundAllocation>().AnyAsync(a => a.OperationId == refund));
    }

    [Fact]
    public async Task CrossOrderSourcesAndOverAllocationAcrossRefundsAreRejected()
    {
        var source = await Settlement(amount: 40); var other = await Settlement(); var refund = await Refund(source.Payment, 30);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Allocate(refund, Plan(other.Queue, 30)));
        await Allocate(refund, Plan(source.Queue, 30));
        var second = await Refund(source.Payment, 20);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Allocate(second, Plan(source.Queue, 20)));
    }

    private async Task<(Guid Order, Guid Payment, Guid Earning, Guid Payee)> Earning(bool paid, Guid? payee = null, decimal amount = 40)
    {
        var seed = await fixture.SeedAsync(); using var db = fixture.Context(); var financial = await db.OrderFinancials.SingleAsync(f => f.OrderId == seed.OrderId);
        financial.FinancialStatus = "verified"; financial.PayoutStatus = "paid";
        var earning = new EntrepreneurEarning { Id = Guid.NewGuid(), EntrepreneurUserId = payee ?? seed.CustomerId, OrderId = seed.OrderId,
            PaymentId = seed.PaymentId, EntrepreneurEarningsAmount = amount, Currency = "USD", EarningStatus = paid ? "PAID" : "APPROVED" };
        if (paid)
        {
            var batch = new EntrepreneurPayoutBatch { Id = Guid.NewGuid(), Currency = "USD", TotalAmount = amount, EarningCount = 1, Status = "PAID" };
            db.Add(batch); earning.PayoutBatchId = batch.Id; earning.PayoutDate = DateTime.UtcNow;
        }
        db.Add(earning); await db.SaveChangesAsync(); return (seed.OrderId, seed.PaymentId, earning.Id, earning.EntrepreneurUserId);
    }

    [Fact]
    public async Task PaidEntrepreneurAttributionOffsetsFutureNetBatchWithoutChangingOriginalPaidRecord()
    {
        var source = await Earning(true); var refund = await Refund(source.Payment, 15); await Allocate(refund, Plan(source.Earning, 15, "earning"));
        var future = await Earning(false, source.Payee);
        using (var db = fixture.Context()) Assert.Equal(25, Net(await new EntrepreneurPayoutService(db).QuoteAsync(future.Earning, Ct)));
        using (var db = fixture.Context()) Assert.Equal(25, Net(await new EntrepreneurPayoutService(db).MarkPaidAsync(future.Earning, "synthetic", Admin, 25, "USD", Ct)));
        using var verify = fixture.Context(); Assert.Equal("PAID", (await verify.EntrepreneurEarnings.SingleAsync(e => e.Id == source.Earning)).EarningStatus);
        Assert.Equal(15, (await verify.Set<RefundAllocation>().SingleAsync(a => a.OperationId == refund && a.FutureRecovery)).RecoveredAmount);
    }

    [Fact]
    public async Task UnpaidEntrepreneurAdjustmentPreservesGrossAndCreatesNoRecoveryDebt()
    {
        var source = await Earning(false); var refund = await Refund(source.Payment, 15); await Allocate(refund, Plan(source.Earning, 15, "earning"));
        using var db = fixture.Context(); var earning = await db.EntrepreneurEarnings.SingleAsync(e => e.Id == source.Earning);
        Assert.Equal(40, earning.EntrepreneurEarningsAmount); Assert.Equal(-15, earning.RefundAdjustment);
        Assert.False(await db.Set<RefundAllocation>().AnyAsync(a => a.OperationId == refund && a.FutureRecovery));
    }

    [Fact]
    public async Task PayoutCommitFailureRollsBackClaimOffsetsAndPayoutRecord()
    {
        var source = await Settlement(); var refund = await Refund(source.Payment, 30); await Allocate(refund, Plan(source.Queue, 30));
        var future = await Settlement(paid: false, amount: 50, payee: source.Payee);
        using var db = fixture.Context();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION recovery_review_failure() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'synthetic payout failure'; END $$;
            CREATE TRIGGER recovery_review_failure BEFORE INSERT ON supplier_payouts
            FOR EACH ROW EXECUTE FUNCTION recovery_review_failure();
            """);
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => Pay(future.Queue, 20));
            Assert.Equal(0, (await db.Set<RefundAllocation>().SingleAsync(a => a.OperationId == refund && a.FutureRecovery)).RecoveredAmount);
            Assert.False(await db.Set<PayoutRecoveryOffset>().AnyAsync(o => o.TargetId == future.Queue));
            Assert.Equal("ready_for_payout", (await db.SettlementQueue.SingleAsync(q => q.Id == future.Queue)).Status);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER recovery_review_failure ON supplier_payouts; DROP FUNCTION recovery_review_failure();"); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingSourceFinancialBlocksRecoveryEvenIfConflictDisplayRowIsGone(bool conflict)
    {
        var source = await Settlement(); var operation = await Refund(source.Payment, 30); await Allocate(operation, Plan(source.Queue, 30));
        using (var db = fixture.Context())
        {
            await db.Payments.Where(p => p.Id == source.Payment).ExecuteUpdateAsync(s => s.SetProperty(p => p.RefundEvidenceConflict, conflict));
            await db.OrderFinancials.Where(f => f.OrderId == source.Order).ExecuteDeleteAsync();
        }
        var future = await Settlement(paid: false, amount: 50, payee: source.Payee);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Pay(future.Queue, 20));
        using var verify = fixture.Context();
        Assert.Equal(0m, (await verify.Set<RefundAllocation>().SingleAsync(a => a.OperationId == operation && a.FutureRecovery)).RecoveredAmount);
        Assert.False(await verify.Set<PayoutRecoveryOffset>().AnyAsync(o => o.TargetId == future.Queue));
        Assert.False(await verify.SupplierPayouts.AnyAsync(p => p.OrderId == future.Order));
    }

    [Fact]
    public async Task UnassociatedRecoverySourceRequiresManualReviewWithoutConsumingDebt()
    {
        var source = await Settlement(); var operation = await Refund(source.Payment, 30); await Allocate(operation, Plan(source.Queue, 30));
        using (var db = fixture.Context())
            await db.Payments.Where(p => p.Id == source.Payment).ExecuteUpdateAsync(s => s.SetProperty(p => p.OrderId, (Guid?)null));
        var future = await Settlement(paid: false, amount: 50, payee: source.Payee);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Pay(future.Queue, 20));
        using var verify = fixture.Context();
        Assert.Equal(0m, (await verify.Set<RefundAllocation>().SingleAsync(a => a.OperationId == operation && a.FutureRecovery)).RecoveredAmount);
        Assert.False(await verify.Set<PayoutRecoveryOffset>().AnyAsync(o => o.TargetId == future.Queue));
    }

    [Fact]
    public async Task DurableConflictBlocksRecoveryAndAllocationAfterDisplayStatusChangesAndLaterReservations()
    {
        var source = await Settlement(); var operation = await Refund(source.Payment, 30); await Allocate(operation, Plan(source.Queue, 30));
        using (var db = fixture.Context())
        {
            await db.Payments.Where(p => p.Id == source.Payment).ExecuteUpdateAsync(s => s.SetProperty(p => p.RefundEvidenceConflict, true));
            await db.OrderFinancials.Where(f => f.OrderId == source.Order).ExecuteUpdateAsync(s => s.SetProperty(f => f.FinancialStatus, "needs_review"));
        }
        var future = await Settlement(paid: false, amount: 50, payee: source.Payee);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Pay(future.Queue, 20));
        var next = await Refund(source.Payment, 5);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Allocate(next, Plan(source.Queue, 5)));
        using var context = fixture.Context(); var payment = await context.Payments.SingleAsync(p => p.Id == source.Payment);
        await new PaymentOperationService(context).ReserveAsync(payment.Id, "refund", Guid.NewGuid().ToString(), 5, "USD", payment.TransactionReference!, Ct);
        Assert.Equal(RefundConflictGuard.Status, (await context.OrderFinancials.SingleAsync(f => f.OrderId == source.Order)).FinancialStatus);
        Assert.Equal(0m, (await context.Set<RefundAllocation>().SingleAsync(a => a.OperationId == operation && a.FutureRecovery)).RecoveredAmount);
        Assert.False(await context.Set<PayoutRecoveryOffset>().AnyAsync(o => o.TargetId == future.Queue));
    }

    [Fact]
    public async Task ConflictingSourceEvidenceBlocksAllocationAndFutureRecovery()
    {
        var source = await Settlement(); var original = await Refund(source.Payment, 30); await Allocate(original, Plan(source.Queue, 20, platform: 10));
        var next = await Refund(source.Payment, 20);
        using (var db = fixture.Context()) await db.OrderFinancials.Where(f => f.OrderId == source.Order).ExecuteUpdateAsync(s => s.SetProperty(f => f.FinancialStatus, "refund_evidence_conflict"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Allocate(next, Plan(source.Queue, 20)));
        var future = await Settlement(paid: false, amount: 50, payee: source.Payee);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Pay(future.Queue, 30));
        using var verify = fixture.Context(); Assert.Equal(0, (await verify.Set<Alpha.API.Models.RefundAllocation>().SingleAsync(a => a.OperationId == original && a.FutureRecovery)).RecoveredAmount);
    }

    [Fact]
    public async Task StripeCumulativeRefundDoesNotProportionallyClawBackEntrepreneurEarnings()
    {
        var source = await Earning(true); using var db = fixture.Context(); var payment = await db.Payments.SingleAsync(p => p.Id == source.Payment);
        payment.GatewayPaymentId = "synthetic-stripe-" + source.Payment; payment.PaymentGateway = "stripe"; await db.SaveChangesAsync();
        var controller = new StripeWebhookController(db, null!, new ConfigurationBuilder().Build(), NullLogger<StripeWebhookController>.Instance, new EntrepreneurAdjustmentService(db));
        var stripeEvent = new Stripe.Event { Data = new Stripe.EventData { Object = new Stripe.Charge { Id = "synthetic-charge", PaymentIntentId = payment.GatewayPaymentId, Amount = 10000, AmountRefunded = 3000, Currency = "usd" } } };
        var method = typeof(StripeWebhookController).GetMethod("ProcessChargeRefunded", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(controller, new object[] { stripeEvent, "{}", Ct })!;
        using var verify = fixture.Context(); var earning = await verify.EntrepreneurEarnings.SingleAsync(e => e.Id == source.Earning);
        Assert.Equal(0, earning.RefundAdjustment); Assert.Equal("PAID", earning.EarningStatus);
        Assert.Equal("on_hold", (await verify.OrderFinancials.SingleAsync(f => f.OrderId == source.Order)).PayoutStatus);
    }
}
