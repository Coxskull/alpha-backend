# Disposable local upgrade and restore rehearsal

Result: **PASS**, 2026-10-12. Authorized scope: disposable local rehearsal only.

Baseline PR #32: `e2f969c4bc89fab8417d93e72b43bed388808d31` (seven migrations). Candidate draft PR #36: `d47919b6a8b6d54d3d21e63889021b294fd94b87` (nine migrations). Both source worktrees remained clean; no application commit was changed. The existing exact-head Release build/test/security evidence remains applicable: 175 tests, zero failed/skipped, 24.40% non-migration source coverage. This rehearsal adds upgrade/role/restore evidence; it does not increase that coverage figure.

.NET 8 Release scratch harnesses built with warnings as errors, zero warnings/errors. PostgreSQL 16.15 used pinned image `postgres:16@sha256:ca0bd484cb98bf4b24eb1010e73fb3fcbd6714d240fbc1a10eea5b7dbecb641d`, bound to loopback. Environment provider/database credentials were removed before execution; only fixed disposable local bindings were used.

## Observed checks

- Migrated the exact baseline, seeded synthetic paid supplier history, completed refund operations, future recovery claims, an audited legacy conflict whose display status had been cleared, and a paid entrepreneur batch of 40.25 USD.
- Backed up and restored the seven-migration baseline. All rows in the eleven enumerated business tables matched exactly and migration history remained seven.
- Applied the two pending corrections to reach nine migrations. Full model/schema comparison passed. Historical supplier payout and customer refund totals and the entrepreneur batch amount were preserved.
- Conflict backfill recovered the audited legacy conflict; the unrelated valid source remained unflagged.
- Exercised the application accounting service under an explicitly selected NOLOGIN, NOSUPERUSER, NOBYPASSRLS runtime role. Schema creation, trigger disabling, replication-role bypass and audit updates failed with SQLSTATE 42501; clearing the durable conflict failed with 23514.
- Conflicted future recovery was rejected and its recovered amount remained zero. Valid future settlement recovered 10 USD and recorded a synthetic 40 USD net payout from a 50 USD entitlement.
- Backed up and restored the upgraded database with no owner/ACL restoration. The eleven-table business snapshot matched exactly, migration history remained nine, model/schema parity passed and the durable conflict trigger rejected clearing even as the restore owner.
- Removed the container and all its local databases/roles at completion.

The eleven snapshot tables are orders, payments, order_financials, suppliers, supplier_payouts, settlement_queue, payment_operations, refund_allocations, payout_recovery_offsets, audit_logs and entrepreneur_payout_batches. Complete synthetic schema/data dumps are included, but row equality was asserted for this stated set. This is a PostgreSQL dump/restore rehearsal, not automatic application migration downgrade approval.

## Evidence and reproduction

`rehearsal-evidence.zip` contains execution logs, result manifests, canonical row snapshots, full synthetic PostgreSQL dumps, the standalone C# harnesses, Python orchestration, and two failed scratch-harness development attempts. The first attempt exposed local-function expression-tree compilation and an analyzer warning; the second exposed a quoted Supplier Id column. Both were fixed in the harness; the final run passed every command. No product defect was identified by these failures.

Extract into `/workspace/work/acceptance-rehearsal`. Check out the baseline at `/workspace/work/acceptance-base` and candidate at `/workspace/work/current-financial-gate`; install .NET SDK 8.0.425 at `/workspace/.dotnet`, with disposable NuGet/CLI directories matching `run.py`. With Docker available and loopback 5432 free, execute `python3 /workspace/work/acceptance-rehearsal/run.py`. The runner uses fixed synthetic local connection values, creates one disposable container, and removes it in a finally block. Source project references deliberately identify these exact checkout locations. Do not substitute any hosted target or real records.

## Limits

The runtime grants are a rehearsal template for the exercised accounting path, not a certification of a hosted application's complete privilege set. This role has no login and no existing credential was changed. SELECT grants do not demonstrate tenant RLS policy correctness. Owner bypass capabilities remain a PostgreSQL administration consideration. No hosted Auth/Storage/TLS/provider bindings or real transfers were exercised. Independent human review and any separately authorized target-specific acceptance remain pending. All correction PRs remain drafts; no production access, merge, deployment, live transfer or credential changes occurred.
