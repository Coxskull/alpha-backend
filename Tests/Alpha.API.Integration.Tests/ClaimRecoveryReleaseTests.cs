using Alpha.API.Models;
using Alpha.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace Alpha.API.Integration.Tests;
public sealed partial class RefundRecoveryTests
{
    private async Task<(Guid Order, Guid Payment, Guid Claim, Guid Payee, Guid First, Guid Second, Guid Review)> ReviewedClaim(bool secondApproval = true)
    {
        var source = await Settlement(); var operation = await Refund(source.Payment, 30); await Allocate(operation, Plan(source.Queue, 30));
        using var db = fixture.Context();
        var first = new User { Id = Guid.NewGuid(), FullName = "Synthetic reviewer", Email = Guid.NewGuid() + "@example.invalid", Role = "admin", IsActive = true };
        var second = new User { Id = Guid.NewGuid(), FullName = "Synthetic independent reviewer", Email = Guid.NewGuid() + "@example.invalid", Role = "admin", IsActive = true };
        db.AddRange(first, second); await db.SaveChangesAsync();
        await db.Payments.Where(p => p.Id == source.Payment).ExecuteUpdateAsync(s => s.SetProperty(p => p.RefundEvidenceConflict, true));
        await db.OrderFinancials.Where(f => f.OrderId == source.Order).ExecuteUpdateAsync(s => s.SetProperty(f => f.FinancialStatus, RefundConflictGuard.Status));
        var reviews = new FinancialConflictReviewService(db);
        var review = await reviews.SubmitAsync(source.Payment, new() { IdempotencyKey = "release-review", Evidence = "Synthetic attributable source evidence" }, first.Id, Ct);
        if (secondApproval) await reviews.ApproveAsync(review.Id, new() { Evidence = "Independent synthetic verification" }, second.Id, Ct);
        var claim = await db.Set<RefundAllocation>().SingleAsync(a => a.OperationId == operation && a.FutureRecovery);
        return (source.Order, source.Payment, claim.Id, source.Payee, first.Id, second.Id, review.Id);
    }
    private async Task<object> Release(Guid review, Guid claim, Guid actor)
    {
        using var db = fixture.Context();
        return await new FinancialRecoveryReleaseService(db).ReleaseAsync(review, new() { AllocationId = claim, Evidence = "Claim-only release; original-order holds retained" }, actor, Ct);
    }

