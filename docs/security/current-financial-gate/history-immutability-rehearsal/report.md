# Protected history and guard ownership verification

**PASS**, 2026-10-13. Candidate draft PR #36 remains `d47919b6a8b6d54d3d21e63889021b294fd94b87`; its PR #32 base remains `e2f969c4bc89fab8417d93e72b43bed388808d31`. Neither application head was changed. This supplements the existing 175-test / 24.40% non-migration source coverage evidence, without claiming a new coverage run or human signoff.

The earlier disposable upgrade rehearsal explicitly checked audit UPDATE, trigger disabling, replication-role bypass and conflict-flag clearing. This extension checks DELETE, cascading TRUNCATE, recovery rollback, guard replacement and guard survival after restoration.

## Observations

All 19 recorded pre-backup rejection assertions passed:

- Runtime NOLOGIN/NOSUPERUSER/NOBYPASSRLS role: DELETE and TRUNCATE CASCADE against audit_logs, payout_recovery_offsets and refund_allocations were rejected (six assertions, SQLSTATE 42501).
- Runtime: offset amount UPDATE, replacement of reject_financial_history_change(), and dropping the audit append-only trigger were rejected (three assertions, 42501). Decreasing recovered_amount on a valid recovered allocation was rejected by the trigger (one assertion, 23514).
- Owner with DML privileges: DELETE and TRUNCATE CASCADE against the same three history tables were rejected by database guards (six assertions, 23514). Audit UPDATE, offset amount UPDATE and decreasing allocation recovery were also rejected (three assertions, 23514).
- After full backup/restore, six additional owner-level DELETE/TRUNCATE CASCADE assertions passed. Exact eleven-table business snapshots before and after these rejected attempts were equal. The existing durable payment conflict guard also survived restoration and rejected flag clearing.

The accounting path still blocked the conflicted 30 USD recovery and preserved its zero recovered balance, while valid recovery recorded 10 USD against a synthetic 50 USD entitlement and a 40 USD net payout. Baseline seven-migration backup restoration, nine-migration upgrade/model parity, historical monetary preservation and corrected backup restoration all passed again. Both standalone .NET 8 Release builds with warnings as errors completed with zero warnings/errors.

## Evidence and reproducibility

`history-mutation-checks.json` records the exact 19 statements and expected rejection codes. `execution.json` records every final runner command exit code. `history-immutability-evidence.zip` includes the harness, orchestration, logs, full synthetic dumps, row snapshots and the first unsuccessful harness attempt. `SHA256SUMS` covers the published files.

The first attempt required every owner truncate to return 23514, but a refund-allocation FK dependency caused PostgreSQL to reject the statement before trigger execution. The final probes use CASCADE so they reach the actual guards. The successful run directly confirms the expected trigger result; no application defect or correction was needed.

To reproduce, extract to `/workspace/work/history-immutability-rehearsal`, retain baseline/candidate checkouts at `/workspace/work/acceptance-base` and `/workspace/work/current-financial-gate`, .NET 8.0.425 at `/workspace/.dotnet`, and the local package/CLI directories described in `run.py`. Run `python3 /workspace/work/history-immutability-rehearsal/run.py` with Docker available and loopback port 5432 free. The runner pins the managed local Docker socket, strips inherited provider/database bindings and uses a fixed disposable synthetic connection. The PostgreSQL 16.15 image is pinned to `postgres:16@sha256:ca0bd484cb98bf4b24eb1010e73fb3fcbd6714d240fbc1a10eea5b7dbecb641d`.

## Gate disposition

Local history and restore checks pass. The disposable container/databases/role were removed. PostgreSQL administrative owners can deliberately replace/disable guards; this verifies DML guards and a constrained runtime role, not immunity to an administrator. The tested runtime grants remain a template for the exercised accounting path and do not certify hosted grants/RLS, Auth, Storage, TLS or real provider behavior.

Hosted target/baseline acceptance, independent human security/accounting review, explicit conflict-resolution procedures and manual financial exceptions remain pending. No production access, merge, deployment, live transfer or credential changes occurred. No human review request was sent and no formal PR approval was posted.
