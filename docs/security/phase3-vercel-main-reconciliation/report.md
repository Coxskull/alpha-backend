# Vercel runtime fix and Phase 3 merge reconciliation

2026-10-13 (Asia/Taipei). Frontend Vercel/CI success confirmed; backend correction local gates passed. Human operator/accounting/security and isolated hosted financial acceptance are not claimed.

## Confirmed Vercel cause and fix

Owner-provided logs show deployment 4eSeBeuLNU4pCS7CoFfD57m22BRB at frontend head 9031196bf15df6d36cd68dc363dcde4ebe7ad4f6 rejected discontinued Node 20.x before executing a build. Current Vercel documentation lists 22.x and 24.x: https://vercel.com/docs/functions/runtimes/node-js/node-js-versions.

Frontend PR1 head **00663da0b342699a8cca2116d148e96e76ce4a80** changes app and lockfile engines to 24.x and CI to Node 24.19.0. No payout logic or dependency versions change. Local Node24 clean npm install (scripts enabled), TypeScript, changed-file lint, eight pure contract cases, production dependency audit (zero entries), Next16.4 production build and eleven synthetic Chromium cases passed. One pre-existing unused configuration-page variable lint warning remains. The prior development-only braces advisory is unchanged; full-graph clearance is not claimed.

[Vercel replacement check](https://vercel.com/alpha-auto-s-projects/alpha-auto-mvp/5YtpLbtuzioe4wRVbDmnwz4of48c) reports **success** for that exact head. [Frontend Node24 CI](https://github.com/Coxskull/Alpha-Auto-MVP/actions/runs/38351421732) also passed. Browser cases are local synthetic acceptance, not hosted financial acceptance. No production URL was accessed and no deployment command was run; the existing Vercel Git integration automatically ran the PR check after the correction push.

## Merge-order gap and corrective draft

PR39 merged to main at **b443b3bc0e15d7453b484ce424e97ff802f8d0ee**, whose tree equals reviewed PR39 head cdffbfc5bdb8acfa710cbed0c7f6eda93f561b66. PR40 then merged at **533280bc8656d2acaf10c7884243a0e358531ea7**, but its target remained feature/phase2-claim-recovery-release. GitHub saying both PRs are merged therefore does not establish that main includes the Phase3 contract. Fresh main refs and git ancestry confirm PR40 is absent from main.

[Backend corrective draft PR41](https://github.com/Coxskull/alpha-backend/pull/41) targets main, merging the already-owner-merged PR40 branch. Exact correction head **b5ed348396e64164f2bb8bb26132707b82d610fa** has tree **e0389ec37db7e45d97dbfb79a36eaf2625eaedea**, identical to reviewed PR40 head ebd572b613ba60b8e1befd063c30fcc429b37217. No new product edits or migrations are introduced. The missing nine-file contract/test/workflow/documentation diff is preserved.

Fresh exact-correction disposable validation passed **208 tests (77 unit / 131 PostgreSQL integration), zero failures/skips**, .NET8.0.425 Release warnings-as-errors builds, app/unit/integration vulnerability audits, eleven migrations / 60 tables model/schema parity and startup HTTP200. Source coverage **25.64% (4654/18150)**, generated migrations separated. This is correction-head validation, not a claim that current main already includes the contract.

Backend [Release CI](https://github.com/Coxskull/alpha-backend/actions/runs/38351582036) and [companion CI](https://github.com/Coxskull/alpha-backend/actions/runs/38351582006) both passed on the exact correction head. Decoded Release logs are preserved in the archive. Artifact 11791690016, SHA256 7339370ae07df1ab0e94c56cc909d183e62660e25c1e3002f8ac08a8d322a992, expires 2026-11-12.

## Next gates

Owner review/merge of PR41 brings the missing backend contract to main; then verify resulting main SHA. Frontend PR1 is still an unmerged draft and requires this backend contract before payout recording rollout. Owner merges do not authorize agent production deployment. Continue disposable acceptance until an approved isolated hosted target/baseline is provided. Independent operator/accounting/security review and the development advisory remain pending.

The archive preserves original Vercel error, corrected head/build/audit/CI/browser logs, successful Vercel status JSON, tree/ancestry proof, backend TRX/coverage/audit/schema/startup and runners. Local app/browser processes stopped and disposable PostgreSQL was removed. No agent merge, production access/deployment, live transfer or credential change occurred.
