# PR #32 financial integrity gate

Verdict: **BLOCKED at PR #32 head; corrections validated and awaiting independent review.**

Repository: Coxskull/alpha-backend. PR #32 remains open/draft. Both GitHub metadata and a fresh remote pull ref identify `8946949623433e8317be7b6814e93086defda818`, exactly matching the reported commit. The initial workspace was at base `ce256aa41ba0fdf8fafcfd7852bde47d1ced82e4`; an isolated worktree was fetched for testing. The PR description's 116-test evidence and policy link point at predecessor `08dadc2baaa6eeb8dafbed877d0aa496d9cbcecd`, not the current head. Six commits / 105 changed files separate the base from the reviewed head. No current-head difference from the user's reported SHA exists.

## Reproduced findings

1. **High — order refund total is overwritten by a single payment.** `Controllers/PayPalController.cs:189` and `Controllers/StripeWebhookController.cs:939` assign `financial.RefundAmount = payment.RefundedAmount`. Distinct completed refunds on payments sharing an order understate the order ledger. A supplemental concurrent-reconciliation test injected into the otherwise exact PR application source expected 30 and observed 20. Its failing log is in `pr32-evidence.zip`; it is explicitly separate from the pristine exact-head suite. Corrections serialize all order payments in stable ID order, aggregate refund totals across the order, and reject incompatible refund currencies. Settlement/payment-completion locks use the same stable ordering. The regression passes in the correction suite.

2. **High — financial audit and recovery history is mutable through SQL.** At the exact-head schema, a synthetic transaction inserted, updated and deleted an `audit_logs` row successfully before rollback (`audit-immutability-baseline.log`: UPDATE 1 / DELETE 1). Reviewed allocation fingerprints enforce replay rules through the service, but the database does not prevent editing allocation identity/evidence or deleting/changing recovery offsets. The correction migration makes `audit_logs` and `payout_recovery_offsets` append-only, rejects TRUNCATE, and protects allocation identity/evidence while permitting only bounded, monotonic recovery totals. Twelve direct-SQL regression cases verify update/delete/truncate rejection and preservation of recorded values. These guards cover the financial audit/recovery tables; other audit domains and legacy ledgers are outside this correction. Database owners can still deliberately disable/drop triggers; runtime users must not have schema-owner privileges.

## Exact execution results

| Check | PR32 `8946949` | Correction `77e0441` |
|---|---:|---:|
| .NET 8 Release builds, warnings as errors | PASS, zero warnings/errors | PASS, zero warnings/errors |
| Unit tests | 77 passed | 77 passed |
| PostgreSQL/HTTP integration tests | 69 passed | 83 passed |
| Failed / skipped in complete suite | 0 / 0 | 0 / 0 |
| Migration/model parity | PASS, 57 tables | PASS, 57 tables and history triggers tested |
| NuGet application/unit/integration audits | No known vulnerable packages | No known vulnerable packages |
| Compatibility dependency audit | Not separately run at PR32 | No known vulnerable packages |
| Synthetic startup health | 200 | 200 |
| Non-migration source line coverage | 24.26% (4,333/17,863) | 24.30% (4,343/17,874) |
| Assembly line coverage including migrations | 54.08% | 11.34% |
| Assembly branch coverage | 29.34% | 29.52% |

SDK 8.0.425; PostgreSQL 16.15, digest-pinned container `postgres:16@sha256:ca0bd484cb98bf4b24eb1010e73fb3fcbd6714d240fbc1a10eea5b7dbecb641d`. Host port bound to 127.0.0.1 only. Both suites use the fixed disposable loopback database; runtime database/Supabase/JWT/provider variables were removed from subprocess environments. Provider HTTP responses are synthetic handlers; HTTP acceptance uses loopback processes. The compatibility check executes the provisioning migrations in disposable PostgreSQL and compares columns, keys, relationships and indexes to the model. The archive contains generated model/migration SQL and table/schema observations. The correction migration was verified on the original schema and with recreated disposable databases at the preceding correction commit; the final correction differs only by the additional Stripe concurrency test and was verified against that isolated schema. The final correction coverage excludes migration hits because those migrations were already applied; its assembly percentage consequently drops without a drop in runtime coverage. A first correction attempt found a regression assertion expecting a trigger error where PostgreSQL's foreign-key TRUNCATE guard fired earlier; the final test uses CASCADE and verifies the trigger's rejection. Only final complete-suite results appear above.

