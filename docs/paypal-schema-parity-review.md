# PayPal Persistence Schema Parity Review

**Evidence ID:** `ALPHA-EV-20261012-PAYPAL-SCHEMA-PARITY`  
**Status:** Repository-only review complete; non-production schema comparison blocked pending an approved sanitized schema-only export  
**Scope:** Canonical Alpha Auto backend, `Coxskull/alpha-backend`  
**Related:** [Issue #15](https://github.com/Coxskull/alpha-backend/issues/15), [PR #14](https://github.com/Coxskull/alpha-backend/pull/14), [PR #16](https://github.com/Coxskull/alpha-backend/pull/16)

## Executive result

The checked-in source establishes a **repository migration/model parity concern** for payment/refund work. It does not establish the exact schema deployed in any hosted environment. No database connection was made and no database was read or changed as part of this review.

**Gate decision: do not generate or apply a refund-ledger migration yet.** First compare the current EF model and checked-in migration history with an approved, sanitized schema-only export from a non-production environment. Confirm the export's environment and capture timestamp. Do not include rows, customer/payment data, secrets, credentials, connection strings, or production identifiers.

## Evidence reviewed

| Evidence | Finding | Interpretation |
|---|---|---|
| `Models/Payment.cs` on `main` | Declares `refunded_amount`, `refund_status`, `refund_reference`, and `refunded_at`, alongside payment/provider identifiers. | These are model declarations, not proof that every environment has matching columns. |
| `DTOs/RefundPaymentDto.cs` on `main` | Contains `OrderId` and `Amount`; no caller-provided idempotency key. | Request contract needs a deliberate backward-compatibility/API decision before adding required idempotency. |
| `Data/AppDbContext.cs` on `main` | Contains an explicit Payment entity mapping; the mapping reviewed did not explicitly configure the refund-specific fields. | Compare conventions, attributes, fluent configuration, generated SQL, and the actual non-production schema before deciding whether fields are missing or convention-mapped. |
| `Migrations/20260609065938_InitialCreate.cs` | Creates a limited initial set of tables including users, orders, suppliers, drivers, status history, audit logs, and delivery proof; it does not create a payment/refund-attempt ledger. | The initial migration alone is not a complete schema representation of the current model. |
| `Migrations/20260609152717_OrderRelations.cs` | The checked-in migration identified by repository search is for order relations. | It does not establish that the proposed refund-attempt ledger or idempotency uniqueness constraints exist. |
| `payment_refund_attempts` proposal in lifecycle design | Suggested durable attempt ledger and uniqueness rules. | Proposed design only; no implementation or migration has been applied. |

## Required non-production evidence package

Provide or generate this package in an approved non-production environment, then attach it to the issue/PR without secrets or row data:

1. **Schema-only export** (DDL only; no data) for the payment-related tables and their indexes/constraints, including `payments`, `order_financials`, settlements, referral records, and any existing provider-operation/refund tables.
2. **Migration inventory** from that same environment: migration history table contents limited to migration IDs, plus the repository migration files used to create the environment. Do not include business rows.
3. **Environment declaration**: non-production environment name, database engine/version, export timestamp, and confirmation that credentials and data rows have been excluded.
4. **Model metadata** from the same checked-out commit: EF Core model/migration status or generated create-script output, reviewed locally without connecting to production.
5. **Constraint inventory**: primary keys, foreign keys, unique indexes, partial indexes, numeric precision, nullability, and timestamp semantics for the relevant tables.

## Comparison checklist

- [ ] Map each Payment property to an actual non-production column and confirm SQL type/nullability/default.
- [ ] Confirm whether the explicit fluent mapping overrides or omits any attribute/convention mapping.
- [ ] Compare checked-in migration IDs with the non-production migration history.
- [ ] Check whether the model snapshot exists and corresponds to the current model; do not infer snapshot absence from a failed path lookup alone.
- [ ] Identify existing unique keys for provider order IDs, capture IDs, refund IDs, and logical operation/idempotency keys.
- [ ] Check whether settlements and referral side effects have stable unique source-operation keys.
- [ ] Verify current provider refund/capture identifier fields and compatibility with PR #14's capture-ID validation.
- [ ] Review any existing manual SQL or out-of-band schema changes documented by the project owner.
- [ ] Record every mismatch as a separate finding with exact table/column/index and source evidence.

## Implementation gate after parity review

Only after the evidence package is reviewed should a separate implementation PR introduce an additive migration and code for capture-operation reservation, refund-attempt ledger, idempotency, explicit provider status handling, and reconciliation. The migration must be generated from the verified model and tested against a disposable PostgreSQL database initialized from the approved non-production schema shape.

Required automated tests include duplicate/concurrent capture, duplicate/concurrent partial refunds, over-refund prevention with in-flight reservations, provider pending/failed/unknown statuses, provider timeout after possible success, database rollback, and idempotent settlement/referral effects. All provider interactions must be mocked or sandbox-isolated; no production database writes or live PayPal calls.

## Safety and release status

- No database was accessed or mutated.
- No live PayPal call was made.
- No runtime code or migration was changed by this review artifact.
- No merge or deployment occurred.
- PR #14 and PR #16 remain review gates; this document does not imply independent human approval or hosted acceptance.
- Preserve unrelated campaign safeguards: `CampaignReady=false` and `Delivery=NOT_SENT`.

## Next action

The repository-only review is recorded. The next dependency is the approved sanitized **non-production schema-only export**. Once available, complete the table-by-table comparison and record a go/no-go decision for the additive migration before implementing runtime idempotency changes.
