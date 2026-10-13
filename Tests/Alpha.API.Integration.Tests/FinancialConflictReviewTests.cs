using Alpha.API.DTOs;
using Alpha.API.Models;
using Alpha.API.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;
using Xunit;
namespace Alpha.API.Integration.Tests;

[Collection("Disposable PostgreSQL")]
public sealed class FinancialConflictReviewTests(DatabaseFixture fixture)
{
    private async Task<(Guid Payment, Guid Order, Guid First, Guid Second)> Seed()
    {
        var seed = await fixture.SeedAsync(); using var db = fixture.Context();
        var first = new User { Id = Guid.NewGuid(), FullName = "Synthetic first reviewer", Email = Guid.NewGuid() + "@example.invalid", Role = "admin", IsActive = true };
        var second = new User { Id = Guid.NewGuid(), FullName = "Synthetic second reviewer", Email = Guid.NewGuid() + "@example.invalid", Role = "admin", IsActive = true };
        db.AddRange(first, second); await db.SaveChangesAsync();
        await db.Payments.Where(x => x.Id == seed.PaymentId).ExecuteUpdateAsync(x => x.SetProperty(p => p.RefundEvidenceConflict, true));
        return (seed.PaymentId, seed.OrderId, first.Id, second.Id);
    }
    private static FinancialConflictReviewDto Request(string key = "synthetic-review") => new() { IdempotencyKey = key, Evidence = "Reviewed synthetic provider evidence reference CASE-001" };
    private static FinancialConflictApprovalDto Approval() => new() { Evidence = "Independently verified synthetic evidence CASE-001" };

    [Fact]
    public async Task TwoAdministratorsRecordEvidenceWithoutReleasingHoldOrChangingMoney()
    {
        var seed = await Seed(); using var db = fixture.Context(); var service = new FinancialConflictReviewService(db);
        var review = await service.SubmitAsync(seed.Payment, Request(), seed.First, default);
        var approval = await service.ApproveAsync(review.Id, Approval(), seed.Second, default);
        Assert.Equal(seed.Second, approval.ReviewerId);
        var status = JsonSerializer.Serialize(await service.StatusAsync(review.Id, default));
        Assert.Contains("two_attestations_recorded", status); Assert.Contains("\"holdReleased\":false", status);
        var payment = await db.Payments.AsNoTracking().SingleAsync(x => x.Id == seed.Payment);
        Assert.True(payment.RefundEvidenceConflict); Assert.Equal(0m, payment.RefundedAmount);
        Assert.True(await RefundConflictGuard.HasAsync(db, seed.Order, default));
        Assert.Equal(2, await db.AuditLogs.CountAsync(x => x.OrderId == seed.Order && x.Action.StartsWith("Financial conflict")));
    }

    [Fact]
    public async Task ReplaysAreStableButEvidenceOrReviewerCannotBeReassigned()
    {
        var seed = await Seed(); using var db = fixture.Context(); var service = new FinancialConflictReviewService(db);
        var review = await service.SubmitAsync(seed.Payment, Request(), seed.First, default);
        Assert.Equal(review.Id, (await service.SubmitAsync(seed.Payment, Request(), seed.First, default)).Id);
        var changed = Request(); changed.Evidence = "Different evidence";
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAsync(seed.Payment, changed, seed.First, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAsync(seed.Payment, Request(), seed.Second, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(review.Id, Approval(), seed.First, default));
        var approval = await service.ApproveAsync(review.Id, Approval(), seed.Second, default);
        Assert.Equal(approval.Id, (await service.ApproveAsync(review.Id, Approval(), seed.Second, default)).Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(review.Id, new() { Evidence = "Changed approval" }, seed.Second, default));
    }

    [Fact]
    public async Task ChangedEvidenceOnAnotherOrderPaymentInvalidatesReviewAndRecordedApproval()
    {
        var seed = await Seed(); using var db = fixture.Context(); var service = new FinancialConflictReviewService(db);
        var review = await service.SubmitAsync(seed.Payment, Request(), seed.First, default);
        await service.ApproveAsync(review.Id, Approval(), seed.Second, default);
        db.Payments.Add(new Payment { Id = Guid.NewGuid(), OrderId = seed.Order, Amount = 1, Currency = "USD", PaymentStatus = "paid" });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(review.Id, Approval(), seed.Second, default));
        Assert.Contains("evidence_changed", JsonSerializer.Serialize(await service.StatusAsync(review.Id, default)));
        var next = await service.SubmitAsync(seed.Payment, Request("fresh-evidence"), seed.First, default);
        Assert.NotEqual(review.SnapshotHash, next.SnapshotHash);
    }