    [Fact]
    public async Task ClaimReleaseAllowsPartialAndZeroNetRecoveryButKeepsOriginalOrderHeld()
    {
        var seed = await ReviewedClaim(); await Release(seed.Review, seed.Claim, seed.Second);
        var first = await Settlement(amount: 20, paid: false, payee: seed.Payee); Assert.Equal(0m, await Pay(first.Queue, 0));
        var next = await Settlement(amount: 30, paid: false, payee: seed.Payee); Assert.Equal(20m, await Pay(next.Queue, 20));
        using var db = fixture.Context();
        Assert.True(await RefundConflictGuard.HasAsync(db, seed.Order, Ct));
        Assert.Equal(RefundConflictGuard.Status, (await db.OrderFinancials.SingleAsync(f => f.OrderId == seed.Order)).FinancialStatus);
        Assert.Equal(30m, (await db.Set<RefundAllocation>().SingleAsync(a => a.Id == seed.Claim)).RecoveredAmount);
        Assert.Equal(80m, (await db.SupplierPayouts.SingleAsync(p => p.OrderId == seed.Order)).Amount);
        var financial = await db.OrderFinancials.SingleAsync(f => f.OrderId == seed.Order);
        var queue = new SettlementQueue { Id = Guid.NewGuid(), OrderFinancialId = financial.Id, PayeeType = "supplier", PayeeId = seed.Payee, Amount = 10, Status = "ready_for_payout" };
        db.Add(queue); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new PayoutAccountingService(db).SettlementAsync(queue.Id,
            new() { PaidAmount = 10, Currency = "USD", PayoutReference = "synthetic-original-held" }, Admin, null, Ct));
        var refund = await Refund(seed.Payment, 5); await Assert.ThrowsAsync<InvalidOperationException>(() => Allocate(refund, Plan(queue.Id, 5)));
    }

    [Fact]
    public async Task NewConflictingEvidenceInvalidatesReleaseUntilFreshTwoPersonReview()
    {
        var seed = await ReviewedClaim(); await Release(seed.Review, seed.Claim, seed.First);
        var future = await Settlement(amount: 50, paid: false, payee: seed.Payee);
        using (var db = fixture.Context())
        {
            db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrderId = seed.Order, Action = "Stripe refund evidence conflict held for manual review", PerformedBy = "stripe", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => Pay(future.Queue, 20));
        using (var db = fixture.Context())
        {
            Assert.Equal(0m, (await db.Set<RefundAllocation>().SingleAsync(a => a.Id == seed.Claim)).RecoveredAmount);
            Assert.False(await db.SupplierPayouts.AnyAsync(p => p.OrderId == future.Order));
            var reviews = new FinancialConflictReviewService(db);
            var fresh = await reviews.SubmitAsync(seed.Payment, new() { IdempotencyKey = "fresh-after-conflict", Evidence = "Changed evidence re-reviewed" }, seed.First, Ct);
            await reviews.ApproveAsync(fresh.Id, new() { Evidence = "Independent changed evidence review" }, seed.Second, Ct);
            await new FinancialRecoveryReleaseService(db).ReleaseAsync(fresh.Id, new() { AllocationId = seed.Claim, Evidence = "Fresh claim-only authorization" }, seed.Second, Ct);
        }
        Assert.Equal(20m, await Pay(future.Queue, 20));
    }

    [Fact]
    public async Task OneAttestationCrossOrderClaimAndChangedEvidenceCannotBeReleased()
    {
        var seed = await ReviewedClaim(secondApproval: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Release(seed.Review, seed.Claim, seed.First));
        using (var db = fixture.Context()) await new FinancialConflictReviewService(db).ApproveAsync(seed.Review, new() { Evidence = "Independent review" }, seed.Second, Ct);
        var other = await ReviewedClaim(); await Assert.ThrowsAsync<InvalidOperationException>(() => Release(seed.Review, other.Claim, seed.First));
        using (var db = fixture.Context()) await db.Payments.Where(p => p.Id == seed.Payment).ExecuteUpdateAsync(s => s.SetProperty(p => p.RefundStatus, "changed"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Release(seed.Review, seed.Claim, seed.First));
    }

    [Fact]
    public async Task ConcurrentReleaseRetriesHaveOneRecordAndChangedReplayFails()
    {
        var seed = await ReviewedClaim(); await Task.WhenAll(Release(seed.Review, seed.Claim, seed.Second), Release(seed.Review, seed.Claim, seed.Second));
        using var db = fixture.Context(); Assert.Equal(1, await db.Set<FinancialRecoveryRelease>().CountAsync(r => r.ReviewId == seed.Review && r.AllocationId == seed.Claim));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new FinancialRecoveryReleaseService(db).ReleaseAsync(seed.Review,
            new() { AllocationId = seed.Claim, Evidence = "Changed release evidence" }, seed.Second, Ct));
    }

    [Theory]
    [InlineData("UPDATE financial_recovery_releases SET evidence = 'changed'")]
    [InlineData("DELETE FROM financial_recovery_releases")]
    [InlineData("TRUNCATE financial_recovery_releases CASCADE")]
    public async Task ClaimReleaseHistoryIsImmutable(string sql)
    {
        var seed = await ReviewedClaim(); await Release(seed.Review, seed.Claim, seed.First); using var db = fixture.Context();
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql)); Assert.Equal("23514", error.SqlState);
    }

    [Fact]
    public async Task ClaimReleaseCannotBypassMissingSourceFinancesOrAnotherUnreleasedClaim()
    {
        var seed = await ReviewedClaim(); await Release(seed.Review, seed.Claim, seed.Second);
        var other = await Settlement(payee: seed.Payee); var refund = await Refund(other.Payment, 10); await Allocate(refund, Plan(other.Queue, 10));
        using (var db = fixture.Context()) await db.Payments.Where(p => p.Id == other.Payment).ExecuteUpdateAsync(s => s.SetProperty(p => p.RefundEvidenceConflict, true));
        var future = await Settlement(amount: 50, paid: false, payee: seed.Payee); await Assert.ThrowsAsync<InvalidOperationException>(() => Pay(future.Queue, 10));
        using (var db = fixture.Context())
        {
            var financial = await db.OrderFinancials.SingleAsync(f => f.OrderId == seed.Order);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM settlement_queue WHERE order_financial_id = {0}", financial.Id);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM order_financials WHERE id = {0}", financial.Id);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => Pay(future.Queue, 10));
        using var verify = fixture.Context(); Assert.Equal(0m, (await verify.Set<RefundAllocation>().SingleAsync(a => a.Id == seed.Claim)).RecoveredAmount);
    }
    [Fact]
    public async Task ReleasedClaimStillIsolatesPayeeRoleAndCurrency()
    {
        var seed = await ReviewedClaim(); await Release(seed.Review, seed.Claim, seed.Second);
        var unrelated = await Settlement(amount: 50, paid: false); Assert.Equal(50m, await Pay(unrelated.Queue, 50));
        var driver = await Settlement(type: "driver", amount: 50, paid: false, payee: seed.Payee); Assert.Equal(50m, await Pay(driver.Queue, 50));
        var euro = await Settlement(amount: 50, paid: false, payee: seed.Payee, currency: "EUR"); Assert.Equal(50m, await Pay(euro.Queue, 50, "EUR"));
        var matching = await Settlement(amount: 50, paid: false, payee: seed.Payee); Assert.Equal(20m, await Pay(matching.Queue, 20));
    }

    [Fact]
    public async Task ConcurrentPayoutsCannotConsumeReleasedClaimTwice()
    {
        var seed = await ReviewedClaim(); await Release(seed.Review, seed.Claim, seed.First);
        var first = await Settlement(amount: 50, paid: false, payee: seed.Payee);
        var second = await Settlement(amount: 50, paid: false, payee: seed.Payee);
        async Task<bool> TryPay(Guid queue)
        {
            try { await Pay(queue, 20); return true; }
            catch (InvalidOperationException) { return false; }
        }
        var results = await Task.WhenAll(TryPay(first.Queue), TryPay(second.Queue)); Assert.Single(results, x => x);
        using var db = fixture.Context();
        Assert.Equal(30m, (await db.Set<RefundAllocation>().SingleAsync(x => x.Id == seed.Claim)).RecoveredAmount);
        Assert.Equal(30m, await db.Set<PayoutRecoveryOffset>().Where(x => x.AllocationId == seed.Claim).SumAsync(x => x.Amount));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOriginalPaidHistoryRejectsReleaseOrReholdsRecovery(bool released)
    {
        var seed = await ReviewedClaim();
        if (released) await Release(seed.Review, seed.Claim, seed.Second);
        using (var db = fixture.Context()) await db.SupplierPayouts.Where(x => x.OrderId == seed.Order).ExecuteDeleteAsync();
        if (!released) await Assert.ThrowsAsync<InvalidOperationException>(() => Release(seed.Review, seed.Claim, seed.Second));
        var future = await Settlement(amount: 50, paid: false, payee: seed.Payee);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Pay(future.Queue, 20));
    }
    [Fact]
    public async Task PendingSignedSourceRefundEventBlocksClaimReleaseAndRecovery()
    {
        var seed = await ReviewedClaim();
        // Establish Stripe identity before a fresh review so the later pending event is the only change.
        var intent = "pi_synthetic_" + Guid.NewGuid().ToString("N");
        using (var db = fixture.Context())
        {
            await db.Payments.Where(x => x.Id == seed.Payment).ExecuteUpdateAsync(s => s.SetProperty(x => x.PaymentGateway, "stripe").SetProperty(x => x.GatewayPaymentId, intent));
            var reviews = new FinancialConflictReviewService(db);
            var fresh = await reviews.SubmitAsync(seed.Payment, new() { IdempotencyKey = "stripe-source-review", Evidence = "Synthetic Stripe source reviewed" }, seed.First, Ct);
            await reviews.ApproveAsync(fresh.Id, new() { Evidence = "Independent Stripe source review" }, seed.Second, Ct);
            seed.Review = fresh.Id;
        }
        await Release(seed.Review, seed.Claim, seed.Second);
        using (var db = fixture.Context())
        {
            db.PaymentWebhookEvents.Add(new PaymentWebhookEvent { Id = Guid.NewGuid(), Gateway = "stripe", GatewayEventId = "evt_synthetic_" + Guid.NewGuid(), EventType = "charge.refunded", Processed = false,
                Payload = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new { data = new { @object = new { payment_intent = intent } } })), ReceivedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var future = await Settlement(amount: 50, paid: false, payee: seed.Payee);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Pay(future.Queue, 20));
        var replay = await Release(seed.Review, seed.Claim, seed.Second);
        Assert.False(System.Text.Json.JsonSerializer.SerializeToElement(replay).GetProperty("effective").GetBoolean());
        using var check = fixture.Context(); Assert.Equal(0m, (await check.Set<RefundAllocation>().SingleAsync(x => x.Id == seed.Claim)).RecoveredAmount);
    }
    [Fact]
    public async Task FreshAttestationsCannotOverrideMixedSourceCurrency()
    {
        var seed = await ReviewedClaim(); using var db = fixture.Context();
        await db.OrderFinancials.Where(x => x.OrderId == seed.Order).ExecuteUpdateAsync(s => s.SetProperty(x => x.Currency, "EUR"));
        var reviews = new FinancialConflictReviewService(db);
        var fresh = await reviews.SubmitAsync(seed.Payment, new() { IdempotencyKey = "mixed-currency", Evidence = "Mixed source reviewed but not repaired" }, seed.First, Ct);
        await reviews.ApproveAsync(fresh.Id, new() { Evidence = "Independent mixed source review" }, seed.Second, Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Release(fresh.Id, seed.Claim, seed.Second));
        Assert.False(await db.Set<FinancialRecoveryRelease>().AnyAsync(x => x.ReviewId == fresh.Id));
    }
}
