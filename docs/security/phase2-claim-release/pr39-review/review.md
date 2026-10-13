# PR39 focused review and version-one upgrade rehearsal

2026-10-13. Reviewed candidate cdffbfc5bdb8acfa710cbed0c7f6eda93f561b66, PR39 targeting main. Current main is 923554c0005c26f0e80e38051d41c9eb1699b65e, containing merged PR38. PR39 remains an open draft and has not reached main.

**Focused code review and disposable upgrade rehearsal: PASS; no additional product correction identified in this scope.** This is an assistant code/automated review, not independent human security/accounting signoff or a formal GitHub approval.

Reviewed authorization from active identities rather than request body; two distinct active attestations and an attesting release actor; same-payment claim ownership; currency agreement; existing paid-source capacity and total attribution; immutable idempotent release history; stable source-payment/payee locking; pending signed source refund gating; all-claim preflight before balance consumption; bounded recovery offsets; source fingerprint invalidation; and retention of original-order/new-allocation holds. Existing exact-head evidence remains 205 passing tests and both successful GitHub checks, with 25.53% source coverage. That entire suite was not unnecessarily repeated against unchanged product code; this review adds a distinct upgrade scenario not covered by the earlier fresh-database gate.

## Additional observed upgrade scenario

The baseline harness references the actual PR38 application at 442f3ed5c946b08580f0d6934dfa54f2f943cba8, whose source tree matches merged main. It provisions ten migrations and synthetic historical supplier payments, refund operations, recovery allocations, an entrepreneur batch, durable conflict state and two version-one administrator attestations using the old service implementation.

The candidate harness references the actual PR39 application. It snapshots all rows in payments, orders, order_financials, supplier_payouts, refund_allocations, audit_logs, financial_conflict_reviews, financial_conflict_approvals and entrepreneur_payout_batches, then applies the eleventh migration. Those nine-table snapshots match exactly after upgrade; monetary and old attestation evidence are not rewritten.

The candidate marks the actual old version-one review as evidence_changed. Attempting release with it fails and inserts no release row. A new version-two review and independent approval permit the existing 30 USD claim release. The transaction-bounded recovery path recovers exactly 30 against a synthetic target, while the original payment conflict remains held. This exercise creates no actual bank/provider transfer and does not claim hosted upgrade or a real payout confirmation.

Both standalone .NET 8 Release harnesses built with warnings as errors, zero warnings/errors. Every final command passed. PostgreSQL 16.15 used the same pinned disposable loopback image and fixed synthetic credentials; inherited hosted/provider bindings were removed. Container/databases were removed. Source worktrees remained unchanged.

The first harness attempt used a synthetic target-kind label longer than the model's 16-character bound; PostgreSQL rejected it. The harness now uses the valid settlement target-kind value. This was a scratch fixture issue; no application correction was needed. The unsuccessful log is retained separately in the evidence archive.

## Evidence and pending gates

upgrade-review-evidence.zip contains both harnesses, runner, exact source identities, logs, baseline IDs, final result and failed scratch attempt. upgrade-review-result.json and execution.json expose the final assertions/exit codes; SHA256SUMS covers published artifacts.

Runtime privilege limits, fingerprint changes after privileged out-of-band edits, provider evidence authenticity, migration-owner bypass, retention/case standards and target-specific hosted acceptance still require independent human/operational assessment. This review does not eliminate those documented limits.

PR39 still needs an authorized merge into main. The original no-merge instruction remains in effect for this agent. Once the user merges it, fetch and verify the resulting exact main SHA before Phase 3. No main verification of the unmerged changes or Phase 3 implementation is claimed. No production access, merge, deployment, live transfer or credential changes occurred.
