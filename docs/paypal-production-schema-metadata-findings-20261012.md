# Production payment schema metadata parity findings

**Evidence ID:** ALPHA-EV-20261012-P2J001
**Date:** 2026-10-12
**Source:** Supabase Alpha production project (metadata-only queries), compared with Coxskull/alpha-backend source on main
**Scope:** public schema; no payment/customer rows were read; no DDL or DML executed.

## Confirmed
- `payments` exists with `refunded_amount`, `refund_status`, `refund_reference`, `refunded_at`, and gateway fields.
- `payment_refund_attempts` and `payment_capture_operations` do **not** exist in `public`.
- `referral_transactions` has unique indexes `ux_referral_transactions_event_key`, `uq_referral_transactions_event`, and order/service credit constraints.
- `entrepreneur_earnings` has `ux_entrepreneur_earnings_event_key` and composite uniqueness.
- `settlement_queue` has `ux_settlement_queue_financial_payee` on (order_financial_id, payee_type).
- `order_financials` has partial unique index `ux_order_financials_order` on non-null order_id.
- `payments.order_id` and `payments.transaction_reference` are indexed but not uniquely indexed.
- `payments.order_id` is nullable in production; C# Payment.OrderId is non-nullable Guid.
- Production `order_financials.auto_parts_commission_rate` is numeric(10,6); AppDbContext configures HasPrecision(8,4).
- Supabase migration listing API returned an empty migration list; this does not establish absence of EF migration history in the database.

## Decisions
1. Preserve existing uniqueness protections; do not blindly add overlapping constraints.
2. Treat the precision and nullability differences as **confirmed metadata/model discrepancies**, not proof of a current application failure.
3. Do not tighten `payments.order_id` nullability or change production numeric precision without inspecting affected flows and testing a migration.
4. Do not introduce capture/refund attempt tables directly into production until an additive EF migration, idempotency flow, rollback/reconciliation, and concurrent integration tests are reviewed.
5. Keep production metadata inspection read-only; use isolated disposable PostgreSQL for any write/migration tests.

## Required follow-up
- Align EF precision with actual intended domain contract (production numeric(10,6) vs EF (8,4)) in a separate reviewed PR.
- Decide how legacy/unassociated payments are represented before addressing nullable order_id.
- Implement a durable capture/refund operation ledger and stable PayPal idempotency keys in a separate draft PR, then test against disposable PostgreSQL.
- Verify exact-head CI, human approval, and rollback strategy before deployment.

**No production database mutation or PayPal provider call was performed.**
