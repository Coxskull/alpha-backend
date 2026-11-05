# ALPHA Supabase Production Function Hardening — Verified Receipt

**Date:** 2026-11-05  
**Database:** connected Supabase project `Alpha`, ref `giujjtscdvtpyjpnceag` (NOT Bliss Bot, NOT the separate Alpha Lead Generation staging vault).  
**Status:** database changes **already applied** and independently verified; this file records production migration provenance, not a pending deploy script.

## Before and after

Initial Supabase Security Advisor warnings:
- `public.update_orders_timestamp()`: mutable search path.
- `public.update_updated_at_column()`: mutable search path.
- `public.log_order_changes()`: mutable search path.
- `auth_leaked_password_protection`: disabled (separate Auth plan/settings gate).

### Migration 1: narrow search-path pinning

Applied with the connected Supabase migration API as
`alpha_pin_public_timestamp_trigger_search_paths_20261105`:

```sql
ALTER FUNCTION public.update_orders_timestamp() SET search_path = '';
ALTER FUNCTION public.update_updated_at_column() SET search_path = '';
```

These are `SECURITY INVOKER` trigger functions using `NEW.updated_at=now()` (built-in `pg_catalog`), and neither requires user-schema names. Independent `pg_proc` readback confirms both pinned empty paths, and active triggers `public.orders.trg_orders_updated_at` and `public.products.trg_products_updated_at` still reference the same function objects and are enabled.

### Migration 2: retire an unused incompatible legacy trigger

Applied with the connected Supabase migration API as
`alpha_retire_unused_incompatible_audit_trigger_20261105`:

```sql
DROP FUNCTION public.log_order_changes() RESTRICT;
```

**Pre-change checks:** `pg_depend` had no dependent database objects, `pg_trigger` had no attached triggers, and connected backend repository code search found no application usages outside an older read-only security assessment. Its body expected `audit_logs(table_name, record_id, action_type, old_data, new_data)` even though the only actual `public.audit_logs` table has `id, order_id, action, performed_by, created_at`. Activating it would fail or misbehave; do not reintroduce it without a reviewed audit-table contract and full tests.

**Critical data preservation:** `public.audit_logs` contained **955** existing audit rows before retirement and **955** after; no table row was deleted, updated, or migrated. `DROP FUNCTION ... RESTRICT` would have refused execution if a new database dependency had appeared. No payment/commission history, RLS grants, triggers, STOP, or live customer records were modified.

## Independent post-change readback

- `to_regprocedure('public.log_order_changes()') IS NULL`: **true**.
- `public.audit_logs`: **955** rows, latest row timestamp `2026-08-29 07:14:23.426033+00`.
- Both timestamp functions: `proconfig` indicates `search_path=''`; both are still `SECURITY INVOKER`; associated triggers enabled.
- Refreshed Supabase Security Advisor: **0 function_search_path_mutable warnings**. Remaining security WARN: **1 auth_leaked_password_protection disabled**.
- No production feature or end-to-end order mutation was performed solely to test these changes; actual application regression acceptance remains separate.

## Open tasks and safe rollback

Leaked-password protection is documented as available on Supabase **Pro or higher**. Account plan is not confirmed by the connector; do not create a paid plan or change authentication policy automatically. The owner should check `https://supabase.com/dashboard/project/giujjtscdvtpyjpnceag/auth/providers` and actual plan eligibility, then evaluate auth rollout with legitimate sign-in tests. Never log credentials.

If restoring timestamp-function behavior is ever required, only their function settings can be reverted via `ALTER FUNCTION ... RESET search_path`, but this would reintroduce advisor warnings and should be reviewed first.

The retired audit function must **not** be recreated from its old invalid body. Any replacement requires explicit schema, event semantics, append-only audit and backfill/retention requirements, with independent test coverage.

## Read-only evidence queries (safe to rerun)

```sql
SELECT to_regprocedure('public.log_order_changes()') IS NULL AS retired,
       (SELECT count(*) FROM public.audit_logs) AS historic_audit_rows;

SELECT p.oid::regprocedure::text AS signature, p.prosecdef, p.proconfig
FROM pg_proc p
JOIN pg_namespace n ON n.oid=p.pronamespace
WHERE n.nspname='public'
  AND p.proname IN ('update_orders_timestamp','update_updated_at_column');

SELECT t.tgname, t.tgenabled
FROM pg_trigger t
WHERE NOT t.tgisinternal
  AND t.tgfoid IN ('public.update_orders_timestamp()'::regprocedure,
                  'public.update_updated_at_column()'::regprocedure);
```

This is a historical production receipt. **Do not rerun the two production migrations** by applying this document as SQL. It does not authorize changes to Railway/Factory STOP or another Supabase project.
