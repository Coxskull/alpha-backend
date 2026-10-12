using Alpha.API.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Alpha.API.Integration.Tests;

[Collection("Disposable PostgreSQL")]
public sealed class AuditIntegrityTests(DatabaseFixture fixture)
{
    private async Task<RefundAllocation> Allocation()
    {
        var seed = await fixture.SeedAsync();
        using var db = fixture.Context();
        var operation = new PaymentOperation { Id = Guid.NewGuid(), PaymentId = seed.PaymentId, Kind = "refund", State = "completed",
            Amount = 10, Currency = "USD", IdempotencyKey = Guid.NewGuid().ToString(), ProviderRequestId = Guid.NewGuid().ToString("N") };
        var allocation = new RefundAllocation { OperationId = operation.Id, SourceKind = "settlement", SourceId = Guid.NewGuid(),
            PayeeType = "supplier", PayeeId = Guid.NewGuid(), Currency = "USD", Amount = 10, FutureRecovery = true, Evidence = "Synthetic original", ReviewedBy = Guid.NewGuid() };
        db.AddRange(operation, allocation); await db.SaveChangesAsync(); return allocation;
    }
    [Theory]
    [InlineData("UPDATE audit_logs SET action = 'changed' WHERE id = {0}")]
    [InlineData("DELETE FROM audit_logs WHERE id = {0}")]
    [InlineData("TRUNCATE audit_logs")]
    public async Task AuditHistoryRejectsDirectSqlMutation(string sql)
    {
        using var db = fixture.Context();
        var audit = new AuditLog { Id = Guid.NewGuid(), OrderId = Guid.NewGuid(), Action = "Synthetic original", PerformedBy = "gate", CreatedAt = DateTime.UtcNow };
        db.Add(audit); await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, audit.Id));
        Assert.Equal("23514", error.SqlState);
        Assert.Equal("Synthetic original", (await db.AuditLogs.AsNoTracking().SingleAsync(a => a.Id == audit.Id)).Action);
    }

    [Theory]
    [InlineData("UPDATE payout_recovery_offsets SET amount = 2 WHERE id = {0}")]
    [InlineData("DELETE FROM payout_recovery_offsets WHERE id = {0}")]
    [InlineData("TRUNCATE payout_recovery_offsets")]
    public async Task OffsetHistoryRejectsDirectSqlMutation(string sql)
    {
        using var db = fixture.Context();
        var allocation = await Allocation();
        var offset = new PayoutRecoveryOffset { AllocationId = allocation.Id, TargetId = Guid.NewGuid(), TargetKind = "settlement", Amount = 1 };
        db.Add(offset); await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, offset.Id));
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(1m, (await db.Set<PayoutRecoveryOffset>().AsNoTracking().SingleAsync(a => a.Id == offset.Id)).Amount);
    }

    [Theory]
    [InlineData("UPDATE refund_allocations SET evidence = 'changed' WHERE id = {0}")]
    [InlineData("UPDATE refund_allocations SET payee_id = '00000000-0000-0000-0000-000000000001' WHERE id = {0}")]
    [InlineData("UPDATE refund_allocations SET recovered_amount = 0 WHERE id = {0}")]
    [InlineData("UPDATE refund_allocations SET recovered_amount = 11 WHERE id = {0}")]
    [InlineData("DELETE FROM refund_allocations WHERE id = {0}")]
    [InlineData("TRUNCATE refund_allocations")]
    public async Task AllocationIdentityAndEvidenceAreImmutableAndRecoveryIsMonotonic(string sql)
    {
        using var db = fixture.Context();
        var allocation = await Allocation();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE refund_allocations SET recovered_amount = 3 WHERE id = {allocation.Id}");
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, allocation.Id));
        Assert.Equal("23514", error.SqlState);
        var stored = await db.Set<RefundAllocation>().AsNoTracking().SingleAsync(a => a.Id == allocation.Id);
        Assert.Equal(3m, stored.RecoveredAmount); Assert.Equal(allocation.PayeeId, stored.PayeeId); Assert.Equal(allocation.Evidence, stored.Evidence);
    }
}
