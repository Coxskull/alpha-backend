using Alpha.API.Models;
using Alpha.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Alpha.API.Integration.Tests;

[Collection("Disposable PostgreSQL")]
public sealed class RefundConflictPersistenceTests(DatabaseFixture fixture)
{
    [Theory]
    [InlineData("UPDATE payments SET refund_evidence_conflict = FALSE WHERE id = {0}")]
    [InlineData("UPDATE payments SET order_id = NULL WHERE id = {0}")]
    [InlineData("DELETE FROM payments WHERE id = {0}")]
    public async Task ConflictIdentityCannotBeClearedOrRemovedThroughSql(string sql)
    {
        var seed = await fixture.SeedAsync(); using var db = fixture.Context();
        await db.Payments.Where(p => p.Id == seed.PaymentId).ExecuteUpdateAsync(s => s.SetProperty(p => p.RefundEvidenceConflict, true));
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, seed.PaymentId));
        Assert.Equal("23514", error.SqlState);
        var payment = await db.Payments.SingleAsync(p => p.Id == seed.PaymentId);
        Assert.True(payment.RefundEvidenceConflict); Assert.Equal(seed.OrderId, payment.OrderId);
    }

    [Fact]
    public async Task MigrationReholdsHistoricallyClearedAuditedConflictsWithoutChangingRefundTotals()
    {
        var seed = await fixture.SeedAsync(); using var db = fixture.Context();
        var migrator = db.GetService<IMigrator>();
        try
        {
            await migrator.MigrateAsync("20261012120000_FinancialAuditIntegrity");
            db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), OrderId = seed.OrderId,
                Action = "Stripe refund evidence conflict held for manual review", PerformedBy = "stripe", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        finally { await migrator.MigrateAsync(); }
        db.ChangeTracker.Clear();
        var payment = await db.Payments.SingleAsync(p => p.Id == seed.PaymentId);
        var financial = await db.OrderFinancials.SingleAsync(f => f.OrderId == seed.OrderId);
        Assert.True(payment.RefundEvidenceConflict); Assert.Equal(0m, payment.RefundedAmount);
        Assert.Equal(RefundConflictGuard.Status, financial.FinancialStatus);
        Assert.Equal("on_hold", financial.PayoutStatus); Assert.Equal(0m, financial.RefundAmount);
    }
}
