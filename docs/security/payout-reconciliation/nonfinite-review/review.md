# Source-first reconciliation: nonfinite numeric review

2026-10-13 (Asia/Taipei). User explicitly chose continued Alpha Auto financial phases over the attachment's proposed Bliss-first switch. Existing register AGM-LAUNCH-009 remains unchanged; no launch-wide acceptance or new permanent task ID is claimed.

## Provenance and finding

Backend PR44 is merged at 197affe709259e9aeea3d504ed56e56c2e1ba834; its tree 9147d7db3bef348f9bf1a8679b38703a145d9cb6 equals reviewed f515c6a7de88f60cdfd573d4c64443e378ac5418. Frontend PR4 is merged at 6a34589d83c888703bd920b2d896c7fbcedf7048; its tree a538be4f240f7668fb5bf35a8a8e7d381489954c equals reviewed ee6ced8c9ccf9e4a0a9cde5fecb984aa83dc0294.

Medium availability finding: source-first reconciliation still materialized native PostgreSQL NaN as System.Decimal. Six added regression probes against the unchanged merged application implementation reproduced InvalidCastException in supplier payout, driver payout, settlement source, order financials, entrepreneur earning and payout batch. This was a checkout with added tests, not an unchanged clean checkout. Original logs/TRX are archived. The final tests additionally check immutable prior-capture replay and healthy next-page continuation.

## Correction and evidence

Draft PR45 exact head 281a78019284f2d29005ecb55f446afb4f4ac4d7 preflights mapped decimal fields in parameterized native queries within the same repeatable-read transaction. Only a positively identified nonfinite value becomes manual_review_nonfinite_numeric with inspectionAvailable=false and null money/reference values. Known scope-limit markers remain supported; unrelated operational failures still propagate. Inspection and fresh capture fail closed. Existing immutable capture replay returns original evidence without re-reading corrupt source. No numeric repairs, financial writes, hold changes, migrations or provider calls are added.

Preflight candidate reads follow the 201-row materialization bound. Recovery totals already aggregate all offsets for the selected claims; the nonfinite guard follows that same full allocation scope. Identifiers come from EF mapping; source identifiers are parameters. All current mappings use the default schema.

Final local gates:243 tests (77 unit,166 disposable PostgreSQL/HTTP integration), zero failed/skipped; .NET8 Release builds with warnings as errors; locked restores; three direct/transitive NuGet audits without advisory findings; 11 migrations with model parity; loopback startup health 200. Non-migration source coverage 26.58% (4887/18384 lines). Whole-assembly coverage includes generated migrations and is reported separately in JSON/XML.

Nine synthetic browser checks passed against unchanged frontend ee6ced8c9ccf9e4a0a9cde5fecb984aa83dc0294, including manual-row action denial and pagination. Browser API responses were intercepted fixtures; this does not claim browser-to-live-API or hosted acceptance. No frontend PR is required for this existing contract.

CI evidence: https://github.com/Coxskull/alpha-backend/actions/runs/38384682850 and https://github.com/Coxskull/alpha-backend/actions/runs/38384682816 . See archived exact-head logs and recorded final conclusions.

## Limits and next action

Synthetic NaN corruption is tested directly; Infinity/-Infinity are recognized by the guard but are not separately injected into constrained numeric columns. Finite extreme-value/decimal-overflow handling is outside this correction. Recovery allocation/offset guards are exercised by ordinary recovery regressions, not dedicated corrupted-ledger fixtures. Added queries have not been capacity-certified. The unchanged frontend's previously documented development dependency advisory remains outside this backend correction; no fresh frontend dependency clearance is claimed.

No production access, hosted database inspection, manual deployment, live money, credential change or merge was performed. Disposable PostgreSQL and browser/server processes were cleaned up. Review PR45 against this evidence before deciding whether to merge. Accounting/operator/security acceptance remains with the authorized reviewer.
