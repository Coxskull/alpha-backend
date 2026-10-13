# Phase 3: payout operator backend and frontend

2026-10-13. **Draft implementation and automated acceptance PASS**, with the development-tool advisory below still open. Independent accounting/operator/security and hosted acceptance are not claimed.

| Repository | Draft PR | Exact head |
|---|---|---|
| Coxskull/alpha-backend | #40, stacked on #39 | ebd572b613ba60b8e1befd063c30fcc429b37217 |
| Coxskull/Alpha-Auto-MVP | #1, targeting main | 9031196bf15df6d36cd68dc363dcde4ebe7ad4f6 |

## Operator behavior

Both settlement pages replace bodyless mark-paid actions with current-quote review. The new administrator entrepreneur payout queue is linked from the program header. The shared dialog presents gross, signed adjustments, payable before recovery, recovery offset, exact net cash, currency and remaining recovery; zero cash and minimum thresholds are explicit. Operators enter the exact net amount/reference and acknowledge the reviewed result. There is no gross fallback or money transfer.

Malformed, missing-metadata, unbalanced/unsafe-number, held, unauthorized or below-threshold quotes cannot be confirmed. The numeric display range is conservatively bounded at one trillion in absolute monetary values; larger values require manual review rather than JavaScript confirmation. Another payout can change the quote; the API returns payout_quote_changed, and the UI clears amount/acknowledgment, preserves the reference and requires a new decision after refresh. Network/5xx POST outcomes are treated as unknown and reconciled by quote/status refresh, with no automatic POST retry. A successful payout followed by a failed list refresh remains a known recorded payout. Authenticated API configuration is preserved.

The backend adds net/payable/carry-forward/zero/eligibility metadata and stable codes beside existing error messages, preserving paidAmount/status fields and locked atomic accounting. Entrepreneur threshold eligibility is available before confirmation. New UI fresh-quote metadata is a deployment dependency; legacy fresh quotes are rejected safely, while already-recorded responses remain readable.

## Verification

- Backend: **208 tests passed**, 77 unit + 131 PostgreSQL/HTTP integration; zero failures/skips. .NET SDK 8.0.425 Release builds with warnings as errors had zero warnings/errors. Locked restores, application/unit/integration vulnerability audits, eleven-migration / 60-table schema/model parity and synthetic startup HTTP 200 passed. Non-migration source coverage **25.64% (4,654/18,150)**; generated migrations are reported separately.
- Backend CI: [exact-head release](https://github.com/Coxskull/alpha-backend/actions/runs/38345894577) and [companion](https://github.com/Coxskull/alpha-backend/actions/runs/38345894603) passed and agree with local results. [Artifact 11788925580](https://github.com/Coxskull/alpha-backend/actions/runs/38345894577/artifacts/11788925580) SHA-256 21ab6cafd932992bab67fbc49c0517b30fabccb78e3a73687c34ca519d9e13a0, expires 2026-11-12.
- Frontend: Node **20.20.2**, Next **16.4.0**, full TypeScript check, production build and changed-file ESLint passed. The unchanged entrepreneur configuration page has one existing unused loadConfiguration warning; no new lint errors remain. Eight pure contract cases passed locally and in CI.
- Eight local mocked Chromium scenarios passed: exact net/currency/reference with gross rejection; zero-net/carry-forward; stale refresh/manual reconfirmation; held source; dispatcher denial; unknown POST reconciled as recorded without repeat; mobile fit/focus/escape; entrepreneur threshold. All API calls were intercepted at fixed loopback with synthetic users/tokens, actual authorization headers were checked and other outbound browser requests were blocked. No browser case called a real backend/provider.
- Frontend [exact-head CI](https://github.com/Coxskull/Alpha-Auto-MVP/actions/runs/38348889250) passed fresh install, production audit, typecheck, changed-file lint, eight contract cases and build. Browser cases were run locally, not in that CI workflow. No frontend line-coverage percentage is claimed.

The screenshot is a **synthetic local acceptance preview**, not production data. A visual check corrected the existing portal theme's input/button overrides and verified white readable input/button text and bounded checkbox sizing. Source worktrees remained clean; disposable PostgreSQL containers and local frontend/browser processes were removed/stopped.

## Dependency findings and remediation

The exact original frontend base d351b81c8ea0989b7fcc633f8c4caf755c0a54b2 had **19 production audit entries: 1 critical, 7 high, 11 moderate**. Next/eslint-config-next were updated to 16.4.0 and Axios to patched 1.20; compatible locks were refreshed. Remaining React Admin decoder findings were removed with unused React Admin/data-provider dependencies and their unreferenced OrdersShow scaffold. The active orders route imports the separate OrdersList component. Existing order components relied on a transitive icons package; @mui/icons-material is now explicitly declared.

The exact final frontend **production audit is clean (zero known entries)**. Full graph audit still reports five high development entries propagating from a single upstream braces advisory, [GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm). Its latest published 3.0.3 is affected. An incompatible eslint-config-next downgrade was not forced. This dev toolchain issue remains an independent-review/future-remediation item; no complete all-dependency security clearance is claimed.

Development logs retain the initial backend fixture selecting both platform and recovery rows, a React effect lint issue, initial audits, and the icons-dependency type error. Those were corrected before the stated final gates. Only explicitly final/ directories establish the current-head results.

## Evidence and next gate

validation-summary.json records both heads, tests, CI, coverage, audit scopes and command exits. exact-head-evidence.zip preserves final local/CI logs, TRX, coverage, schema/startup SQL and manifests, browser results/screenshot, audit before/after/residuals, source runner/harness and marked development logs. SHA256SUMS verifies deliverables. All execution used disposable PostgreSQL or loopback mocks with inherited hosted/provider bindings removed.

PR40 depends on still-unmerged PR39. After an authorized PR39 merge, retarget PR40 to main before merging and verify the resulting main commit. Frontend PR1 depends on the Phase 3 backend quote/error contract. Neither draft authorizes merge/deployment. Human accounting/operator/security acceptance, the development advisory, approved isolated hosted schema/roles/Auth/Storage/TLS and provider sandbox contracts remain pending. No production access, merge, deployment, live transfer or credential changes occurred.