## Reviewed financial behavior and test evidence

- Settlement: payment/refund review gates, proof and assignment requirements, queue creation and payout readiness reviewed. Locks protect financially reviewed payouts from concurrent refunds. Mechanic/unattributed queues remain manual exceptions.
- Payee/role/currency isolation: `OtherPayeesRolesAndCurrenciesAreNeverDebited`, `PaidAttributionRecoversOnlyFromSamePayeesFuturePayouts`, cross-order source rejection, customer resource ownership, and reordered supplier/driver role tests pass. Recovery matches identity, payee type and currency without foreign-exchange conversion.
- Idempotency/concurrency: same-key reservation, changed-amount rejection, concurrent captures/refunds, allocation replay, concurrent future payouts, shared-order source caps, commit rollback and unknown-provider-outcome recovery pass. Advisory payee locks and row locks serialize claim consumption; quotes roll back transient offsets until exact net confirmation.
- Refund attribution: completed, balanced and evidenced plans cap recovery at attributable historical payouts; unpaid adjustments preserve gross earnings. Paid history is retained and recovery comes from future same-payee payouts. Tests reject ambiguous historical evidence and over-allocation. Zero-net payouts and carry-forward pass.
- Webhooks: signatures, atomic processing leases, expired/failed lease recovery, concurrent signed Stripe delivery, complete/incomplete/conflicting refund evidence, historical/late snapshots, mapping ambiguity and rollback-induced payout holds pass. Stripe cumulative refunds do not prorate entrepreneur clawbacks. No Stripe API calls are made by the evidence adapter.
- Ledger/audit: existing paid-record preservation and atomic offset/payout rollback tests pass; the direct-SQL immutability finding above required correction. The complete executed-test inventory and per-file coverage are in each archive.

Security audits comprise fresh NuGet advisory checks, the repository's executable security/HTTP boundary tests, scoped source review, and the database mutation probes. No general-purpose SAST/secret-scanning product was installed; this is not a claim of an exhaustive vulnerability audit. Historical tracked credential exposure remains a documented owner-controlled release blocker. No credential values were read for validity, changed or exported.

## Acceptance limits

Passing tests do not authorize release. Original refund orders retain tax/referral/dispute reconciliation holds; ambiguous/unsupported sources and the separate legacy referral ledger require manual review. Overall runtime coverage remains about 24%; the assembly figure includes generated migrations and must not be represented as runtime coverage. Hosted schema/TLS/Supabase RLS/storage behavior and real provider sandbox contracts were not exercised. The provisioning migration chain is not an approved hosted upgrade. The new immutability migration needs owner review against a sanitized non-production schema and runtime grants before deployment.

The correction source commit is `77e0441994d4ba8ea6811f3eb5d8ec7c67df3ac1`, based on PR32 head. The evidence branch adds only this report and artifacts, keeping the correction PR head exactly equal to the tested commit. PR32 was rechecked after validation and still matches the reported SHA. No production access, merge, deployment, live transfer, bank debit, hosted migration or credential change occurred.

## Evidence

- `pr32-evidence.zip`: exact-head build/restore/audit/startup logs, original 146-test TRX, merged coverage and per-file summaries, schema results, SHA-256 source inventory, mutation probe and separately labeled failing supplemental refund regression.
- `correction-evidence.zip`: exact correction commit identity, 160-test TRX, build/restore/audit/startup logs, migration/model parity, coverage and source inventory. `supplemental-execution.json` retains the separately run compatibility dependency audit.
- `validation-summary.json` and `SHA256SUMS`: counts, coverage and archive integrity.
