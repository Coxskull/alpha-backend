# PR #34 — persistent refund evidence conflicts

PR #33 remains at its reviewed head `40a633910f12d3687b463f70e9acff93746b2f2b`. Follow-up draft PR #34 targets its branch and is validated at exact head `19c6c2034b5577a78c1a66e39e2e207b1b10537f`. No merge or hosted actions were performed.

## Correction

The [prior review](../pr33-review/report.md) reproduced two cases where signed valid evidence silently changed `refund_evidence_conflict` to `refund_review`. Conflicts now persist on payments as a separate boolean flag. Valid/replayed Stripe evidence, refunds on another payment, PayPal reservation/completion and generic review status changes cannot clear the effective gate. Allocation, settlement, source-order payout and future recovery inspect all payments on the order. Conflict evidence locks every affected outstanding recovery payee on that order, including claims attached to another payment. Reservation locks use stable order-payment ordering and reload financial state.

SQL guards reject clearing the flag, deleting flagged payments or moving their identity/order association. Owners can deliberately alter/drop guards; hosted runtime grants must be reviewed separately. The provisioning migration conservatively re-holds every payment on orders with an existing conflict status or the exact prior Stripe conflict audit action. Old audit evidence identifies an order rather than one payment, so this can intentionally hold additional same-order payments. Customer refund totals and confirmed paid payout history remain unchanged. The EF snapshot adds one boolean column; the generated designer contains full model metadata.

There is no automatic clearance and no new resolution endpoint. An expressly reviewed, evidenced, audited resolution procedure remains a separate accounting acceptance task. Resetting financial display status cannot release future recovery; downgrading is not an approved resolution procedure. The migration/backfill is not an approved upgrade for a hosted manually provisioned schema.

## Exact-head results

[Release evidence](https://github.com/Coxskull/alpha-backend/actions/runs/38250975127) and [companion .NET validation](https://github.com/Coxskull/alpha-backend/actions/runs/38250974946) both succeeded at `19c6c2034b5577a78c1a66e39e2e207b1b10537f`.

- .NET SDK 8.0.425 Release builds with warnings as errors: zero warnings/errors.
- 77 unit + 92 PostgreSQL/HTTP integration tests: 169 passed, zero failed/skipped.
- Fresh application/unit/integration NuGet audits: no known vulnerable packages.
- Disposable migration/model parity: PASS, 57 tables; synthetic startup: HTTP 200.
- Runtime source coverage: 24.34% (4,355/17,890). Assembly coverage is 58.25% including generated migrations and must not be described as runtime coverage.
- Both previously failing signed-event review probes now pass. New cases cover completed PayPal refunds preserving customer totals and conflict state, future recovery/allocation after display-status changes and later reservations, SQL flag/identity removal rejection, legacy-audit backfill through migration downgrade/reapply, and cross-payment conflict processing waiting for the source payee lock.

[Exact-head CI artifact](https://github.com/Coxskull/alpha-backend/actions/runs/38250975127/artifacts/11747439875) contains TRX, coverage, build/restore/audit/startup logs, model/migration SQL and schema results. GitHub reports SHA-256 `7e5e750ed816041215a114c6ceea2ec56c24ba7965d10c8ec99faf7ea6efdb77`; retention is 30 days. Its head identity matches PR #34. CI PostgreSQL was disposable and used synthetic provider responses with no production secrets.

## Durable local evidence and scope

`local-a7d7faf.zip` preserves the complete local 168-test run at the first implementation commit, plus the final exact-head CI job log and artifact metadata. `lock-19c6c20.zip` preserves the two locally rerun recovery-lock cases at the final commit. The last commit changes only the lock test, adding the other-payment variant. These local counts are labeled separately from the complete 169-test final CI run. ZIP and per-file SHA-256 manifests accompany the evidence.

Both local PostgreSQL containers and CI service containers were removed after testing. No production access, live provider calls/transfers, bank debits, credential changes, hosted migration, merge or deployment occurred. The known blocker is corrected in the proposed branch; independent review, hosted non-production schema/grants/accounting acceptance, a reviewed conflict-resolution procedure, historical credential-owner gates and existing manual exceptions remain pending. This evidence is technical verification, not release approval.
