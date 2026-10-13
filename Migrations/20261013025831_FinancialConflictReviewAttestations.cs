using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alpha.API.Migrations
{
    /// <inheritdoc />
    public partial class FinancialConflictReviewAttestations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financial_conflict_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_reviewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    evidence = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    snapshot_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_conflict_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_financial_conflict_reviews_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_conflict_reviews_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_conflict_reviews_users_first_reviewer_id",
                        column: x => x.first_reviewer_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_conflict_approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_conflict_approvals", x => x.id);
                    table.ForeignKey(
                        name: "FK_financial_conflict_approvals_financial_conflict_reviews_rev~",
                        column: x => x.review_id,
                        principalTable: "financial_conflict_reviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_conflict_approvals_users_reviewer_id",
                        column: x => x.reviewer_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financial_conflict_approvals_review_id",
                table: "financial_conflict_approvals",
                column: "review_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_conflict_approvals_reviewer_id",
                table: "financial_conflict_approvals",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "IX_financial_conflict_reviews_first_reviewer_id",
                table: "financial_conflict_reviews",
                column: "first_reviewer_id");

            migrationBuilder.CreateIndex(
                name: "IX_financial_conflict_reviews_order_id",
                table: "financial_conflict_reviews",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_financial_conflict_reviews_payment_id_created_at",
                table: "financial_conflict_reviews",
                columns: new[] { "payment_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_financial_conflict_reviews_payment_id_idempotency_key",
                table: "financial_conflict_reviews",
                columns: new[] { "payment_id", "idempotency_key" },
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION validate_financial_conflict_attestation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF length(btrim(NEW.evidence)) = 0 THEN
                        RAISE EXCEPTION 'Attestation evidence is required' USING ERRCODE = '23514';
                    END IF;
                    IF TG_TABLE_NAME = 'financial_conflict_reviews' THEN
                        IF length(btrim(NEW.idempotency_key)) = 0 OR NEW.snapshot_hash !~ '^[0-9A-F]{64}$'
                            OR NOT EXISTS (SELECT 1 FROM payments p WHERE p.id = NEW.payment_id
                                AND p.order_id = NEW.order_id AND p.refund_evidence_conflict) THEN
                            RAISE EXCEPTION 'Review must identify current conflicted payment evidence' USING ERRCODE = '23514';
                        END IF;
                    ELSE
                        IF EXISTS (SELECT 1 FROM financial_conflict_reviews r WHERE r.id = NEW.review_id
                            AND r.first_reviewer_id = NEW.reviewer_id) THEN
                            RAISE EXCEPTION 'Second reviewer must be distinct' USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER financial_conflict_review_insert BEFORE INSERT ON financial_conflict_reviews
                    FOR EACH ROW EXECUTE FUNCTION validate_financial_conflict_attestation();
                CREATE TRIGGER financial_conflict_approval_insert BEFORE INSERT ON financial_conflict_approvals
                    FOR EACH ROW EXECUTE FUNCTION validate_financial_conflict_attestation();
                CREATE TRIGGER financial_conflict_reviews_immutable BEFORE UPDATE OR DELETE ON financial_conflict_reviews
                    FOR EACH ROW EXECUTE FUNCTION reject_financial_history_change();
                CREATE TRIGGER financial_conflict_reviews_no_truncate BEFORE TRUNCATE ON financial_conflict_reviews
                    FOR EACH STATEMENT EXECUTE FUNCTION reject_financial_history_change();
                CREATE TRIGGER financial_conflict_approvals_immutable BEFORE UPDATE OR DELETE ON financial_conflict_approvals
                    FOR EACH ROW EXECUTE FUNCTION reject_financial_history_change();
                CREATE TRIGGER financial_conflict_approvals_no_truncate BEFORE TRUNCATE ON financial_conflict_approvals
                    FOR EACH STATEMENT EXECUTE FUNCTION reject_financial_history_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financial_conflict_approvals");

            migrationBuilder.DropTable(
                name: "financial_conflict_reviews");
            migrationBuilder.Sql("DROP FUNCTION validate_financial_conflict_attestation();");
        }
    }
}
