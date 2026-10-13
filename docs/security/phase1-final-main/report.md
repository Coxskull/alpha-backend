# Phase 1 closure: exact merged main

**PASS**, 2026-10-13, at `eebeb85d23a9068cd799b763a2f37a405e0d09b3` (PR #37 merge).

PR #37 has brought the PR #36 financial corrections into main. Git ancestry confirms PR32 head e2f969c, PR36 head d47919b and merged correction head 907809b are all included. The merged main tree is identical to the independently tested PR37 source head 907809b. The earlier Phase 1 missing-corrections blocker is resolved at this SHA; the old blocked report remains a historical record.

## Verification

- .NET SDK 8.0.425, Release builds with warnings as errors: zero warnings/errors.
- 175 tests passed: 77 unit and 98 disposable PostgreSQL/HTTP integration; zero failures/skips.
- Application, unit and integration dependency graphs restored with lockfiles; current transitive vulnerability audits reported no known vulnerable packages from current sources.
- Nine applied migrations, 57 model tables, zero missing tables or model/schema mismatches.
- Non-migration source coverage: 24.40% (4,366/17,895). Assembly coverage including generated migrations: 61.93%; it is not the runtime source coverage measure.
- Synthetic local startup returned HTTP 200. Health alone does not establish hosted database readiness.
- Source worktree remained clean. Disposable PostgreSQL container/databases were removed after validation.

The confirmed corrections include aggregate same-order refunds, append-only audit/recovery history, durable refund-conflict backfill/holds, and fail-closed recovery when source evidence is missing. Their regression tests are now part of main. Provider responses and credentials used by tests are synthetic. Inherited database/provider runtime bindings were stripped before dotnet execution. PostgreSQL 16.15 used the pinned image postgres:16@sha256:ca0bd484cb98bf4b24eb1010e73fb3fcbd6714d240fbc1a10eea5b7dbecb641d, fixed loopback targets and separate disposable databases.

## Evidence

`validation-summary.json` records ancestry, exact head, test counters, schema and coverage results and every command exit code. `exact-main-evidence.zip` contains build/restore/audit logs, TRX, full coverage, generated model/migration SQL, schema observations, startup logs and runner source. `SHA256SUMS` covers all published files. These are fresh merged-main local logs, not reused PR-head results.

## Next development gate

Phase 2 can proceed from this verified baseline: define and implement explicitly authorized administrator conflict review and audited resolution, unknown-refund-ID and missing-source recovery procedures, and historical exception handling. Existing unresolved financial holds must continue to fail closed. A resolution mechanism needs separate durable resolution evidence rather than silently removing the current immutable conflict flag. Use synthetic fixtures and reviewable draft changes.

Phase 1 passing does not close independent human security/accounting review, target-specific hosted schema/grants/Auth/Storage/TLS acceptance, provider sandbox contracts, credential-owner remediation or manual financial exceptions. This result does not authorize deployment or live transfers. No merge, production access, deployment, live transfer or credential changes were performed by this verification; the user's PR37 merge was observed only.
