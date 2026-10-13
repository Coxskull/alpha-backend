# Alpha hosted compatibility and security assessment — 12 October 2026

Release status: OPEN / PENDING. Project Alpha (`giujjtscdvtpyjpnceag`) was inspected through read-only metadata queries. No hosted schema, customer records, payments, policies, grants or credentials were changed. Isolation from production has not been established; application startup and migration tests use synthetic loopback PostgreSQL only.

## Confirmed catalog observations

- Active project in ap-southeast-1; PostgreSQL 17.6. Public catalog contains 61 tables, 684 columns, 151 indexes and 154 constraints.
- No development branches or Supabase migration history were returned. EF history contains only `20260609065938_InitialCreate` and `20260609152717_OrderRelations`, both with product version 9.0.0. The revised application's migration chain is a different baseline.
- The comparison at commit `8946949623433e8317be7b6814e93086defda818` recorded 188 metadata differences, including names, missing columns, types and nullability. This is not a count of proven runtime failures.
- Confirmed names corrected in development: `entrepreneur_payout_batches`, `entrepreneur_referral_audits`, `customers.phone`, and `customer_vehicles.vin`. The local mapping migration preserves synthetic pre-migration ledger records. Customer and vehicle projections do not certify full hosted entity compatibility.
- Hosted tables `payment_operations`, `refund_allocations`, and `payout_recovery_offsets` are absent. Other missing profile, webhook lease and recovery columns remain. Full hosted compatibility is pending.

## Security observations and limits

- Security advisors returned 54 informational RLS-enabled-without-policy findings (ordinary-role default denial) and three mutable function search-path warnings. No error-level findings were returned; this is not comprehensive security certification.
- The three functions are `update_orders_timestamp()`, `update_updated_at_column()`, and `log_order_changes()`. They are SECURITY INVOKER trigger functions with no configured search path. Review their definitions and dependencies before setting a bounded path in an isolated clone. Guidance: https://supabase.com/docs/guides/database/database-linter?lint=0011_function_search_path_mutable
- Seven tables have RLS disabled: `__EFMigrationsHistory`, `audit_logs`, `delivery_proof`, `status_history`, `suppliers`, `users`, and `drivers`. Catalog checks found no SELECT, INSERT, UPDATE or DELETE grants for anon/authenticated on these tables. The `customer_visible_products` view also has no SELECT grant for either role. Do not infer public exposure solely from disabled RLS.
- The inspecting postgres role and service_role bypass RLS. The actual application's database role and privileges remain to be verified. Public policies were absent. API-exposed schemas, Auth configuration, Storage policies, hosted TLS/runtime startup and credential rotation were not certified.
- Current Supabase documentation was consulted through its documentation tool. The changelog endpoint was blocked with HTTP 403 by the execution network policy.

## Required release work

1. Establish a demonstrably isolated staging clone and approved application database role before hosted execution.
2. Review a target-specific upgrade plan against Alpha's actual migration history; do not run the empty-database migration chain blindly against existing Alpha tables.
3. Validate financial nulls, identities and legacy account activation/password backfills. Do not fabricate monetary defaults or silently activate accounts.
4. Rehearse upgrades with synthetic/approved staging data, backups, rollback and acceptance criteria; then reassess policies, function paths, Auth/Storage and TLS.
5. Rotate previously exposed credentials and obtain independent security/accounting review. Package audits and passing tests alone do not authorize release.

No merge, deployment, production database change, live payment or bank-account debit is authorized by this report.