    [Fact]
    public async Task ConcurrentSecondApprovalsHaveOneEffect()
    {
        var seed = await Seed(); Guid reviewId;
        using (var db = fixture.Context()) reviewId = (await new FinancialConflictReviewService(db).SubmitAsync(seed.Payment, Request(), seed.First, default)).Id;
        async Task<Guid> Approve() { using var db = fixture.Context(); return (await new FinancialConflictReviewService(db).ApproveAsync(reviewId, Approval(), seed.Second, default)).Id; }
        var ids = await Task.WhenAll(Approve(), Approve()); Assert.Equal(ids[0], ids[1]);
        using var check = fixture.Context(); Assert.Equal(1, await check.Set<FinancialConflictApproval>().CountAsync(x => x.ReviewId == reviewId));
        Assert.Equal(1, await check.AuditLogs.CountAsync(x => x.Action == "Financial conflict second attestation " + reviewId));
    }

    [Fact]
    public async Task CompetingIndependentReviewersCannotReplaceTheWinner()
    {
        var seed = await Seed(); Guid reviewId; var thirdId = Guid.NewGuid();
        using (var db = fixture.Context())
        {
            db.Users.Add(new User { Id = thirdId, FullName = "Synthetic third reviewer", Email = Guid.NewGuid() + "@example.invalid", Role = "admin", IsActive = true });
            await db.SaveChangesAsync();
            reviewId = (await new FinancialConflictReviewService(db).SubmitAsync(seed.Payment, Request(), seed.First, default)).Id;
        }
        async Task<bool> TryApprove(Guid actor)
        {
            using var db = fixture.Context();
            try { await new FinancialConflictReviewService(db).ApproveAsync(reviewId, Approval(), actor, default); return true; }
            catch (InvalidOperationException) { return false; }
        }
        var results = await Task.WhenAll(TryApprove(seed.Second), TryApprove(thirdId));
        Assert.Single(results.Where(x => x));
        using var check = fixture.Context(); Assert.Equal(1, await check.Set<FinancialConflictApproval>().CountAsync(x => x.ReviewId == reviewId));
    }

    [Fact]
    public async Task DemotedFirstReviewerPreventsSecondAttestation()
    {
        var seed = await Seed(); using var db = fixture.Context(); var service = new FinancialConflictReviewService(db);
        var review = await service.SubmitAsync(seed.Payment, Request(), seed.First, default);
        await db.Users.Where(x => x.Id == seed.First).ExecuteUpdateAsync(x => x.SetProperty(u => u.IsActive, false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ApproveAsync(review.Id, Approval(), seed.Second, default));
        Assert.False(await db.Set<FinancialConflictApproval>().AnyAsync(x => x.ReviewId == review.Id));
    }

    [Fact]
    public async Task InactiveOrWrongRoleCannotAttestAndUnconflictedPaymentCannotBeReviewed()
    {
        var seed = await Seed(); using var db = fixture.Context(); var service = new FinancialConflictReviewService(db);
        await db.Users.Where(x => x.Id == seed.First).ExecuteUpdateAsync(x => x.SetProperty(u => u.IsActive, false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SubmitAsync(seed.Payment, Request(), seed.First, default));
        await db.Users.Where(x => x.Id == seed.Second).ExecuteUpdateAsync(x => x.SetProperty(u => u.Role, "customer"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SubmitAsync(seed.Payment, Request(), seed.Second, default));
        var other = await fixture.SeedAsync();
        await db.Users.Where(x => x.Id == seed.First).ExecuteUpdateAsync(x => x.SetProperty(u => u.IsActive, true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitAsync(other.PaymentId, Request(), seed.First, default));
    }

    [Theory]
    [InlineData("UPDATE financial_conflict_reviews SET evidence = 'changed'")]
    [InlineData("DELETE FROM financial_conflict_reviews")]
    [InlineData("TRUNCATE financial_conflict_reviews CASCADE")]
    [InlineData("UPDATE financial_conflict_approvals SET evidence = 'changed'")]
    [InlineData("DELETE FROM financial_conflict_approvals")]
    [InlineData("TRUNCATE financial_conflict_approvals CASCADE")]
    public async Task AttestationHistoryRejectsSqlMutation(string sql)
    {
        var seed = await Seed(); using var db = fixture.Context(); var service = new FinancialConflictReviewService(db);
        var review = await service.SubmitAsync(seed.Payment, Request(), seed.First, default);
        await service.ApproveAsync(review.Id, Approval(), seed.Second, default);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql)); Assert.Equal("23514", ex.SqlState);
        Assert.True(await db.Set<FinancialConflictReview>().AnyAsync(x => x.Id == review.Id));
        Assert.True(await db.Set<FinancialConflictApproval>().AnyAsync(x => x.ReviewId == review.Id));
    }

    [Fact]
    public async Task DirectSqlCannotRecordSelfApproval()
    {
        var seed = await Seed(); using var db = fixture.Context();
        var review = await new FinancialConflictReviewService(db).SubmitAsync(seed.Payment, Request(), seed.First, default);
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO financial_conflict_approvals (id, review_id, reviewer_id, evidence, created_at) VALUES ({Guid.NewGuid()}, {review.Id}, {seed.First}, {'x'.ToString()}, {DateTime.UtcNow})"));
        Assert.Equal("23514", error.SqlState);
    }
}
