using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alpha.API.Migrations
{
    /// <inheritdoc />
    public partial class ClaimScopedRecoveryReleases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financial_recovery_releases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    evidence = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_recovery_releases", x => x.id);
                    table.ForeignKey(
                        name: "FK_financial_recovery_releases_financial_conflict_reviews_revi~",
                        column: x => x.review_id,
                        principalTable: "financial_conflict_reviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_recovery_releases_refund_allocations_allocation_id",
                        column: x => x.allocation_id,
                        principalTable: "refund_allocations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_recovery_releases_users_reviewer_id",
                        column: x => x.reviewer_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financial_recovery_releases_allocation_id",
                table: "financial_recovery_releases",
                column: "allocation_id");

            migrationBuilder.CreateIndex(
                name: "IX_financial_recovery_releases_review_id_allocation_id",
                table: "financial_recovery_releases",
                columns: new[] { "review_id", "allocation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_recovery_releases_reviewer_id",
                table: "financial_recovery_releases",
                column: "reviewer_id");
            migrationBuilder.Sql("""
                CREATE FUNCTION validate_financial_recovery_release() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF length(btrim(NEW.evidence)) = 0 OR NEW.source_fingerprint !~ '^[0-9A-F]{64}$'
                        OR NOT EXISTS (
                            SELECT 1 FROM financial_conflict_reviews r
                            JOIN financial_conflict_approvals a ON a.review_id = r.id
                            JOIN refund_allocations c ON c.id = NEW.allocation_id
                            JOIN payment_operations o ON o.id = c.operation_id
                            WHERE r.id = NEW.review_id AND o.payment_id = r.payment_id
                                AND o.kind = 'refund' AND o.state = 'completed'
                                AND c.future_recovery AND c.recovered_amount < c.amount
                                AND a.reviewer_id <> r.first_reviewer_id
                                AND NEW.reviewer_id IN (r.first_reviewer_id, a.reviewer_id)) THEN
                        RAISE EXCEPTION 'Release requires two attestations and a matching existing recovery claim' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER financial_recovery_release_insert BEFORE INSERT ON financial_recovery_releases
                    FOR EACH ROW EXECUTE FUNCTION validate_financial_recovery_release();
                CREATE TRIGGER financial_recovery_releases_immutable BEFORE UPDATE OR DELETE ON financial_recovery_releases
                    FOR EACH ROW EXECUTE FUNCTION reject_financial_history_change();
                CREATE TRIGGER financial_recovery_releases_no_truncate BEFORE TRUNCATE ON financial_recovery_releases
                    FOR EACH STATEMENT EXECUTE FUNCTION reject_financial_history_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financial_recovery_releases");
            migrationBuilder.Sql("DROP FUNCTION validate_financial_recovery_release();");
        }
    }
}
