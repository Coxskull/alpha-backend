# Release blocker remediation and rollout gates

This change requires independent review. It does not authorize merging, deployment, hosted database changes or live payments.

## Credential handling

Tracked signing/database credentials have been removed. Startup requires a runtime signing key of at least 32 bytes and DATABASE_URL; the known exposed signing-key fingerprint is explicitly rejected. Missing bindings fail startup. Supply a newly generated random signing key and issuer/audience through the deployment secret store, and rotate/revoke the exposed database credential through its owner. Removing files does not remove historical exposure or revoke an already deployed key. Do not copy a prior key back into the new runtime. No rotation, history rewrite or credential validity check was performed by this change.

Supply Supabase URL/service-role key server-side only. This change does not establish hosted RLS/storage correctness. Configure PayMongo__WebhookSecret and PayMongo__LiveMode explicitly; an unconfigured webhook returns 503 and an invalid signature is rejected before persistence. Configure payment providers through runtime bindings. No real provider credentials are required by tests.

## API changes

- Authentication is required by default. Registration/login, intentionally public referral/service-request entry points, public catalog reads, health and signed webhook routes retain explicit anonymous access.
- Direct confirm-payment is removed (410 for an authorized administrator); clients must use provider-verified completion.
- Payments require an owned customer order or staff authorization. Refund/reconciliation require administrator authorization. Order assignment and administration are staff-only. Customer-owned reads and assigned driver/supplier workflows enforce resource scope; mixed roles use owned assignment unions, never the first claim as an access bypass.
- Private customer/cart/address/vehicle records require self or staff access. Product writes require the caller's supplier profile or staff authority. Password hashes are excluded from JSON responses.
- Suspended memberships cannot regain the legacy primary role. Existing tokens are checked against active user/role state on every authenticated request; newly issued tokens last 30 minutes. Authentication requests are limited to 20/minute per observed remote IP. Validate trusted forwarding/topology and limits in staging before rollout behind a proxy.
- Refund clients must supply a nonempty stable IdempotencyKey (maximum 128 characters). Reusing a key with different fields is rejected. Preserve the same key across retries; do not create a new logical operation because of a timeout.

## Payment safety

A PostgreSQL row lock reserves capture/refund state before any provider operation. A unique operation key and stable provider request ID supplement the reservation. Provider HTTP calls run outside the reservation transaction. Amounts exceeding completed and in-flight balances are rejected. Only matching completed provider responses advance payment/order/financial state, status history and audit records inside one transaction.

Capture/refund timeouts, malformed responses, pending/failed responses and local commit failures retain an uncertain operation and reserve funds. Replaying a key does not repeat a provider POST. Reconciliation reads provider state only and applies a completed result once. If a refund ID is unknown after a timeout, automated reconciliation deliberately cannot invent a replacement operation: operators must obtain provider evidence and review recovery. Do not retry after the provider's idempotency retention window as if no operation existed.

A completed refund records financial RefundAmount, holds payouts and blocks settlement until refund allocation is reviewed. Full refunds reverse referral commissions once. **Partial-refund allocation across supplier/driver/entrepreneur earnings and recovery of already-paid payouts remain an explicit financial-policy/acceptance gate**; no guessed proration is applied. Uncertain/failed attempts also retain the payout hold for review. This is a safe hold, not proof of fully automated refund accounting.

Webhook events receive atomic processing leases; failed events remain retryable and expired leases can be reclaimed. Completed events are acknowledged. Concurrent completion/refund effects are serialized on the payment row. Error persistence clears rolled-back business changes first. PayMongo incoming signatures and retrieved checkout/order/payment amount/currency are verified; Stripe signatures remain required.

## Schema migrations

The checked-in chain now reproduces all 55 current model tables on an empty disposable PostgreSQL database, including payment_operations and webhook processing leases. Verification compares table/column presence, column types/nullability, declared indexes/uniqueness, FK/PK names and the EF snapshot. Legacy unassociated payments have nullable order_id and cannot reserve provider operations.

**Do not apply this provisioning chain to a manually provisioned existing hosted database.** The model-reproduction migration adds historical missing tables/columns, adjusts relationships and has a destructive Down path. An existing database may already contain these objects and out-of-band constraints. First obtain an approved sanitized non-production schema-only export and migration inventory, reconcile its baseline/constraints with the model, and create a reviewed target-specific upgrade/rollback plan. No hosted baseline was obtained or migration applied by this task.

Development may opt into plaintext PostgreSQL only for a literal loopback IP via Database__AllowLocalPlaintext=true. Hosted/non-development connections continue to require VerifyFull. Confirm staging TLS/CA bindings before rollout.

## Signup compatibility

Registration now creates customers through their mapped entity, avoiding the obsolete `phone` column and omitted required columns. The provisioning chain includes community builder profiles/cities and member business activity, plus the five public role catalog definitions. Nullable city state participates in a NULLS NOT DISTINCT unique index so repeated city inserts retain one connection on PostgreSQL 15 or later; verification uses PostgreSQL 16.

Disposable HTTP acceptance covers all five public roles, customer profile persistence, normalized-email duplicate rejection, consent and privileged-role rejection, invalid and valid referrals, login, active-only token claims, ownership and rollback after a late database failure. Driver/mechanic/supplier memberships remain profile_incomplete; registration itself creates no referral rewards. These checks do not certify an existing hosted schema, Supabase signup, email verification, or production readiness. Existing catalogs/tables require target-specific baseline reconciliation before applying the provisioning migration.

Application content excludes the Tests directory to prevent recursive copying of test build outputs and avoid shipping test artifacts.

## Verification

The exact-head evidence workflow builds with warnings treated as errors, restores dependency lockfiles, audits application/unit/integration dependency graphs, runs unit and disposable-PostgreSQL integration suites, merges coverage, checks startup, and retains TRX/schema/identity/log artifacts. Migration/model mismatches now fail the job. Actions and PostgreSQL image are pinned to immutable revisions; runner OS and SDK are explicit. CI does not use production credentials or call live providers.

Remaining operational gates: credential rotation/revocation; hosted non-production schema/TLS/Supabase acceptance; partial-refund and previously paid payout policy; unknown-refund-ID recovery procedure; independent security review and risk-based integration coverage. Obtain separate authorization before any merge or deployment.

Provider contracts checked during implementation: [PayMongo signatures](https://docs.paymongo.com/docs/developer-tools-webhook-setup-management), [checkout resource](https://docs.paymongo.com/reference/checkout-session-resource), [PayPal refund/idempotency/representation](https://developer.paypal.com/api/payments/v2/captures-refund).
