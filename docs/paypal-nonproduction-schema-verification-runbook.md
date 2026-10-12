# Non-production PayPal schema parity verification runbook

**Evidence ID:** ALPHA-EV-20261012-P2I001
**Scope:** Coxskull/alpha-backend; Issue #15; draft PR #17
**Environment classification:** Connected Supabase project **Alpha** is PRODUCTION and MUST NOT be used for this verification.
**Status:** Repository inspection complete; non-production database verification pending.

## Source findings (main at c7df5fbf0bbcce9c458ce6a722dabd34d9bf4fe2)

- `Models/Payment.cs` maps `payments` and declares `refunded_amount`, `refund_status`, `refund_reference`, `refunded_at`, provider IDs and gateway response.
- `Data/AppDbContext.cs` explicitly maps core Payment fields, but not all refund fields; `[Column]` attributes may still map those properties. Do not label this a mismatch without inspecting EF model metadata and a database schema.
- `DTOs/RefundPaymentDto.cs` declares `OrderId` and `Amount`, but no stable caller-supplied idempotency key.
- `Models/OrderFinancial.cs` includes `refund_amount`, settlement, financial and payout state fields.
- `Models/SettlementQueue.cs` maps `settlement_queue`; verify whether source-operation uniqueness exists before relying on retry safety.
- `Models/ReferralTransaction.cs` and `Models/Entrepreneur/EntrepreneurEarning.cs` contain `EventKey`; field presence does not prove a unique index.
- These are repository observations only, **not** live schema findings.

## Safe environment requirements

1. Obtain owner approval for an **isolated disposable PostgreSQL** instance with no production credentials, data or network access. A new Supabase development project or branch is acceptable only after cost/approval checks and isolation are confirmed; prefer local PostgreSQL.
2. Record the non-production environment name, PostgreSQL version, repository commit SHA, UTC collection time and operator.
3. Initialize from approved, reviewed **schema-only** SQL/migrations. Do not restore production rows, customer information, tokens, secrets, webhook payloads or payment references.
4. Use a least-privilege read-only database role for inventory queries. Run `docs/sql/paypal-schema-parity-readonly.sql` against this disposable environment.
5. If migration history exists, collect **migration IDs only**. Review outputs for sensitive identifiers before uploading them.
6. Compare EF model metadata and checked-in migrations to actual table columns, numeric precision, nullability, foreign keys, unique/partial indexes, and provider-operation identifiers.
7. Record each mismatch with table/column/index, repository reference, database metadata and remediation proposal; do not modify any schema yet.
8. Validate proposed additive changes only in the disposable environment, with mocked PayPal provider calls and parallel capture/refund tests. No production deployment or migration.

## Go/no-go gates

- [ ] Environment explicitly certified non-production, with production access excluded.
- [ ] Sanitized schema-only source and migration history verified.
- [ ] Payment, order financial, settlement and referral schema comparison completed.
- [ ] Capture/refund idempotency and uniqueness plan reviewed.
- [ ] Tests pass against disposable PostgreSQL and mocked provider.
- [ ] Exact-head CI green, human security review completed.
- [ ] Separate authorization obtained before any merge or deployment.

**Current decision: NO-GO for schema migration and payment lifecycle runtime changes.**
This runbook is documentation only. No database queries, migrations, production calls, merges or deployments were executed.
