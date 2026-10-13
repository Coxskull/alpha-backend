# Financial conflict review: two administrators, holds retained

This phase implements evidence review and two distinct administrator attestations. It does not implement hold release, change payment/refund amounts, create transfers, or resolve a provider operation. The durable conflict flag and all existing payout/allocation/recovery gates remain enforced after both attestations.

## API

All routes below require an authenticated active administrator. Evidence must be a nonblank statement/reference of at most 4,000 characters; do not include credentials, raw authorization headers or unnecessary customer data. References are retained as text and never fetched by the server.

- `GET /api/financial-conflicts?page=1&pageSize=50`: paged durable-conflict payment queue, bounded to 100 rows. Null-order cases remain visible but cannot be attested until an explicitly reviewed source repair exists.
- `POST /api/financial-conflicts/payments/{paymentId}/reviews`: `{ "idempotencyKey": "stable-case-key", "evidence": "Reviewed provider evidence and accounting case reference" }`. Submission is the first administrator's attestation. It retains the actor, timestamp and SHA-256 financial-state fingerprint. Replaying identical actor/evidence/key returns the original review; changed submissions are rejected. New evidence requires a new key.
- `POST /api/financial-conflicts/reviews/{reviewId}/approval`: `{ "evidence": "Independent evidence verification and case reference" }`. A different active administrator provides the second attestation; the first administrator must still be active. Concurrent identical approvals have one effect; a changed statement or competing reviewer cannot replace the recorded approval.
- `GET /api/financial-conflicts/reviews/{reviewId}`: returns immutable attestations, `evidenceUnchanged`, status (`awaiting_second_administrator`, `two_attestations_recorded`, `evidence_changed`) and `holdReleased: false`.

Second approval requires the financial fingerprint to match the first review. It covers all same-order payments, operations, refund allocations/recovery offsets, order financial rows and known conflict audit events in stable identity order. New or changed evidence on another same-order payment invalidates the prior review. Read status is advisory and can change immediately after it is returned. Exact replays of an earlier submission remain historical records; inspect status rather than assuming they attest current evidence.

Stable order-payment locks serialize concurrent submissions/approvals with existing payment workflows. Unique indexes bound one review per payment/key and one second attestation per review. Actor identities and source references use restrictive foreign keys. PostgreSQL rejects review/approval UPDATE, DELETE and TRUNCATE; insert guards reject empty evidence, invalid source identity/hash and self-approval. Actor authorization and fingerprint validation are application responsibilities; application runtime users must not have unrestricted direct SQL write/admin access.

The additive migration introduces two tables. Test it against an approved isolated hosted baseline before hosted use; disposable provisioning evidence is not hosted upgrade authorization. Down drops review history and is not an approved rollback or conflict-resolution procedure. Migration owners can bypass guards; deploy with a separate constrained runtime identity.

## Pending recovery and exception procedures

For an unknown provider refund ID: retain the uncertain operation/reservation, collect provider-issued references and authenticated amount/currency/status evidence through an approved operator workflow, and reconcile only an identified original operation. Never create a replacement provider POST to guess the outcome or insert fabricated completed amounts.

For a missing order/financial source: retain the recovery hold, collect original immutable source/paid-history evidence, and require database/accounting review of a specific repair plan. Neither attestation endpoint recreates or reassociates source rows. Existing amounts, currencies, payee identities and recovery offsets must reconcile before a separately approved repair.

For historical tax/referral/dispute/legacy exceptions: record an external case reference in evidence, retain the hold, and require the responsible accounting/security owners to define the resolution. Two attestations alone do not authorize release.

## Separate hold-release step

A later reviewed implementation must define an append-only resolution event tied to the reviewed evidence version and two authorized reviewers, invalidate authorization after new conflicting evidence, serialize resolution with evidence/payout locks, and preserve the immutable original conflict. It must specify which individual claims/statuses can change and verify accounting outcomes. Do not simply clear `refund_evidence_conflict` or mark the order verified after recording two attestations.

Independent security/accounting review, owner-defined case/evidence standards, retention/access controls and target-specific hosted acceptance remain pending. No production access, deployment, live transfers or credential changes are part of this feature.
