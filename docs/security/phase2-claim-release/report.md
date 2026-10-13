# Phase 2: existing-claim recovery release

Date: 2026-10-13. **Local exact-head gate PASS** at cdffbfc5bdb8acfa710cbed0c7f6eda93f561b66, draft PR #39 stacked on PR #38 head 442f3ed5c946b08580f0d6934dfa54f2f943cba8. Scope selected by the user: existing future-recovery claims only; keep original-order payout and new-allocation holds.

## Resulting behavior

An active attesting administrator records an immutable authorization for one existing outstanding future-recovery allocation on the reviewed payment after two distinct active administrators have attested the unchanged financial evidence. Source currencies must agree; original paid history is revalidated and bounded against total attributed debt. The payment conflict flag and financial/queue holds are not cleared. Original-order payout and new-allocation guards are unchanged.

Recovery preflights all matching payee/type/currency claims before consuming any balance. A conflicted claim needs its own matching source fingerprint. Missing source/order/financial/paid-history evidence, changed provider evidence, another unreleased conflicted claim or an unprocessed signed source refund event stays blocked. Source changes require a fresh two-admin review and another immutable release. Ordinary partial recovery and its offsets are excluded from the ongoing authorization fingerprint so the approved claim can progress across zero-net and later net payouts without double consumption.

Fingerprint version 2 adds original paid queues, supplier/driver payout history and entrepreneur earnings/batches. Existing version-1 attestations are retained but become stale and cannot authorize release without fresh review. No review is silently rewritten. Actor/source/claim/evidence references remain immutable; identical retries create one effect, changed/reassigned retries fail.

## Validation

- .NET SDK 8.0.425 Release builds with warnings as errors: zero warnings/errors.
- **205/205 tests passed**: 77 unit + 128 PostgreSQL/HTTP integration; zero failed/skipped. Fourteen cases were added to the 191-test review baseline, plus HTTP authorization/binding checks in an existing case.
- Cases include partial/zero-net progress, original-order/new-allocation hold retention, unchanged paid history, evidence-change rehold and fresh review, cross-order/one-review/stale-state rejection, concurrent release and payout idempotency, immutable release UPDATE/DELETE/TRUNCATE guards, unreleased sibling claims, missing finances/paid history, payee/role/currency isolation, pending signed source events and mixed source currencies despite fresh attestations.
- Eleven migrations / 60 model tables; zero missing tables or model/schema mismatches.
- Fresh application/unit/integration transitive vulnerability audits: no known vulnerable packages from current sources.
- Non-migration source coverage: 25.53% (4,630/18,134). Generated migrations are reported separately in the coverage manifest.
- Synthetic startup returned HTTP 200; source worktree remained clean and all disposable containers/databases were removed.

The first development run caught a tracked/detached RefundAllocation identity conflict when a context already tracked the claim; reloading now uses a fresh read-only query. Subsequent 204-test implementation run passed. Final source review added the mixed-currency rejection and stacked-base CI branch filters. Only final/ logs in the archive establish the current head; earlier logs are explicitly development records.

## Evidence and limits

validation-summary.json records exact head/base, TRX counters, schema totals, coverage and command exits. exact-head-evidence.zip contains final build/restore/audit/test/TRX/coverage/schema/startup logs, generated SQL, runner source and earlier development logs. SHA256SUMS covers published files. All execution used pinned disposable loopback PostgreSQL 16.15 and synthetic providers, with inherited hosted/provider bindings stripped from dotnet execution.

Release history has restrictive FKs, uniqueness, immutable UPDATE/DELETE/TRUNCATE guards and insert checks for two attestations, an eligible claim and an attesting actor. Active-role and source fingerprint verification remain application responsibilities. A privileged SQL writer/owner can forge/bypass application checks; runtime identities must be constrained. Administrative out-of-band source mutations require separately coordinated procedures. Source coverage remains limited and operator/provider evidence authenticity remains an independent human acceptance requirement.

The additive migration needs an approved isolated hosted baseline/upgrade rehearsal. Down destroys release history and is not an approved operational rollback. No production access, merge, deployment, live transfers or credential changes occurred. This is backend functionality; no operator UI is supplied.

## Stack and merge order

PR #39 targets PR #38's branch, while #38 remains an open draft targeting main. Either merge #39 into #38 first, then integrate #38 into main with a rewritten combined description and fresh exact-main verification; or merge #38 first and **retarget #39 to main before merging it**. Merging into a former branch after its parent PR is already merged will not update main. No merge is performed by this work.
