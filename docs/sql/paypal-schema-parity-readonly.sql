-- ALPHA-EV-20261012-PAYPAL-SCHEMA-AUDIT
-- READ ONLY. Execute against an approved NON-PRODUCTION PostgreSQL database only.
-- Output is schema metadata; review identifiers before sharing. Never include connection strings.
-- No BEGIN/COMMIT or DDL/DML is used.
SELECT current_database() AS database_name, current_setting('server_version') AS postgres_version;
SELECT table_schema, table_name, column_name, data_type, udt_name,
       is_nullable, numeric_precision, numeric_scale, column_default
FROM information_schema.columns
WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
  AND (table_name IN ('payments', 'order_financials', 'settlement_queue',
                      'referral_transactions', 'entrepreneur_earnings',
                      'payment_refund_attempts', 'payment_capture_operations')
       OR table_name LIKE '%refund%' OR table_name LIKE '%settlement%')
ORDER BY table_schema, table_name, ordinal_position;
SELECT n.nspname AS schema_name, t.relname AS table_name,
       c.conname AS constraint_name, c.contype AS constraint_type,
       pg_get_constraintdef(c.oid) AS definition
FROM pg_constraint c
JOIN pg_class t ON t.oid = c.conrelid
JOIN pg_namespace n ON n.oid = t.relnamespace
WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
  AND (t.relname IN ('payments', 'order_financials', 'settlement_queue',
                     'referral_transactions', 'entrepreneur_earnings',
                     'payment_refund_attempts', 'payment_capture_operations')
       OR t.relname LIKE '%refund%' OR t.relname LIKE '%settlement%')
ORDER BY n.nspname, t.relname, c.conname;
SELECT schemaname, tablename, indexname, indexdef
FROM pg_indexes
WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
  AND (tablename IN ('payments', 'order_financials', 'settlement_queue',
                     'referral_transactions', 'entrepreneur_earnings',
                     'payment_refund_attempts', 'payment_capture_operations')
       OR tablename LIKE '%refund%' OR tablename LIKE '%settlement%')
ORDER BY schemaname, tablename, indexname;
-- Migration IDs only; run separately if the EF migrations table exists:
-- SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";
