# Isolated acceptance plan — consolidated financial corrections

Candidate: PR #36, `d47919b6a8b6d54d3d21e63889021b294fd94b87`, based on current PR #32 head `e2f969c4bc89fab8417d93e72b43bed388808d31`. This is a prepared plan, not authorization to access production, change credentials, apply hosted migrations, merge or deploy. No hosted execution was performed while preparing it.

## Required inputs before hosted execution

| Input | Acceptance condition |
|---|---|
| Isolated target | Owner identifies a disposable non-production project/database and proves it is distinct from production. Never use inherited DATABASE_URL or Supabase bindings as evidence of isolation. |
| Baseline | Approved schema-only DDL/catalog export, actual EF migration history/product versions, grants, functions, policies and extensions for that isolated target. Static claims in repository reports are not a current baseline. |
| Data | Synthetic financial/account fixtures or explicitly approved sanitized data. No live customer or financial records are required. |
| Database identities | Separate migration owner and least-privileged runtime role. Record runtime effective grants, ownership, superuser/BYPASSRLS status and ability to alter/drop guards. |
| Providers and storage | Loopback/mocked providers and isolated storage by default. Real sandbox contract tests require separately identified test accounts/configuration. No production credentials or live transfers. |
| Review owners | Independent security, accounting, database and client reviewers; approved exception and explicitly audited conflict-resolution procedures. |

## Rehearsal sequence and evidence

1. Record candidate commit, target identity, approved baseline checksum and isolation evidence. Refuse any target whose production separation is unproven.
2. Generate and independently review a target-specific upgrade/backfill script against the actual baseline. The nine-migration provisioning chain is validated for disposable databases and is not an approved hosted upgrade. Refuse lossy monetary narrowing, fabricated monetary defaults, ambiguous payee identities or silent account activation.
3. Snapshot the isolated target and prove snapshot restoration before upgrade. Migration downgrade is not a conflict-resolution procedure: it can remove conflict columns/guards.
4. Rehearse only the reviewed script. Preserve table/column/key/index/function/trigger/grant observations, migration history and checksums. Reconcile the model against the resulting isolated schema and account for every exception.
5. Run the acceptance cases below with the runtime role, synthetic accounts and explicit mock endpoints. Collect sanitized logs, TRX, schema observations, before/after balances and audit records tied to the candidate commit and target snapshot.
6. Have independent reviewers assess results and unresolved exceptions. Record PASS/FAIL/PENDING per gate. No result in this plan authorizes production migration, merge, deployment, live provider activity or credential changes.

## Financial and security acceptance cases

| Area | Required observable result |
|---|---|
| Ledger mapping | Existing reviewed ledger identities, references and amounts survive mapping changes. Fractional/oversized values that cannot be represented are rejected without changing rows. |
| Refund totals | Same-order refunds on multiple payments and gateways reconcile to the sum of confirmed customer refunds. Unsupported currency combinations stay under review. |
| Isolation | Customer ownership, active roles, payee identity/type and currency boundaries hold at HTTP and database layers. Cross-order attribution and wrong-payee offsets are rejected. |
| Idempotency | Replays create one effect; changed keys/amounts/plans are rejected. Unknown provider outcomes retain reservations and reconcile without duplicate POSTs. |
| Concurrent payouts | The same claim cannot be consumed twice. Evidence-state changes on another payment wait for affected payee recovery locks. Failed commits preserve claim balances and create no partial offsets/payouts. |
| Conflict persistence | Signed contradictory evidence creates a durable payment hold. Valid/late/replayed evidence, another payment, PayPal completion, proof updates and generic review statuses cannot clear its effective recovery gate. |
| Missing sources | Missing financial rows or missing order associations block future recovery. No balance is consumed and no offsets/payouts are recorded. Out-of-band source repairs use an explicitly reviewed procedure. |
| Historical backfill | Inspect every order re-held from a legacy conflict status/audit. Old audits identify an order, so all same-order payments may be conservatively held. Refund totals and paid history remain unchanged. |
| Audit integrity | Runtime UPDATE/DELETE/TRUNCATE attempts against protected history fail and preserve rows. Flag clearing/deletion/reassociation fails. Runtime cannot disable triggers, change replica mode or replace guard functions. |
| Authorization | Deactivated/suspended/expired/wrong-issuer identities fail; no provider/business write happens before required authentication, role and ownership checks. |
| Webhooks | Invalid signatures fail before persistence; concurrent delivery has one lease/effect; failed/expired leases recover; ambiguous/incomplete/conflicting evidence remains held. |
| Hosted boundaries | Explicitly verify TLS, actual database role/grants/RLS, exposed schemas, Auth/Storage policies and startup against the isolated target. Health 200 alone does not establish these. |
| Client/accounting | Clients confirm exact net payout amount, currency and reference; zero-net offsets/carry-forward and thresholds are accepted. Tax/referral/dispute/legacy exceptions remain held until reviewed. |

## Current disposition

The candidate has isolated automated evidence. Hosted target isolation/baseline, target-specific upgrade/backfill, runtime grants/Auth/Storage/TLS/provider acceptance, independent review, explicit audited conflict resolution and existing manual accounting exceptions remain pending. Credential-owner rotation/revocation remains separate and was not performed. Do not promote the candidate solely from passing CI or migration-heavy assembly coverage.
