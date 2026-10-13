# Phase 4: internal payout reconciliation

2026-10-13 (Asia/Taipei). Draft implementation and local automated gates passed. Independent operator/accounting/security, hosted financial acceptance and external provider/bank reconciliation are not claimed.

| Repository | Draft PR | Exact head |
|---|---|---|
| Coxskull/alpha-backend | #42 targeting main | 08b31205949db27dcc8cb049fef1d930ccb2289a |
| Coxskull/Alpha-Auto-MVP | #2 targeting main | 5806378a81902d5985f4a3c1c553b2995a6d5f55 |

## Implementation and financial boundary

The administrator internal review queue compares supported supplier/driver settlement sources against payout records and entrepreneur earnings against their single-earning batch. It checks net amounts, signed adjustments, currency/payee/type attribution, order association, global recovery-claim totals, target offsets, payout audit amounts/references and paid metadata. Missing, duplicated, mixed/legacy or contradictory evidence appears as exceptions. Nothing in these endpoints repairs accounting, records a payment, releases a hold or calls a provider.

The frontend is linked from the financial dashboard, shows page-local exceptions and keyset paging for both source kinds, current snapshot net/recovery/reference details, and immutable history with captured values and exceptions. Captures write only versioned observations into existing SQL-guarded audit_logs. Each fresh capture requires the inspected fingerprint; stale evidence is rejected. Exact actor/source/fingerprint/UUID replay returns the original observation, including after source state changes. Session advisory locking on an explicitly held connection serializes identical capture IDs before the repeatable-read transaction. No payout/payee lock is acquired by reconciliation; concurrent payout reads observe one committed state. A captured observation may become stale after its database snapshot; it is not approval or proof of continued consistency. Current active-administrator database roles and token authorization are both enforced.

Unknown POST outcomes preserve the capture UUID/fingerprint; operators may check history or explicitly replay that same request. No automatic retries or financial mutation occurs. Existing UPDATE/DELETE/TRUNCATE audit guards protect history; privileged database owners remain able to bypass guards. No new migration or dependency version changes were introduced. API/runbook: docs/security/payout-reconciliation.md in backend PR42.

## Exact-head validation

- Backend: **224/224 tests passed**, 77 unit +147 PostgreSQL/HTTP integration, zero failures/skips. .NET8.0.425 Release warnings-as-errors builds, locked restores, app/unit/integration vulnerability audits, eleven-migration /60-table model/schema parity and synthetic health200 passed. Non-migration source coverage **26.22% (4797/18293)**; generated migrations reported separately.
- Sixteen new integration cases exercise balanced and missing evidence, exact source/reference/currency/payee matching, zero-net recovery ledger checks, immutable SQL capture history, actor-bound concurrent idempotency, stale fingerprints, concurrent payout observations, entrepreneur batch currency and anonymous/non-admin/unknown/inactive/active-admin HTTP behavior.
- Frontend: Node24.19.0 clean install with package scripts enabled, TypeScript, changed-file lint, Next16.4 production build and **12 pure contract tests** passed. One pre-existing unused-variable warning remains in the unchanged entrepreneur configuration code. **Eight local synthetic Chromium scenarios** passed: balanced exact fingerprint capture, exception capture without payout mutation, stale evidence, unknown outcome resolved through history without repeat, malformed queue, API denial, page cursor/filter/mobile sizing and earning-scoped capture. Browser requests use synthetic bearer tokens and fixed loopback interception; external requests were blocked. Browser cases are local, not CI/provider acceptance. No frontend coverage percentage is claimed.
- Frontend [exact-head CI](https://github.com/Coxskull/Alpha-Auto-MVP/actions/runs/38354180898) passed; [Vercel PR check](https://vercel.com/alpha-auto-s-projects/alpha-auto-mvp/5BVhQvFij3QCwUmAHHGYMb8k4mo8) reports success. The existing Git integration ran the PR preview check; no production URL or deployment command was used.
- Backend [Release CI](https://github.com/Coxskull/alpha-backend/actions/runs/38354134964) and [companion CI](https://github.com/Coxskull/alpha-backend/actions/runs/38354134888) both passed on the exact head. Decoded Release logs are preserved permanently. Artifact11791834369, SHA25631fba877a82bd6b89dd5daf4508e4d8fa53539f1f42b44ae70928aac3d2ac6cd, expires2026-11-12.

Development evidence retains the first 217-test successful iteration and the later three HTTP fixture expectation failures: unseeded accounts were correctly rejected with401 before role authorization. Final fixtures seed current users for403/200 checks and expect401 for anonymous/unknown/inactive users. This was a test-fixture correction, not a weakened authorization check. Only final/ establishes the reported exact-head gates.

## Findings and operational limits

Production npm audit has zero known entries. A fresh full graph audit retains five high development entries propagating from the prior single braces advisory GHSA-vfj7-8cjw-p6xm; no all-dependency security clearance is claimed.

This is a supported **source-first internal consistency review**, not complete general-ledger or external bank/provider reconciliation. Source-less historical payout records must be inventoried separately. Unsupported mixed/legacy batches, missing order attribution and more than200 inspected evidence rows per order/batch/target require manual review. Queues/history use UUID keyset cursors and bounded20-source pages; new UUIDs behind an existing cursor require a fresh pass. Exceptions filtering is per loaded page, not a whole-ledger assertion. Existing audit storage uses textual versioned payload lookup; realistic-volume benchmarking/index review in isolation remains required before hosted acceptance. No approved isolated hosted target or baseline is inferred from configured environment bindings.

Both drafts require human operator/accounting/security review. Backend42 supplies the API dependency before frontend2 payout reconciliation use. After owner merges, reconcile resulting main SHAs and verify source changes. No agent merge, production access/deployment, live transfer or credential change occurred. Local app/browser processes stopped and disposable PostgreSQL was removed. The synthetic screenshot's existing Live header is a baseline UI badge, not proof of production access.

validation-summary.json records heads/counts/coverage/audit scopes/command exits. exact-head-evidence.zip includes final logs/TRX/schema/startup/coverage/audits, frontend CI/Vercel status/browser outcomes/screenshot, runner/harness and marked development logs. SHA256SUMS verifies deliverables.
