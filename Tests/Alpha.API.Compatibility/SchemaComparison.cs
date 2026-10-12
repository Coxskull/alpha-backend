using Alpha.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

public static class SchemaComparison
{
    public static async Task<List<string>> CompareAsync(AppDbContext db)
    {
        var columns = await db.Database.SqlQueryRaw<ColumnInfo>("""
            SELECT c.relname AS "Table", a.attname AS "Column",
                pg_catalog.format_type(a.atttypid, a.atttypmod) AS "Type", NOT a.attnotnull AS "Nullable"
            FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON c.relnamespace = n.oid
            JOIN pg_catalog.pg_attribute a ON a.attrelid = c.oid
            WHERE n.nspname = 'public' AND c.relkind = 'r' AND a.attnum > 0 AND NOT a.attisdropped
            """).ToListAsync();
        var indexes = await db.Database.SqlQueryRaw<IndexInfo>("""
            SELECT t.relname AS "Table", i.relname AS "Name", ix.indisunique AS "Unique"
            FROM pg_index ix JOIN pg_class t ON ix.indrelid = t.oid
            JOIN pg_class i ON ix.indexrelid = i.oid JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'public'
            """).ToListAsync();
        var constraints = await db.Database.SqlQueryRaw<ConstraintInfo>("""
            SELECT t.relname AS "Table", c.conname AS "Name" FROM pg_constraint c
            JOIN pg_class t ON c.conrelid = t.oid JOIN pg_namespace n ON n.oid = t.relnamespace
            WHERE n.nspname = 'public'
            """).ToListAsync();
        var mismatches = new List<string>();
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table == null) continue;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            foreach (var property in entity.GetProperties())
            {
                var name = property.GetColumnName(store);
                if (name == null) continue;
                var actual = columns.SingleOrDefault(c => c.Table == table && c.Column == name);
                if (actual == null) { mismatches.Add($"Missing column {table}.{name}"); continue; }
                var expectedType = property.GetColumnType(store) ?? property.GetRelationalTypeMapping().StoreType;
                if (actual.Type != expectedType) mismatches.Add($"Type {table}.{name}: {actual.Type}; expected {expectedType}");
                if (actual.Nullable != property.IsColumnNullable(store)) mismatches.Add($"Nullability {table}.{name}");
            }
            foreach (var index in entity.GetIndexes())
            {
                var actual = indexes.SingleOrDefault(i => i.Table == table && i.Name == index.GetDatabaseName());
                if (actual == null || actual.Unique != index.IsUnique) mismatches.Add($"Index {table}.{index.GetDatabaseName()}");
            }
            var keys = entity.GetForeignKeys().Select(f => f.GetConstraintName()).Append(entity.FindPrimaryKey()?.GetName());
            foreach (var name in keys.Where(n => n != null))
                if (!constraints.Any(c => c.Table == table && c.Name == name)) mismatches.Add($"Constraint {table}.{name}");
        }
        if (db.Database.HasPendingModelChanges()) mismatches.Add("EF snapshot differs from current model");
        return mismatches;
    }
    public sealed class ColumnInfo { public string Table { get; set; } = ""; public string Column { get; set; } = ""; public string Type { get; set; } = ""; public bool Nullable { get; set; } }
    public sealed class IndexInfo { public string Table { get; set; } = ""; public string Name { get; set; } = ""; public bool Unique { get; set; } }
    public sealed class ConstraintInfo { public string Table { get; set; } = ""; public string Name { get; set; } = ""; }
}
