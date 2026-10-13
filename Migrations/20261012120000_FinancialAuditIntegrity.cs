using Alpha.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Alpha.API.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261012120000_FinancialAuditIntegrity")]
public sealed class FinancialAuditIntegrity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE FUNCTION reject_financial_history_change() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION 'Financial history is append-only' USING ERRCODE = '23514';
        END $$;
        CREATE TRIGGER audit_logs_append_only BEFORE UPDATE OR DELETE ON audit_logs
            FOR EACH ROW EXECUTE FUNCTION reject_financial_history_change();
        CREATE TRIGGER audit_logs_no_truncate BEFORE TRUNCATE ON audit_logs
            FOR EACH STATEMENT EXECUTE FUNCTION reject_financial_history_change();
        CREATE TRIGGER recovery_offsets_append_only BEFORE UPDATE OR DELETE ON payout_recovery_offsets
            FOR EACH ROW EXECUTE FUNCTION reject_financial_history_change();
        CREATE TRIGGER recovery_offsets_no_truncate BEFORE TRUNCATE ON payout_recovery_offsets
            FOR EACH STATEMENT EXECUTE FUNCTION reject_financial_history_change();
        CREATE FUNCTION protect_refund_allocation_history() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF TG_OP <> 'UPDATE' THEN
                RAISE EXCEPTION 'Refund allocation history cannot be removed' USING ERRCODE = '23514';
            END IF;
            IF (to_jsonb(NEW) - 'recovered_amount') IS DISTINCT FROM (to_jsonb(OLD) - 'recovered_amount')
                OR NEW.recovered_amount < OLD.recovered_amount OR NEW.recovered_amount > OLD.amount
                OR (NOT OLD.future_recovery AND NEW.recovered_amount <> OLD.recovered_amount) THEN
                RAISE EXCEPTION 'Only bounded monotonic recovery may update an allocation' USING ERRCODE = '23514';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER refund_allocations_history BEFORE UPDATE OR DELETE ON refund_allocations
            FOR EACH ROW EXECUTE FUNCTION protect_refund_allocation_history();
        CREATE TRIGGER refund_allocations_no_truncate BEFORE TRUNCATE ON refund_allocations
            FOR EACH STATEMENT EXECUTE FUNCTION reject_financial_history_change();
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TRIGGER refund_allocations_no_truncate ON refund_allocations;
        DROP TRIGGER refund_allocations_history ON refund_allocations;
        DROP FUNCTION protect_refund_allocation_history();
        DROP TRIGGER recovery_offsets_no_truncate ON payout_recovery_offsets;
        DROP TRIGGER recovery_offsets_append_only ON payout_recovery_offsets;
        DROP TRIGGER audit_logs_no_truncate ON audit_logs;
        DROP TRIGGER audit_logs_append_only ON audit_logs;
        DROP FUNCTION reject_financial_history_change();
        """);
}
