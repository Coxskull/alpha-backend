using Alpha.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text.Json;

// Intentionally fixed disposable loopback targets; never consume DATABASE_URL.
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
AppDbContext Context(string database) => new(new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql($"Host=127.0.0.1;Port=5432;Database={database};Username=alpha_ci;Password=disposable-ci-only;SSL Mode=Disable")
    .Options);
using var model = Context("alpha_model_ci");
File.WriteAllText(Path.Combine(output, "model.sql"), model.Database.GenerateCreateScript());
File.WriteAllText(Path.Combine(output, "migrations.sql"), model.GetService<IMigrator>().GenerateScript());
var tables = model.Model.GetEntityTypes().Select(e => e.GetTableName()).Distinct().Order().ToArray();
File.WriteAllText(Path.Combine(output, "model-tables.json"), JsonSerializer.Serialize(tables));
Console.WriteLine($"Model generated: {tables.Length} tables; migration IDs: {string.Join(",", model.Database.GetMigrations())}");
if (args.Contains("--model-only")) return;

var observations = new Dictionary<string, object>();
observations["scope"] = "Disposable loopback PostgreSQL only; no hosted schema or provider operations";
observations["postgresql"] = await model.Database.SqlQueryRaw<string>("SELECT version() AS \"Value\"").SingleAsync();
// Bootstrap from current model independently of migrations to establish provider compatibility.
await model.Database.EnsureCreatedAsync();
observations["modelOrdersReadable"] = await model.Orders.CountAsync() == 0;
observations["modelPaymentsReadable"] = await model.Payments.CountAsync() == 0;
observations["modelSecurityLogsReadable"] = await model.SecurityLogs.CountAsync() == 0;
using var migrated = Context("alpha_migrations_ci");
try
{
    await migrated.Database.MigrateAsync();
    observations["migrationsApplied"] = true;
    observations["appliedMigrationIds"] = await migrated.Database.GetAppliedMigrationsAsync();
    var actual = await migrated.Database.SqlQueryRaw<string>(
        "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'").ToListAsync();
    var missing = tables.Where(t => t != null && !actual.Contains(t)).ToArray();
    observations["missingModelTablesFromMigrations"] = missing;
    observations["migrationModelParity"] = missing.Length == 0 ? "PASS (table presence only)" : "FAIL";
}
catch (Exception ex)
{
    observations["migrationsApplied"] = false;
    // Disposable DB only. Avoid connection strings and exception details in exported evidence.
    observations["migrationFailureType"] = ex.GetType().Name;
    observations["migrationModelParity"] = "FAIL";
}
File.WriteAllText(Path.Combine(output, "database-compatibility.json"),
    JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(File.ReadAllText(Path.Combine(output, "database-compatibility.json")));
if (!(bool)observations["modelOrdersReadable"] || !(bool)observations["modelPaymentsReadable"]
    || !(bool)observations["modelSecurityLogsReadable"]) throw new Exception("Model database smoke failed.");
// Migration parity is a release gate: a successful migration command is insufficient
// when the migrated schema omits tables required by the current EF Core model.
if (!string.Equals(observations["migrationModelParity"]?.ToString(), "PASS (table presence only)", StringComparison.Ordinal))
    throw new InvalidOperationException("Disposable PostgreSQL migration/model parity failed. See database-compatibility.json.");
