using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alpha.API.Migrations
{
    /// <inheritdoc />
    public partial class PreserveRefundEvidenceConflicts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "refund_evidence_conflict",
                table: "payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);
            migrationBuilder.Sql("""
                -- Old conflict events have order-level audit evidence only. Conservatively hold every payment on those orders.
                UPDATE payments p SET refund_evidence_conflict = TRUE
                WHERE EXISTS (SELECT 1 FROM order_financials f WHERE f.order_id = p.order_id AND f.financial_status = 'refund_evidence_conflict')
                   OR EXISTS (SELECT 1 FROM audit_logs a WHERE a.order_id = p.order_id AND a.action = 'Stripe refund evidence conflict held for manual review');
                UPDATE order_financials f SET financial_status = 'refund_evidence_conflict', payout_status = 'on_hold', settlement_status = 'blocked'
                WHERE EXISTS (SELECT 1 FROM payments p WHERE p.order_id = f.order_id AND p.refund_evidence_conflict);
                UPDATE settlement_queue q SET status = 'on_hold'
                WHERE q.status <> 'paid' AND EXISTS (SELECT 1 FROM order_financials f WHERE f.id = q.order_financial_id AND f.financial_status = 'refund_evidence_conflict');
                CREATE FUNCTION protect_payment_refund_conflict() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF OLD.refund_evidence_conflict THEN
                        IF TG_OP = 'DELETE' THEN
                            RAISE EXCEPTION 'Conflicting refund evidence requires explicit reviewed resolution' USING ERRCODE = '23514';
                        END IF;
                        IF NOT NEW.refund_evidence_conflict OR NEW.id IS DISTINCT FROM OLD.id OR NEW.order_id IS DISTINCT FROM OLD.order_id THEN
                            RAISE EXCEPTION 'Conflicting refund evidence requires explicit reviewed resolution' USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER payment_refund_conflict_persistent BEFORE UPDATE OR DELETE ON payments
                    FOR EACH ROW EXECUTE FUNCTION protect_payment_refund_conflict();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER payment_refund_conflict_persistent ON payments;
                DROP FUNCTION protect_payment_refund_conflict();
                """);
            migrationBuilder.DropColumn(
                name: "refund_evidence_conflict",
                table: "payments");
        }
    }
}
