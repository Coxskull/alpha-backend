# Phase 1: merged main financial baseline

Date: 2026-10-13. **Disposition: BLOCKED — financial corrections have not reached main.**

GitHub marks PR #32 merged at `1dba047e598c45094f2cf2939752cbf0c5ca7dae` and PR #36 merged at `907809b497f26ec89876090e567868314fb44eae`. Current fetched main is the PR32 merge commit. It includes PR32 head `e2f969c4bc89fab8417d93e72b43bed388808d31` but neither reviewed PR36 head `d47919b6a8b6d54d3d21e63889021b294fd94b87` nor PR36's merge commit. PR36 merged into PR32's former source branch after PR32 had already merged into main. Closing both PRs does not propagate the later branch changes into main.

## Exact-head results

| Check | Current main 1dba047 | Correction branch 907809b |
|---|---|---|
| .NET SDK / configuration | 8.0.425 / Release | 8.0.425 / Release |
| Builds with warnings as errors | PASS, zero warnings/errors | PASS, zero warnings/errors |
| Unit tests | 77 passed | 77 passed |
| PostgreSQL/HTTP integration | 72 passed | 98 passed |
| Total failures / skips | 0 / 0 | 0 / 0 |
| Migration chain / model tables | 7 / 57 | 9 / 57 |
| Model/schema parity | PASS | PASS |
| App/unit/integration transitive dependency audits | No known vulnerable packages from current sources | No known vulnerable packages from current sources |
| Non-migration source coverage | 24.30%, 4,342/17,866 | 24.40%, 4,366/17,895 |
| Synthetic startup health | HTTP 200 | HTTP 200 |

The absence of the audit-history guards, durable payment conflict column/backfill and hold enforcement, cross-payment refund aggregation corrections, and fail-closed missing-source recovery checks is a release blocker. The 26 additional integration tests also are absent from main. Passing the smaller existing suite does not certify these missing protections.

The existing merged correction branch `review/warning-remediation-evidence-20261012` at 907809b has a source tree identical to reviewed PR36 head d47919b. No new product changes are necessary: a draft follow-up PR must integrate that branch into main. Its exact merge-commit head was independently rebuilt and tested in this phase. No merge is performed by this validation.

## Evidence and limits

`validation-summary.json` records exact SHAs, TRX counters, schema totals, coverage and command exit codes. `exact-head-evidence.zip` contains both complete logs, TRX, coverage, generated SQL, migration/model observations, synthetic startup, ancestry checks, missing-correction file list and runner source. `SHA256SUMS` verifies the files. Restore used checked-in dependency lockfiles. Tests used PostgreSQL 16.15 pinned to postgres:16@sha256:ca0bd484cb98bf4b24eb1010e73fb3fcbd6714d240fbc1a10eea5b7dbecb641d, separate disposable containers and fixed loopback synthetic databases. Inherited provider/database bindings were stripped from dotnet execution. All containers/databases were removed and both source worktrees stayed clean.

This is local exact-head evidence, not a claim of a newly completed hosted acceptance or independent human signoff. Coverage remains limited. Health does not prove hosted database readiness. Dependency audits report known vulnerabilities from current sources and do not establish complete security. Existing unresolved financial findings apply to main until the corrections are integrated and the resulting main SHA is verified. Phase 2 should start from that verified baseline.

No production access, merge, deployment, live transfers or credential changes occurred in this phase. The user's earlier merges were observed only.
