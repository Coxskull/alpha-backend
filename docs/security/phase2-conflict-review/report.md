# Phase 2: immutable financial conflict review and two-administrator attestations

Date: 2026-10-13. Draft PR #38 at **442f3ed5c946b08580f0d6934dfa54f2f943cba8**, based on verified merged main eebeb85d23a9068cd799b763a2f37a405e0d09b3.

**Review/attestation implementation gate: PASS. Hold release: intentionally separate and not implemented**, per the user's selected policy: two distinct administrators approve evidence; implement release in a separate reviewed step.

## Implemented behavior

A bounded administrator-only queue identifies durably conflicted payments. A first administrator submits evidence with a stable idempotency key; another active administrator supplies an independent second attestation. Both reviewers must be active when the second attestation is accepted. Reviewer identity is derived from authentication and rechecked against active role state, not supplied in the request body. Identical retries have one effect; changed evidence/reviewer/key reuse cannot overwrite the immutable original.

A SHA-256 fingerprint binds attestations to order customer/currency/status, every same-order payment, provider operation, allocation/recovery offset, financial row and known conflict audit evidence. Changed evidence requires a new review; read status distinguishes awaiting_second_administrator, two_attestations_recorded and evidence_changed. Stable same-order payment locks and unique indexes serialize competing attempts. Two new tables have restrictive source/actor foreign keys, insert guards against invalid identities/empty evidence/self-approval and append-only UPDATE/DELETE/TRUNCATE guards.

No endpoint clears the durable conflict flag, releases financial holds, changes payment/refund amounts, posts a provider request or authorizes a payout. Unknown-provider-refund-ID, missing-source and historical exception procedures are documented in the feature runbook; they retain holds and require owner-reviewed evidence/repair plans. A later resolution implementation needs immutable version-bound authorization and concurrency protection against new conflicting evidence.

## Exact-head verification

- .NET SDK 8.0.425 Release builds with warnings as errors: zero warnings/errors.
- **191/191 tests passed**, 77 unit + 114 PostgreSQL/HTTP integration; zero failed/skipped. Sixteen integration cases were added to the 175-test verified baseline.
- HTTP checks: unauthenticated/wrong-role rejection, active admin queue, bounded pagination, missing evidence rejection, self-approval rejection, independent approval, inactive-token rejection, hold retention.
- Database/service checks: immutable review/approval mutation guards, direct SQL self-approval rejection, idempotent/changed retries, same and competing concurrent approvals, invalidation after another payment or order-currency change, first reviewer inactivation, wrong/inactive service actors and unconflicted-source rejection.
- Ten migrations / 59 model tables; zero schema/model mismatches or missing model tables.
- Application/unit/integration transitive NuGet audits: no known vulnerable packages from current sources.
- Non-migration source coverage **25.14% (4,531/18,022)**; assembly coverage including generated migrations **64.98%**, reported separately.
- Synthetic local startup health returned HTTP 200. All disposable databases/container were removed and the source worktree remained clean.

The first implementation run passed. A later development run exposed an xUnit assertion analyzer error in a new test; it was fixed using the predicate overload. A final source-review change included order identity/currency in the fingerprint. Only final/ logs in the archive establish this exact-head gate; earlier development logs are retained separately. The final runner stops on a nonzero command result. No product startup guard was bypassed.

## Evidence and limitations

`validation-summary.json` records exact head/base, test counters, schema totals, coverage and command exits. `exact-head-evidence.zip` contains final build/restore/audit/test/TRX/coverage/schema/startup logs, generated SQL, runner source and earlier development logs. `SHA256SUMS` checks published files. Tests used disposable loopback PostgreSQL 16.15 pinned to postgres:16@sha256:ca0bd484cb98bf4b24eb1010e73fb3fcbd6714d240fbc1a10eea5b7dbecb641d; inherited hosted/provider credentials were removed from dotnet execution. Providers were synthetic.

This is a backend API workflow, not an administrator UI. Evidence references are stored as text and never fetched. Fingerprints do not establish provider evidence authenticity; administrators must follow an owner-defined case/evidence standard. Runtime privileges must prevent unrestricted direct SQL forgery/admin access; DB guards enforce immutability and distinct identities, while active-actor and current-evidence authorization remain application responsibilities. Migration owner bypass is possible. Source coverage remains limited.

The additive migration must be reconciled against an approved isolated hosted baseline before hosted use. Down destroys attestation history and is not an approved rollback. Independent human security/accounting review, hosted schema/grants/Auth/Storage/TLS/provider acceptance, owner case standards and evidence retention controls remain pending. No production access, merge, deployment, live transfers or credential changes occurred. The Railway recovery reported by the user was not independently accessed or verified in this feature work.
