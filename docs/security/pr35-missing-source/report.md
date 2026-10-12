# PR #34 review and PR #35 missing-source correction

PR #34 was reviewed at `19c6c2034b5577a78c1a66e39e2e207b1b10537f` and remains unchanged. Draft PR #35 targets its branch and is verified at exact head `2d103cd4a74cadf9b19e21cb08df5c2d3ba0326d`.

## Reproduced finding — bounded integrity gap with missing source data

`RefundAllocationService.RecoverAsync` inner-joined source `order_financials` before checking the durable payment conflict flag. Removing a source financial row therefore removed the claim from the hold check, although it remained in the subsequent recovery-claim query. A supplemental test injected into unchanged PR #34 application source expected payout rejection and failed because no exception was thrown. Direct PostgreSQL observation confirmed a 30 USD recovery claim was consumed, the payment conflict flag remained true, the source financial row was absent, and a synthetic future payout of 20 USD was recorded.

This requires an out-of-band database change or missing historical/operator-managed source data. There is no financial-delete API path in the reviewed controller, and no unauthenticated exploit or production mutation is claimed. The expectation is fail-closed accounting when source evidence is missing, as required by the existing manual-exception policy. The join was pre-existing in the recovery service; this is not attributed as a new API regression introduced by PR #34. Payment-table TRUNCATE CASCADE was also probed in a rolled-back transaction; existing append-only recovery-history guards rejected it.

## Correction

The recovery hold query no longer requires a source financial row in order to inspect payment conflicts. Missing financial records or missing source order associations now require manual review before recovery. Existing payee/role/currency isolation and legacy/durable conflict gates remain. Three PostgreSQL regression cases cover missing financial rows with and without conflict flags and unassociated source payments. Failed attempts preserve claim balances and create no offsets or future payout records. No schema migration is added; workflow filters include PR #35's target branch.

## Exact-head verification

[Release evidence](https://github.com/Coxskull/alpha-backend/actions/runs/38253063515) and [companion validation](https://github.com/Coxskull/alpha-backend/actions/runs/38253063540) both passed at `2d103cd4a74cadf9b19e21cb08df5c2d3ba0326d`:

- .NET SDK 8.0.425 Release builds with warnings as errors: zero warnings/errors.
- 77 unit + 95 PostgreSQL/HTTP integration tests: 172 passed, zero failed/skipped.
- Fresh application/unit/integration NuGet audits: no known vulnerable packages.
- Migration/model parity: PASS, 57 tables. Synthetic startup: HTTP 200.
- Non-migration source coverage: 24.35% (4,357/17,892); assembly coverage including migrations: 58.25%.

[CI artifact](https://github.com/Coxskull/alpha-backend/actions/runs/38253063515/artifacts/11748852981) contains exact-head logs, TRX, merged coverage and schema/startup evidence. GitHub-reported SHA-256: `112d68506dfcd5bab8ad26a514147ef42688f699a36c19dd9d5525b433ad363b`; retention: 30 days. Its recorded head matches PR #35. The local build and all three targeted regression cases also passed.

## Evidence and disposition

`review-19c6c20.zip` preserves the failing supplemental probe, TRX, test patch, source identity, observed ledger values and rejected truncation log. Application source was unchanged; injected tests are labeled separately from PR #34's original green 169-test CI. `correction-2d103cd.zip` preserves local Release/targeted regression logs and TRX plus the complete final exact-head CI job log and artifact metadata. Per-file and archive SHA-256 manifests accompany the ZIPs.

The reproduced gap is corrected on the proposed follow-up branch. No additional scoped blocker was reproduced in this review. This is fresh assistant technical verification, not independent human approval or release authorization. Independent review, hosted non-production schema/grants/accounting acceptance, the explicit audited conflict-resolution procedure and existing credential-owner/manual-exception gates remain pending. No production access, live provider calls/transfers, bank debits, credential changes, hosted migration, merge or deployment occurred. The disposable PostgreSQL container and CI service containers were removed after testing.
