# Migration reconciliation plan

Evidence: ALPHA-EV-20261012-P2Z001. Tracks issue #29 and blocking PR #28.

## Verified state

The EF Core model maps 51 tables. The checked-in migrations are 20260609065938_InitialCreate and 20260609152717_OrderRelations. Both apply on disposable PostgreSQL 16, but current-model table parity fails. The release-evidence workflow now correctly fails when migration parity fails.

## Safe remediation procedure

1. Generate an EF Core 8 migration from the current AppDbContext on an isolated development machine or CI runner using a disposable PostgreSQL endpoint. Review the generated Up and Down operations before committing.
2. Inventory all missing tables, columns, indexes, foreign keys, defaults, nullability, and constraints. Check whether any existing production schema objects are maintained outside EF Core. Do not assume a fresh database.
3. Reject destructive operations (DropTable, DropColumn, ambiguous rename, narrowing type changes) unless separately reviewed with a data-preservation plan. Never use EnsureCreated as a substitute for migrations.
4. Test both a fresh database migrated from zero and an upgrade from a disposable copy of the previous migrated schema. Compare full schema metadata, not only table names.
5. Run Release builds, automated tests, dependency audit, and exact-head release-evidence workflow; archive sanitized SQL diff and results.
6. Keep PR #28 blocked until migration parity passes. Require explicit human approval before production schema deployment.

## Execution status

This document is a remediation checklist only. No migration has been generated, applied, or verified as repaired.
