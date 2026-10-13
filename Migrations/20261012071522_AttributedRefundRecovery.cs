using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alpha.API.Migrations
{
    /// <inheritdoc />
    public partial class AttributedRefundRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "recovery_offset",
                table: "settlement_queue",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "refund_adjustment",
                table: "settlement_queue",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "allocation_fingerprint",
                table: "payment_operations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "refund_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payee_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    payee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    future_recovery = table.Column<bool>(type: "boolean", nullable: false),
                    recovered_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refund_allocations", x => x.id);
                    table.ForeignKey(
                        name: "FK_refund_allocations_payment_operations_operation_id",
                        column: x => x.operation_id,
                        principalTable: "payment_operations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payout_recovery_offsets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payout_recovery_offsets", x => x.id);
                    table.ForeignKey(
                        name: "FK_payout_recovery_offsets_refund_allocations_allocation_id",
                        column: x => x.allocation_id,
                        principalTable: "refund_allocations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payout_recovery_offsets_allocation_id_target_kind_target_id",
                table: "payout_recovery_offsets",
                columns: new[] { "allocation_id", "target_kind", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refund_allocations_operation_id_source_kind_source_id",
                table: "refund_allocations",
                columns: new[] { "operation_id", "source_kind", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refund_allocations_payee_type_payee_id_currency",
                table: "refund_allocations",
                columns: new[] { "payee_type", "payee_id", "currency" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payout_recovery_offsets");

            migrationBuilder.DropTable(
                name: "refund_allocations");

            migrationBuilder.DropColumn(
                name: "recovery_offset",
                table: "settlement_queue");

            migrationBuilder.DropColumn(
                name: "refund_adjustment",
                table: "settlement_queue");

            migrationBuilder.DropColumn(
                name: "allocation_fingerprint",
                table: "payment_operations");
        }
    }
}
