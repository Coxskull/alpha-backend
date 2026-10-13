# Phase 3 backend payout operator contract

This is backend preparation for operator screens. A frontend repository has not yet been identified; no UI is included. Phase 2's claim-release draft remains a prerequisite for the combined branch. Neither draft authorizes deployment or a transfer.

Existing settlement quote/mark-paid and entrepreneur quote/mark-paid routes retain their fields, authorization and accounting behavior. Fresh quotes additionally expose `netAmount` (alias of paidAmount), `payableBeforeRecovery`, `remainingRecoveryBalance` after proposed offsets, `zeroNet`, `quoteIsAdvisory`, `confirmationRequired` and `eligibleForConfirmation`. These are advisory values computed while the payee lock is held; requesting a quote does not consume debt or reserve funds. Paid/replay responses remain recorded results, not fresh quotes; legacy replays may omit new quote-only fields.

Settlement payable is gross minus unpaid refund adjustment. Entrepreneur payable is gross plus signed refund/chargeback adjustments; the response includes the chargeback adjustment. Recovery offsets reduce that payable to net cash. Remaining recovery balance carries forward for the same payee/type/currency. Zero net means no cash transfer and may still require recording reviewed internal offsets with an explicit reference. The existing minimum threshold applies to positive entrepreneur net cash, not zero-net offsets. Entrepreneur quotes expose minimumPayoutThreshold and payout_threshold_not_met before confirmation; blocked quotes are not eligible.

Operator flow: show gross/adjustments/recovery/net/currency and carry-forward together. Disable confirmation on held or ineligible quotes. For eligible quotes, require the exact net amount, currency and reference, and show zero-net outcomes explicitly. After HTTP 409 payout_quote_changed, fetch a fresh quote and require a new operator decision; never silently resend a larger amount. Quotes have no reservation or signed quote token, and may change after another payout. Existing exact-net and atomic-commit checks remain authoritative.

Both payout controllers add a stable `code` beside the existing conflict `message`:

| Code | Operator action |
|---|---|
| payout_review_required | Keep confirmation disabled; review the held/unsupported/not-ready source. |
| payout_quote_changed | Refresh the quote; reconfirm the current net amount and currency. |
| invalid_confirmation | Supply the net amount, currency and reference. |
| payout_replay_conflict | Compare the request with the already-recorded payout; do not create another effect. |
| payout_threshold_not_met | Positive entrepreneur net cash is below the configured minimum; retain it for later eligibility. |

Standard authentication/authorization and model-validation responses remain unchanged. The error codes do not prove provider evidence or authorize cash movement. These endpoints record externally reviewed payouts and never send transfers. Frontend integration, human accounting/operator acceptance, hosted baseline/role/provider checks and rollout remain pending.
