# Phase 4 acceptance review and queue correction

2026-10-13 (Asia/Taipei). Assistant review and automated evidence only, not independent human accounting/operator/security signoff. Backend draft42 and frontend draft2 were updated in place; no additional PR or merge was needed.

## Finding: oversized source blocks unrelated queue visibility (medium)

At original backend application head08b31205949db27dcc8cb049fef1d930ccb2289a, Snapshot throws when an order/batch/target has more than200 evidence rows. Queue called Snapshot for every source without isolating this bounded-scope error. One oversized source therefore made the whole request fail409 and prevented the operator from obtaining a cursor or seeing unrelated sources. This concerns historical/imported/out-of-band data; no current payout API route that can create201 duplicate payouts, unauthenticated exploit, financial mutation or transfer is claimed.

A focused regression was added to the original application source, seeded201 synthetic payout rows and a second ordinary source in disposable PostgreSQL. It failed with the expected order scope exception from QueueAsync. The identity log records the original application commit; the added uncommitted regression test means this reproduction is not a clean unchanged-source test checkout. The application implementation was unchanged during the reproduction. The archive retains failing TRX/logs and the matching regression source.

The corrected queue catches only explicitly marked bounded-scope exceptions for a source. It returns a marker with inspectionAvailable=false, manual_review_scope_exceeded and **null monetary/reference fields**. No zero amounts or validated financial result are invented. Other rows and cursor pagination remain available. Operational/database errors are not silently classified as scope limits. Inspect and capture continue to reject oversized evidence, with no appended audit or financial effect. UI markers show Manual review required and enable no inspection/capture. New current API responses require explicit availability; the paired backend must precede frontend use.

Actual observations retain inspectionAvailable=true. Existing v1 audit payloads missing this added display field deserialize as available observations because only actual bounded observations were previously capturable. Stored records are never rewritten. An additional regression inserts a prior-shape payload, changes source state through a synthetic payout and proves original actor/source/fingerprint replay and scoped history remain stable and byte-identical in storage.

## Final exact heads and gates

| Repository | Draft PR | Corrected exact head |
|---|---|---|
| Coxskull/alpha-backend |42 against main|57f5b359a03ddcdd8202a01f935ca6ac2da31b60|
| Coxskull/Alpha-Auto-MVP |2 against main|72ced72ffe02ee303c36292c72a329ac6a8a2cef|

- Backend: **226 tests passed (77 unit +149 PostgreSQL/HTTP integration)**, zero failed/skipped. .NET8.0.425 Release warnings-as-errors builds, locked restore, app/unit/integration vulnerability audits, eleven-migration /60-table parity and startup health200 passed. Source coverage excluding generated migrations **26.26% (4805/18301)**. Tests cover queue continuation, unavailable amounts, direct inspect/capture refusal, zero appended audit on denial, old immutable capture replay after source changes, scoped history and the prior Phase4 financial/role/concurrency checks.
- Frontend: Node24.19.0 clean install, typecheck, changed-file lint, Next16.4 production build and **13 contract tests** passed. **Nine local synthetic browser scenarios** passed, including the previous eight and oversized manual marker/paging/no-capture behavior. Browser API calls were intercepted at fixed loopback using synthetic tokens and other outbound requests were blocked. Browser scenarios are local, not CI/provider/bank acceptance. One unchanged configuration-page unused-variable lint warning remains.
- Frontend [exact-head CI](https://github.com/Coxskull/Alpha-Auto-MVP/actions/runs/38358881346) and [Vercel PR check](https://vercel.com/alpha-auto-s-projects/alpha-auto-mvp/BvTnnjQAQeyTbGLM42iwypUsZwsf) passed. Backend [companion CI](https://github.com/Coxskull/alpha-backend/actions/runs/38358882313) and [Release CI](https://github.com/Coxskull/alpha-backend/actions/runs/38358882288) both passed on the corrected exact head. Decoded Release logs are preserved. Artifact11793334655, SHA25645ad5dbe2390acb2ea1bfeed47ecaa2362401a22183ad7facc07a404df07fd7d, expires2026-11-12.

Fresh production npm audit remains zero known findings. Full graph audit retains five high development entries from the prior braces advisory GHSA-vfj7-8cjw-p6xm; no all-dependency security clearance is claimed. Source-less payout inventory, external bank/provider evidence, realistic-volume audit lookup benchmarks, legacy large/mixed batches and approved isolated hosted baseline remain pending as documented in the parent runbook. No additional correction was identified in this focused scope; this is not exhaustive financial acceptance.

## Evidence and next review gate

exact-head-review-evidence.zip contains failing original reproduction, final builds/audits/TRX/coverage/schema/startup/CI/browser logs and runners. validation-summary.json records both corrected heads and findings. Synthetic screenshot is local mock data; the pre-existing Live badge is not production evidence. SHA256SUMS covers deliverables. Disposable PostgreSQL and local app/browser processes were removed/stopped.

Review corrected backend42 before frontend2; both remain drafts. Verify resulting main SHAs after owner-authorized merges. Human accounting/operator/security and isolated hosted acceptance are still required. Existing Vercel Git integration ran a PR check after the correction push; no production URL/deployment command was used. No production access, agent merge, live transfer or credential change occurred.
