using Alpha.API.Data;
using Alpha.API.Models.Entrepreneur;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Alpha.API.Integration.Tests;

[Collection("Disposable PostgreSQL")]
public sealed class AlphaMappingCompatibilityTests(DatabaseFixture fixture)
{
    private static AppDbContext Context(string schema)
    {
        // Synthetic loopback connection only. A private schema isolates each compatibility fixture.
        var connection = new NpgsqlConnectionStringBuilder("Host=127.0.0.1;Port=5432;Database=alpha_integration_ci;Username=alpha_ci;Password=disposable-ci-only;SSL Mode=Disable") { SearchPath = schema };
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connection.ConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", schema)).Options);
    }

    private static Task<int> SchemaAsync(AppDbContext db, string schema, bool create)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(schema, "^alpha_mapping_[a-f0-9]{32}$"))
            throw new ArgumentException("Invalid synthetic schema identifier", nameof(schema));
        // Identifiers cannot be query parameters; accept only our generated, bounded identifier.
        var sql = create ? "CREATE SCHEMA " + schema : "DROP SCHEMA " + schema + " CASCADE";
        return db.Database.ExecuteSqlRawAsync(sql);
    }

    [Fact]
    public async Task CatalogDerivedAlphaNamesSupportProjectionsAndLedgerRoundTrips()
    {
        var schema = "alpha_mapping_" + Guid.NewGuid().ToString("N");
        using var root = fixture.Context();
        await SchemaAsync(root, schema, create: true);
        try
        {
            using var db = Context(schema);
            // Only catalog definitions are reproduced; no hosted customer or financial records are copied.
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE customers (id uuid PRIMARY KEY, full_name text, email text, phone text);
                CREATE TABLE customer_vehicles (id uuid PRIMARY KEY, customer_id uuid, year integer, make text, model text, engine text, vin text, nickname text);
                CREATE TABLE entrepreneur_payout_batches (id uuid PRIMARY KEY, settlement_date timestamptz NOT NULL, currency varchar(10) NOT NULL, total_amount numeric(18,2) NOT NULL, earning_count integer NOT NULL, status varchar(30) NOT NULL, payout_reference varchar(200), created_at timestamptz NOT NULL, paid_at timestamptz, processed_by_user_id uuid);
                CREATE TABLE entrepreneur_referral_audits (id uuid PRIMARY KEY, entrepreneur_referral_id uuid NOT NULL, old_entrepreneur_user_id uuid, new_entrepreneur_user_id uuid, reason varchar(500) NOT NULL, changed_by_user_id uuid NOT NULL, changed_at timestamptz NOT NULL);
                """);
            var customer = Guid.NewGuid(); var vehicle = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO customers (id, phone) VALUES ({customer}, {"synthetic-phone"})");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO customer_vehicles (id, customer_id, vin) VALUES ({vehicle}, {customer}, {"synthetic-vin"})");
            Assert.Equal("synthetic-phone", await db.Customers.Where(c => c.Id == customer).Select(c => c.PhoneNumber).SingleAsync());
            Assert.Equal("synthetic-vin", await db.CustomerVehicles.Where(v => v.Id == vehicle).Select(v => v.VIN).SingleAsync());
            var batch = new EntrepreneurPayoutBatch { SettlementDate = DateTime.UtcNow, TotalAmount = 40.25m, EarningCount = 2, Status = "PAID", PayoutReference = "synthetic-ledger" };
            var audit = new EntrepreneurReferralAudit { EntrepreneurReferralId = Guid.NewGuid(), Reason = "Synthetic reviewed reassignment", ChangedByUserId = Guid.NewGuid(), NewEntrepreneurUserId = Guid.NewGuid() };
            db.AddRange(batch, audit); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            Assert.Equal(40.25m, (await db.EntrepreneurPayoutBatches.SingleAsync()).TotalAmount);
            Assert.Equal(audit.NewEntrepreneurUserId, (await db.EntrepreneurReferralAudits.SingleAsync()).NewEntrepreneurUserId);
        }
        finally { await SchemaAsync(root, schema, create: false); }
    }

    [Fact]
    public async Task MappingMigrationPreservesExistingLedgerRows()
    {
        var schema = "alpha_mapping_" + Guid.NewGuid().ToString("N");
        using var root = fixture.Context();
        await SchemaAsync(root, schema, create: true);
        try
        {
            using var db = Context(schema);
            await db.GetService<IMigrator>().MigrateAsync("20261012071522_AttributedRefundRecovery");
            var batch = Guid.NewGuid(); var audit = Guid.NewGuid(); var referral = Guid.NewGuid(); var reviewer = Guid.NewGuid(); var now = DateTime.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"EntrepreneurPayoutBatches\" (\"Id\", \"SettlementDate\", \"Currency\", \"TotalAmount\", \"EarningCount\", \"Status\", \"PayoutReference\", \"CreatedAt\") VALUES ({batch}, {now}, {"USD"}, {40.25m}, {2}, {"PAID"}, {"preserve-synthetic-reference"}, {now})");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"EntrepreneurReferralAudits\" (\"Id\", \"EntrepreneurReferralId\", \"Reason\", \"ChangedByUserId\", \"ChangedAt\") VALUES ({audit}, {referral}, {"Preserve synthetic reason"}, {reviewer}, {now})");
            await db.Database.MigrateAsync();
            var migratedBatch = await db.EntrepreneurPayoutBatches.SingleAsync();
            Assert.Equal(batch, migratedBatch.Id); Assert.Equal(40.25m, migratedBatch.TotalAmount);
            Assert.Equal("preserve-synthetic-reference", migratedBatch.PayoutReference);
            var migratedAudit = await db.EntrepreneurReferralAudits.SingleAsync();
            Assert.Equal(audit, migratedAudit.Id); Assert.Equal(referral, migratedAudit.EntrepreneurReferralId);
            Assert.Equal(reviewer, migratedAudit.ChangedByUserId); Assert.Equal("Preserve synthetic reason", migratedAudit.Reason);
        }
        finally { await SchemaAsync(root, schema, create: false); }
    }

    [Fact]
    public async Task MappingMigrationRejectsLossyMoneyNarrowingWithoutChangingRows()
    {
        var schema = "alpha_mapping_" + Guid.NewGuid().ToString("N");
        using var root = fixture.Context();
        await SchemaAsync(root, schema, create: true);
        try
        {
            using var db = Context(schema);
            await db.GetService<IMigrator>().MigrateAsync("20261012071522_AttributedRefundRecovery");
            var id = Guid.NewGuid(); var now = DateTime.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"EntrepreneurPayoutBatches\" (\"Id\", \"SettlementDate\", \"Currency\", \"TotalAmount\", \"EarningCount\", \"Status\", \"CreatedAt\") VALUES ({id}, {now}, {"USD"}, {40.251m}, {1}, {"PAID"}, {now})");
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());
            Assert.Equal("P0001", error.SqlState);
            Assert.Equal(40.251m, await db.Database.SqlQueryRaw<decimal>("SELECT \"TotalAmount\" AS \"Value\" FROM \"EntrepreneurPayoutBatches\"").SingleAsync());
            Assert.DoesNotContain("20261012120341_AlignAlphaLedgerMappings", await db.Database.GetAppliedMigrationsAsync());
        }
        finally { await SchemaAsync(root, schema, create: false); }
    }
}
