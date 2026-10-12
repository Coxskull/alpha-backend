# PayPal Capture and Refund Lifecycle Hardening Design

**Evidence ID:** `ALPHA-EV-20261012-PAYPAL-LIFECYCLE-DESIGN`  
**Status:** Design proposal; implementation and validation remain pending  
**Scope:** Canonical Alpha Auto backend, `Coxskull/alpha-backend`  
**Related work:** [PR #14 — PayPal capture response verification](https://github.com/Coxskull/alpha-backend/pull/14), [Issue #15 — lifecycle hardening](https://github.com/Coxskull/alpha-backend/issues/15)

## Why a design gate is required

Source review found that capture and refund calls currently reach PayPal before the database transaction protects local state. Two requests can therefore observe the same initial payment/refundable balance and invoke the provider concurrently. The refund handler also treats a successful HTTP response as a completed refund without requiring the provider's refund status to be `COMPLETED`.

These are lifecycle risks inferred from the code path. They are **not evidence that a duplicate live charge or refund has occurred**.

## Non-negotiable invariants

1. A single Alpha order can create at most one successful local payment-completion transition.
2. A retry of the same logical capture must reuse the same provider idempotency key and must not duplicate status history, audit records, settlement creation, or referral commissions.
3. A refund request has a stable client idempotency key. A retry of that logical refund returns/reconciles the existing refund attempt; it does not create a second refund.
4. The sum of completed and in-flight refund reservations must never exceed the captured amount.
5. Only a provider refund explicitly confirmed as `COMPLETED` increments the completed-refund total or triggers full-refund effects. `PENDING`, `FAILED`, `CANCELLED`, unknown, and malformed statuses must not be represented as completed.
6. Unknown outcomes after a provider timeout are reconciled by provider request/refund identifiers before another provider operation is attempted.
7. Provider HTTP calls must never be made by automated tests against live PayPal, and tests must not write to production databases.
8. Keep campaign controls unrelated to payments unchanged: `CampaignReady=false` and `Delivery=NOT_SENT`.

## Proposed persistence model

Prefer a dedicated `payment_refund_attempts` table rather than storing multiple partial-refund attempts in the single `payments.refund_reference` field. The existing fields can remain as a compatibility summary during migration.

Suggested fields:

- `id` UUID primary key
- `payment_id` UUID foreign key
- `order_id` UUID foreign key
- `idempotency_key` text, unique within payment (unique index)
- `provider_request_id` text, unique when present
- `provider_refund_id` text, unique when present
- `requested_amount` numeric and `currency` text
- `status` constrained to `reserved`, `submitted`, `pending`, `completed`, `failed`, `unknown`, `cancelled`
- `provider_status` text nullable, preserving the provider's reported status
- `created_at`, `updated_at`, `completed_at` timestamps
- sanitized failure/reconciliation metadata (never secrets or raw credentials)

A capture attempt/operation record should similarly preserve the Alpha order ID, stable provider request ID, provider order/capture IDs, attempt state, and timestamps. Add database uniqueness for the one successful capture transition per order and for provider identifiers where available. Final constraints and migration strategy require review against the live schema before implementation.

## Capture protocol

1. Begin a short database transaction and atomically reserve the capture operation for the order, using a unique operation key derived from the stable Alpha order ID. If an operation already exists, return its known result or route an unknown/in-progress result to reconciliation; do not blindly issue a second capture.
2. Commit the reservation before the network call. Do not hold a database row lock across an unbounded provider request.
3. Call PayPal using a stable `PayPal-Request-Id` where supported by the current PayPal endpoint/contract. Verify the exact endpoint's idempotency semantics and retention window before relying on them.
4. Persist provider response/unknown outcome. On a confirmed completed capture, validate order ID, Alpha custom/reference IDs, capture ID, amount, and currency using the validator from PR #14.
5. In one database transaction, conditionally transition the payment/order to paid and create status history/audit records. Settlement and referral side effects must be idempotent (unique source-operation key or equivalent); a retry must not duplicate them.
6. If the process crashes after provider success but before local commit, reconcile by provider order/capture state and the stored operation key before retrying.

## Refund protocol

1. Require a caller-supplied idempotency key for each logical refund. Persist a refund-attempt reservation under a database transaction with a uniqueness constraint.
2. Atomically calculate available refundable balance as captured amount minus completed refunds minus active reserved/pending/unknown attempts. Reject a reservation that would exceed the available balance.
3. Call PayPal with a stable provider request ID derived from or mapped to the persisted attempt, after verifying current PayPal refund endpoint idempotency support.
4. Persist the provider response and map provider status explicitly:
   - `COMPLETED`: mark the attempt completed and increase completed refund totals exactly once.
   - `PENDING`: retain the reservation, do not increase completed totals, and schedule/allow reconciliation.
   - `FAILED` or `CANCELLED`: mark terminal failure/cancellation only when provider semantics confirm it; release the reservation according to a reviewed policy.
   - timeout, malformed response, or ambiguous status: mark `unknown`; keep the amount reserved until reconciled.
5. Determine fully-refunded state from the sum of completed refund attempts, not from the requested amount or an unconfirmed provider response.
6. Apply financial/settlement adjustments and full-refund referral reversals once, transactionally and idempotently. Define partial-refund commission and settlement adjustment policy before enabling those effects.
7. Add a reconciliation worker or admin-controlled reconciliation operation to resolve `pending`/`unknown` attempts from provider state. No automated retry may create a new logical refund.

## Required tests before merge

- Same capture key replayed sequentially and concurrently; exactly one paid transition and one set of side effects.
- Capture provider success followed by local transaction failure; retry reconciles rather than duplicates provider capture or side effects.
- Capture provider timeout/unknown outcome and mismatched order/capture/amount/currency.
- Same refund idempotency key replayed sequentially and concurrently; one provider operation and one ledger attempt.
- Different concurrent partial refunds whose combined amount exceeds the captured amount; reservations enforce the limit.
- Refund response statuses `COMPLETED`, `PENDING`, `FAILED`, `CANCELLED`, unknown, and malformed JSON.
- Timeout after provider acceptance, subsequent reconciliation, and safe retry behavior.
- Full and partial refund totals; duplicate completion event; settlement/referral effects exactly once.
- Database rollback, unique-constraint races, and multi-request concurrency against PostgreSQL (not only an in-memory provider).
- Verify capture IDs and refund endpoint identifiers are not confused.

Use mocked HTTP handlers/provider responses and an isolated test database. Tests must assert zero live provider calls and no production writes.

## Delivery sequence

1. Review this design and confirm current PayPal `PayPal-Request-Id` behavior/retention from the applicable API contract.
2. Inspect actual production schema/migrations and design additive migration/backfill; do not run it against production.
3. Implement operation/refund-attempt persistence and idempotency constraints.
4. Refactor provider access behind a mockable interface and implement capture/refund orchestration.
5. Add PostgreSQL integration tests and failure-injection tests.
6. Run exact-head CI; inspect logs and test counts; request independent human review.
7. Only after explicit approval, plan any deployment and production migration separately.

## Current status

This pull request records the design and acceptance criteria only. It does not change runtime payment behavior, perform live PayPal calls, modify production data/configuration, merge code, or deploy the service. It is not a substitute for implementation, CI, or independent review.
