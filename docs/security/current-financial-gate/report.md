# Current-head financial integrity gate — PR #36

Disposition: **isolated consolidated-candidate checks PASS; release remains OPEN / PENDING**. Current PR #32 alone still lacks the drafted financial corrections. No PR was merged, closed or deployed.

## Head reconciliation

The reported/reviewed PR #32 head was `8946949623433e8317be7b6814e93086defda818`. A fresh GitHub snapshot and remote pull ref now identify `e2f969c4bc89fab8417d93e72b43bed388808d31`. It is a direct descendant adding guarded Alpha ledger/customer/vehicle mapping changes and three compatibility tests. Its exact-head CI logs confirm 149 passing tests (77 unit + 72 integration), zero warnings/errors and schema parity. Hosted metadata statements in that commit were not re-executed or independently certified by this assistant; no hosted project/database was accessed.

| Draft | Recorded head | Scope |
|---|---|---|
| PR #32 | `e2f969c4bc89fab8417d93e72b43bed388808d31` | Current base; mappings and guarded narrowing |
| PR #33 | `40a633910f12d3687b463f70e9acff93746b2f2b` | Refund aggregation and financial history guards |
| PR #34 | `19c6c2034b5577a78c1a66e39e2e207b1b10537f` | Durable conflict holds and conservative legacy backfill |
| PR #35 | `2d103cd4a74cadf9b19e21cb08df5c2d3ba0326d` | Fail-closed recovery for missing source evidence |
| PR #36 | `d47919b6a8b6d54d3d21e63889021b294fd94b87` | Consolidated corrections on current PR #32 base |

The original correction heads remain unchanged. PR #36 reapplies their commits on the current base without merging or closing any PR. The conflict migration's generated target model was regenerated to retain current snake-case mappings and numeric precision. The EF snapshot combines the current model with the conflict flag; no temporary metadata migration remains. All nine migrations were applied in disposable PostgreSQL and verified against the combined model.

## Corrected findings and current behavior

- Multiple-payment refunds preserve the order-wide confirmed total under stable payment locks.
- Financial audit/recovery history rejects mutation; reviewed allocation identity/evidence remain protected while legitimate recovery totals increase monotonically.
- Unresolved payment conflicts survive valid/replayed evidence, other payments/gateways and generic display-status changes. Source-order allocation/payout and same-payee future recovery remain gated. Evidence changes wait for outstanding source payee locks, including other payments on the order.
- Missing source financial rows or order associations require manual review without consuming debt or creating offsets/payouts.
- The new base's mapping migration preserves ledger records and rejects lossy monetary narrowing; customer/vehicle projections use current names.

[Original financial findings](../pr32-financial-gate/report.md), [conflict-hold review](../pr33-review/report.md), [durable-hold correction](../pr34-conflict-holds/report.md) and [missing-source review/correction](../pr35-missing-source/report.md) retain reproductions and earlier commit-specific evidence. No additional scoped code blocker was reproduced during consolidation; this is not an exhaustive security audit or independent human approval.

## Exact-head verification

Local execution and [exact-head CI](https://github.com/Coxskull/alpha-backend/actions/runs/38256593847) at `d47919b6a8b6d54d3d21e63889021b294fd94b87` agree:

- .NET SDK 8.0.425 Release builds with warnings as errors: zero warnings/errors.
- 175 tests passed: 77 unit + 98 PostgreSQL/HTTP integration, zero failed/skipped.
- Nine migrations / 57 model tables: disposable migration/model parity PASS.
- Application/unit/integration NuGet audits: no known vulnerable packages from current sources.
- Synthetic loopback startup: HTTP 200. Hosted database/TLS/Supabase readiness is not established by health.
- Non-migration source coverage: 24.40% (4,366/17,895). Assembly coverage including generated migrations: 61.93%; do not describe it as runtime coverage.

[Companion validation](https://github.com/Coxskull/alpha-backend/actions/runs/38256593872) also passed. [CI artifact](https://github.com/Coxskull/alpha-backend/actions/runs/38256593847/artifacts/11750333342) retains TRX, coverage, schema/startup and build/audit logs. GitHub-reported SHA-256: `f7487d309c5dd6d8edbf26b733f00d3713edb699dbd79259fac2e3ce467c96db`; 30-day retention. Its recorded head matches PR #36.

`exact-head-evidence.zip` permanently preserves the full local 175-test TRX, coverage, schema outputs, source tree identity, runner, manifests and final CI job log. It also includes current-base CI logs for reconciliation. ZIP integrity and per-file SHA-256 manifests were checked. All local processes used stripped inherited integration settings, fixed disposable loopback PostgreSQL and synthetic provider responses. The local database and CI service containers were removed.

## Next acceptance gates

The [isolated acceptance plan](isolated-acceptance-plan.md) specifies required inputs, rehearsal steps, observable cases and evidence. Outstanding gates are an owner-proven isolated hosted target and approved baseline; target-specific upgrade/backfill and snapshot-restoration rehearsal; runtime grants/RLS/Auth/Storage/TLS/provider acceptance; independent security/accounting/client review; explicit audited conflict resolution; credential-owner work and existing tax/referral/dispute/legacy exceptions. The provisioning chain must not be applied blindly to a hosted manually provisioned database.

No production access, hosted migration, live provider calls/transfers, bank debits, credential changes, merge or deployment occurred in this work. Passing isolated checks does not authorize release.
