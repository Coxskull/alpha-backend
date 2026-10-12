# PR #33 fresh technical review

Reviewed head: `40a633910f12d3687b463f70e9acff93746b2f2b`; base: `8946949623433e8317be7b6814e93086defda818`. GitHub still reports PR #33 as open/draft at that head. The application source in the review checkout is unchanged; only separately labeled supplemental tests were injected. This is a fresh assistant review of its earlier corrections, not organizationally independent human sign-off. No GitHub approval was submitted.

## Finding: HIGH — valid Stripe evidence silently clears an unresolved conflict

At [StripeWebhookController.cs:944–946](https://github.com/Coxskull/alpha-backend/blob/40a633910f12d3687b463f70e9acff93746b2f2b/Controllers/StripeWebhookController.cs#L944), the `currentCompleteEvidence` branch sets `refund_review` even when the order previously had `refund_evidence_conflict`. This is inherited from PR #32; PR #33 changes aggregation immediately above this branch but does not introduce this state transition.

Two signed HTTP/real-PostgreSQL probes reproduce the problem:

1. Record a verified complete refund list for payment A.
2. Send signed contradictory evidence with a mismatched refund parent. The order becomes `refund_evidence_conflict` as expected.
3. Send a valid complete list for either the same payment A or another payment B on the order.
4. Both cases end at `refund_review` without any manual resolution. The probe expected the unresolved conflict hold to remain and fails in both cases.

This is material because [RefundAllocationService.cs:139–146](https://github.com/Coxskull/alpha-backend/blob/40a633910f12d3687b463f70e9acff93746b2f2b/Services/RefundAllocationService.cs#L139) blocks future recovery only while the source financial status equals `refund_evidence_conflict`. Original-order payout holds remain, but that does not preserve the separate protection against recovery from future orders. The probes demonstrate the state loss; the recovery consequence is established by source inspection, not by performing a real transfer.

Related untested source paths also assign generic `refund_review`: [PaymentOperationService.cs:40](https://github.com/Coxskull/alpha-backend/blob/40a633910f12d3687b463f70e9acff93746b2f2b/Services/PaymentOperationService.cs#L40) during reservation and [PayPalController.cs:197](https://github.com/Coxskull/alpha-backend/blob/40a633910f12d3687b463f70e9acff93746b2f2b/Controllers/PayPalController.cs#L197) on completed refunds. They require the same preservation analysis before a fix is accepted; they are not presented as executed reproductions.

Recommended correction: keep unresolved conflicts persistent across valid replays, other payments and gateways; clear them only through an explicit, evidenced, audited resolution. Prefer durable per-payment conflict state with an order-level aggregate hold so one valid payment cannot resolve another payment's conflict. Test that subsequent allocation and same-payee future payout recovery remain blocked. This review does not change PR #33's application code or implement an unreviewed conflict-resolution process.

## PR #33 correction checks

No additional regression attributable to the ten-file PR diff was identified in this scoped review. Refund sums exclude the in-memory current payment from the database sum and add its updated total exactly once. PayPal/Stripe acquire the relevant order's existing payment rows in stable ID order, matching payout/settlement locking; currency checks precede commit. The supplemental multi-payment CI cases cover both gateways. This does not claim an exhaustive proof of concurrency safety.

The financial history migration was independently exercised through downgrade to `20261012071522_AttributedRefundRecovery` and reapplication. Existing audit rows survived, migration history transitioned correctly, and the update guard returned SQLSTATE 23514 after reapplication. The final supplemental round-trip test passed with warnings treated as errors. Its first draft used analyzer-rejected collection assertions; that harness issue was corrected before the final successful execution, and both logs are retained.

A disposable NOLOGIN, non-superuser runtime role was granted table UPDATE/DELETE privileges. Both attempted audit mutations returned SQLSTATE 23514 and preserved the original row. Attempts to disable triggers and set replica mode returned SQLSTATE 42501. The synthetic role was removed. Owners/superusers remain able to intentionally alter the schema; hosted runtime grants must be accepted separately.

Workflow trigger/checkout changes retain read-only token permissions, avoid persisted Git credentials and consume no production secrets. [Exact-head CI](https://github.com/Coxskull/alpha-backend/actions/runs/38247779853) and [companion validation](https://github.com/Coxskull/alpha-backend/actions/runs/38247780077) passed at the reviewed head: 77 unit + 83 PostgreSQL/HTTP integration tests, zero failed/skipped; zero build warnings/errors; fresh dependency audits; migration/model parity; synthetic startup. Non-migration source coverage is 24.30% (4,343/17,874). [CI artifact](https://github.com/Coxskull/alpha-backend/actions/runs/38247779853/artifacts/11745894660) has GitHub-reported SHA-256 `03391f24dfb9f63d8a2ac5c67fd005bdd138185d8187e140438a65491db7c82f` and records the same head. The existing green CI suite does not include the two new failing review probes.

## Disposition and evidence

The aggregation/history corrections are supported by the checked evidence. **Overall financial release acceptance remains BLOCKED by the inherited conflict-hold finding**, alongside previously documented hosted acceptance/manual accounting/credential-owner gates. PR #33 remains draft and its head is unchanged by this review. No merge, deployment, production access, live transfer or runtime credential change occurred. All new execution used disposable loopback PostgreSQL and synthetic signed webhooks.

`review-evidence.zip` contains the two failing supplemental cases, the passing migration round-trip TRX/log, least-privilege probe log, the exact test patch/round-trip source, execution identity and SHA-256 manifest. The supplemental tests are labeled separately from the pristine 160-test CI result. This archive is preserved on the evidence branch, not the correction branch.
