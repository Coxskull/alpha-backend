# Inventory acceptance review: invalid native numeric records

2026-10-13 (Asia/Taipei). Assistant review and automated evidence, not independent human accounting/operator/security signoff. Parent PR43/backend and PR3/frontend are merged. Their merge trees equal the reviewed application heads; merged-base-proof.json preserves exact SHA/tree proof. Correction drafts44/backend and4/frontend target those main bases.

## Medium finding and original probes

PostgreSQL numeric supports NaN even where ordinary money precision is constrained. At merged backend application65bda0356f3a42375aafc0b64ce2429f868e170e (tree equal reviewed ab0d98bf88abfed744ec600785ed8f95e50f8ac9), an out-of-band synthetic NaN in orphan supplier/driver payouts or an orphan entrepreneur batch caused Npgsql decimal materialization to fail for the inventory page. A linked supplier NaN was instead omitted, since the old SQL filter only checked negative amount and its source links remained consistent.

Four added regression probes against that unchanged application implementation failed: three InvalidCastExceptions and one empty-result assertion. The checkout included the added uncommitted test fixture; this reproduction is not a clean unchanged-source test checkout. The archive includes original identity/build/audit/schema logs, failing TRX and matching regression. These are legacy/out-of-band corruption scenarios, not a decimal-typed API route, unauthenticated exploit or funds-transfer event.

## Corrected behavior

The query checks the native PostgreSQL numeric against the existing non-negative, bounded, two-decimal money domain before any .NET decimal conversion. Valid values become canonical trim-scale decimal strings, preserving value/cents without floating-point conversion. Invalid values, including otherwise-linked records, are included with amount=null, amountValid=false and invalid_payout_amount. No zero, corrected balance or financially valid value is invented. The stored record remains unchanged; tests restore only their synthetic invalid fixtures for test isolation after asserting read behavior. UI markers show Invalid recorded amount and leave paging/read-only controls available. The new explicit validity field is a paired API dependency.

This fixes record-inventory visibility, not all malformed-data readers. Source-first inspection of invalid underlying numeric fields still requires manual review. No automatic normalization/repair, capture, attachment, payment, transfer or hold release occurs. No migration/dependency-version change was introduced.

## Exact corrected heads and validation

|Repository|Draft PR|Head|
|---|---|---|
|Coxskull/alpha-backend|44|f515c6a7de88f60cdfd573d4c64443e378ac5418|
|Coxskull/Alpha-Auto-MVP|4|ee6ced8c9ccf9e4a0a9cde5fecb984aa83dc0294|

Backend **236 tests passed (77 unit+159 PostgreSQL/HTTP integration)**, zero failures/skips. .NET8.0.425 Release warnings-as-errors builds, locked restores, app/unit/integration vulnerability audits, eleven-migration/60-table parity and synthetic health200 passed. Source coverage excluding migrations **26.49% (4864/18361)**; generated migration coverage is separate. The original four cases now pass and prior isolation/idempotency/immutability/concurrency cases remain green.

Frontend Node24.19.0 clean install, TypeScript, changed-file lint, Next16.4 build and **18 contract tests** passed. **Seven local synthetic browser scenarios** passed, including null invalid-amount markers with no invented zero or blocked paging; all fixture API requests allowed only GET/OPTIONS at fixed loopback with synthetic tokens and external browser traffic blocked. Browser tests are local, not CI or provider acceptance. One unchanged configuration-page unused-variable lint warning remains.

[Frontend CI](https://github.com/Coxskull/Alpha-Auto-MVP/actions/runs/38379587594) and [Vercel PR check](https://vercel.com/alpha-auto-s-projects/alpha-auto-mvp/2D3MZXsrLRxxQYeayZZsni7xbHYf) passed. Backend [companion CI](https://github.com/Coxskull/alpha-backend/actions/runs/38379583754) and [Release CI](https://github.com/Coxskull/alpha-backend/actions/runs/38379584066) both passed on the exact correction head. Decoded Release logs and artifact metadata are preserved.

Fresh production npm audit has zero known findings. The prior braces advisory GHSA-vfj7-8cjw-p6xm still propagates through five high development entries; no full-graph clearance is claimed. Other residual gates remain volume/query-plan review, source-first malformed-data handling, approved isolated hosted schema/baseline/roles and independent human accounting/security/operator and external bank/provider evidence.

## Evidence and review order

The archive preserves original failed probes and final TRX/build/audit/schema/startup/coverage/CI/browser logs, merged-parent proof and runners. validation-summary.json records counts/heads/audits. Screenshot is local mock data; the baseline Live badge is not production evidence. SHA256SUMS verifies artifacts. Local processes stopped and disposable PostgreSQL removed.

Review backend44 before frontend4; the new UI requires amountValid and rejects older missing-field responses safely. Verify owner-merged main heads before rollout. Existing Vercel Git integration ran a PR check; no production URL or deployment command was used. No production access, agent merge, live transfer or credential change occurred.
