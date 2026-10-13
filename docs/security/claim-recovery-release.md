# Existing-claim recovery release

This reviewed step is scoped to an explicitly selected, existing future-recovery allocation. Original-order payouts and new allocations remain held. The original payment conflict flag, financial conflict status and historical paid records are never cleared or rewritten.

`POST /api/financial-conflicts/reviews/{reviewId}/recovery-releases`

```json
{ "allocationId": "existing-allocation-guid", "evidence": "Case reference and justification for this claim-only authorization" }
```

An active administrator who supplied one of the two distinct attestations records the release. Both attesting administrators must still be active. The reviewed payment must own the completed refund operation behind the claim; the allocation must be future recovery, positive and still outstanding. The complete reviewed financial snapshot must still match. The response identifies immutable release evidence, the scope `existing_future_recovery_claim_only`, its current effectiveness and `originalOrderHoldReleased: false`.

Identical review/claim/actor/evidence retries return the same record; a changed actor or statement cannot replace it. A stale retry returns historical evidence with `effective: false`, without reauthorizing the claim. A new source-evidence version requires a fresh review, independent attestation and a new release record. The unique key is review plus allocation, permitting immutable superseding authorizations without replacing history.

The recovery fingerprint retains order identity/currency/status, every same-order payment/provider operation, immutable allocation identity/payee/type/currency/amount/evidence, financial source rows, original paid queues/payouts/earnings/batches and known conflict audit events. It excludes ordinary recovered-balance progress and recovery offsets, so approved partial/zero-net recovery can continue. The first/second review fingerprint still covers the complete snapshot, including recovery progress, and is checked at release time. Fingerprint version 2 adds paid-source history. Earlier version-1 attestations remain immutable historical records but become stale; a fresh two-person review is required before release. No old review is silently upgraded.

Recovery preflights every outstanding same-payee/type/currency claim before changing any balance or recording any offset. A conflicted source is eligible only when that exact claim has a matching source fingerprint. New provider/source evidence invalidates its old authorization. Missing source order/financial evidence and other unreleased conflicted claims still block recovery. Unrelated payees, roles and currencies remain isolated. Zero-net, partial progress and concurrent payouts retain existing exact-net confirmation and atomic accounting behavior. Quotes remain advisory.

Release takes stable source-order payment locks, then the existing payee advisory lock. Recovery retains the payout transaction/payee lock. Stripe evidence processing takes the affected payee locks before committing changed evidence, ordering contradictory evidence against payout accounting. Original-order payout and allocation guards are unchanged and do not recognize these releases. Administrative out-of-band source changes require separate operational coordination; runtime roles must not have unrestricted SQL write/admin access.

The new release table has restrictive references, immutable UPDATE/DELETE/TRUNCATE guards and insert checks for two distinct attestations, the selected claim's reviewed payment and an attesting actor. Application code additionally checks active identities and source fingerprints. Migration-owner bypass and direct SQL forgery with excessive privileges remain outside these application guarantees. Use a constrained runtime identity and independently reviewed operator procedures.

The additive migration must be rehearsed against an approved isolated hosted baseline before hosted use. Down drops release history and is not an approved operational rollback. This draft changes no provider amounts, authorizes no live transfers, performs no production operations and supplies no UI. Independent security/accounting acceptance and explicit rollout authorization remain required.
