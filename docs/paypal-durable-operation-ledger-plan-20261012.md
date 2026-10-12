# Durable PayPal payment-operation ledger — proposed migration and integration gate

**Evidence:** ALPHA-EV-20261012-P2O002  
**Scope:** Design and executable test matrix only. No production DDL or runtime behavior change in this PR.

## Verified starting point
- Production `public.payments` has no capture/refund attempt ledger.
- `payments.order_id` is nullable in PostgreSQL; C# currently models it as non-nullable.
- `payments.transaction_reference` has a non-unique index. Existing `refunded_amount` is a single aggregate and cannot independently represent multiple in-flight refunds.
- Existing `referral_transactions`, `entrepreneur_earnings` and `settlement_queue` have unique indexes. Preserve them.
- Current `PayPalController.Refund` calls the provider before opening a database transaction and increments `RefundedAmount` on HTTP success without requiring provider `COMPLETED`. The capture path likewise calls the provider before its DB transaction.
- `PayPalRefundValidator` is currently a helper, not wired into the refund controller.

## Proposed additive schema (requires separate reviewed EF migration)
A `payment_operations` table:
- `id uuid PRIMARY KEY`
- `payment_id uuid NOT NULL REFERENCES payments(id)`
- `operation_kind text NOT NULL CHECK (operation_kind IN ('capture','refund'))`
- `operation_key text NOT NULL` (stable server-side operation identity)
- `provider_request_id text NOT NULL` (stable across retries of **same** operation)
- `provider_reference text NULL` (PayPal capture/refund ID)
- `amount numeric(18,2) NOT NULL CHECK (amount > 0)`
- `currency text NOT NULL`
- `status text NOT NULL CHECK (status IN ('reserved','submitted','pending','completed','failed','unknown','cancelled'))`
- `created_at timestamptz NOT NULL`, `updated_at timestamptz NOT NULL`
- Unique `(payment_id, operation_kind, operation_key)` and unique `provider_request_id`.
- Partial unique `(payment_id)` WHERE `operation_kind='capture'` AND `status IN ('reserved','submitted','pending','unknown','completed')` to prevent duplicate active captures. Do not apply equivalent single-refund uniqueness: multiple sequential partial refunds must remain possible.
- Review index for `(payment_id, operation_kind, status)`; ensure pending/unknown attempts reserve refundable balance.

## Safe controller sequencing
1. Authorize caller and validate order/payment/currency/amount.
2. Begin a **short** DB transaction; lock the payment row (or use an equivalent serializable reservation strategy); calculate remaining refundable balance after subtracting both completed refunds and all active pending/unknown reservations; insert/reuse unique operation row; commit. Never hold DB locks while calling PayPal.
3. Send PayPal request with the ledger's persistent `provider_request_id`. Retry the same operation with the **same** request ID. Never derive refund idempotency only from amount/capture ID: identical legitimate partial refunds must remain distinct.
4. Validate the provider response: only `COMPLETED` may advance local completed refund totals and reverse commissions. `PENDING` remains reserved; ambiguous timeouts become `unknown` and require reconciliation.
5. In a new transaction lock payment/operation rows, transition once to `completed`, apply balance/status/history/referral effects exactly once, and commit. Replayed completed responses must be no-ops.
6. Reconcile `pending`/`unknown` operations using provider GET/webhook verification with signature checks. Do not release a reservation solely on timeout or an ambiguous HTTP failure.
7. Audit transitions without secrets or sensitive provider payloads. Return a retry-safe operation identifier to the caller.

## Mandatory nonproduction tests
- 20 parallel identical capture requests => one active reservation and one local completed effect.
- 20 parallel refunds with total requested amount above paid amount => no over-refund.
- Two distinct valid equal-amount partial refunds => both allowed sequentially with distinct operation IDs.
- Duplicate refund request key => same operation, never double-credit.
- Provider `PENDING`, `FAILED`, `COMPLETED`, malformed response and timeout; assert no premature completion.
- Provider completed response replay and process crash between provider success and DB commit => reconciliation produces exactly one local effect.
- Refund-to-full transition reverses commissions exactly once and does not duplicate audit/status history.
- Unique constraint races, transaction rollback, and mixed currency rejection.
- Run against isolated PostgreSQL with mocked PayPal; migration up/down and index parity; zero production customer data.

## Release gates
No production DDL until: implementation PR + EF migration reviewed, CI/integration tests passed, production backup/PITR and rollback/reconciliation plan confirmed, human approval, and staged deployment. No migration or database mutation was performed for this evidence.
