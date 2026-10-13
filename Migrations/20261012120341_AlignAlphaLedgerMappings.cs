using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alpha.API.Migrations
{
    /// <inheritdoc />
    public partial class AlignAlphaLedgerMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Refuse lossy financial narrowing. Existing exceptions require explicit review.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "EntrepreneurPayoutBatches"
                        WHERE length("Currency") > 10 OR length("Status") > 30
                           OR length("PayoutReference") > 200
                           OR "TotalAmount" <> round("TotalAmount", 2)
                           OR abs("TotalAmount") >= 10000000000000000)
                       OR EXISTS (SELECT 1 FROM "EntrepreneurReferralAudits" WHERE length("Reason") > 500)
                    THEN RAISE EXCEPTION 'Ledger mapping requires manual review of values exceeding the target precision or length';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_EntrepreneurReferralAudits",
                table: "EntrepreneurReferralAudits");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EntrepreneurPayoutBatches",
                table: "EntrepreneurPayoutBatches");

            migrationBuilder.RenameTable(
                name: "EntrepreneurReferralAudits",
                newName: "entrepreneur_referral_audits");

            migrationBuilder.RenameTable(
                name: "EntrepreneurPayoutBatches",
                newName: "entrepreneur_payout_batches");

            migrationBuilder.RenameColumn(
                name: "PhoneNumber",
                table: "customers",
                newName: "phone");

            migrationBuilder.RenameColumn(
                name: "VIN",
                table: "customer_vehicles",
                newName: "vin");

            migrationBuilder.RenameColumn(
                name: "Reason",
                table: "entrepreneur_referral_audits",
                newName: "reason");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "entrepreneur_referral_audits",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "OldEntrepreneurUserId",
                table: "entrepreneur_referral_audits",
                newName: "old_entrepreneur_user_id");

            migrationBuilder.RenameColumn(
                name: "NewEntrepreneurUserId",
                table: "entrepreneur_referral_audits",
                newName: "new_entrepreneur_user_id");

            migrationBuilder.RenameColumn(
                name: "EntrepreneurReferralId",
                table: "entrepreneur_referral_audits",
                newName: "entrepreneur_referral_id");

            migrationBuilder.RenameColumn(
                name: "ChangedByUserId",
                table: "entrepreneur_referral_audits",
                newName: "changed_by_user_id");

            migrationBuilder.RenameColumn(
                name: "ChangedAt",
                table: "entrepreneur_referral_audits",
                newName: "changed_at");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "entrepreneur_payout_batches",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Currency",
                table: "entrepreneur_payout_batches",
                newName: "currency");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "entrepreneur_payout_batches",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "TotalAmount",
                table: "entrepreneur_payout_batches",
                newName: "total_amount");

            migrationBuilder.RenameColumn(
                name: "SettlementDate",
                table: "entrepreneur_payout_batches",
                newName: "settlement_date");

            migrationBuilder.RenameColumn(
                name: "ProcessedByUserId",
                table: "entrepreneur_payout_batches",
                newName: "processed_by_user_id");

            migrationBuilder.RenameColumn(
                name: "PayoutReference",
                table: "entrepreneur_payout_batches",
                newName: "payout_reference");

            migrationBuilder.RenameColumn(
                name: "PaidAt",
                table: "entrepreneur_payout_batches",
                newName: "paid_at");

            migrationBuilder.RenameColumn(
                name: "EarningCount",
                table: "entrepreneur_payout_batches",
                newName: "earning_count");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "entrepreneur_payout_batches",
                newName: "created_at");

            migrationBuilder.AlterColumn<string>(
                name: "reason",
                table: "entrepreneur_referral_audits",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "entrepreneur_payout_batches",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "currency",
                table: "entrepreneur_payout_batches",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "total_amount",
                table: "entrepreneur_payout_batches",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "payout_reference",
                table: "entrepreneur_payout_batches",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_entrepreneur_referral_audits",
                table: "entrepreneur_referral_audits",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_entrepreneur_payout_batches",
                table: "entrepreneur_payout_batches",
                column: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_entrepreneur_referral_audits",
                table: "entrepreneur_referral_audits");

            migrationBuilder.DropPrimaryKey(
                name: "PK_entrepreneur_payout_batches",
                table: "entrepreneur_payout_batches");

            migrationBuilder.RenameTable(
                name: "entrepreneur_referral_audits",
                newName: "EntrepreneurReferralAudits");

            migrationBuilder.RenameTable(
                name: "entrepreneur_payout_batches",
                newName: "EntrepreneurPayoutBatches");

            migrationBuilder.RenameColumn(
                name: "phone",
                table: "customers",
                newName: "PhoneNumber");

            migrationBuilder.RenameColumn(
                name: "vin",
                table: "customer_vehicles",
                newName: "VIN");

            migrationBuilder.RenameColumn(
                name: "reason",
                table: "EntrepreneurReferralAudits",
                newName: "Reason");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "EntrepreneurReferralAudits",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "old_entrepreneur_user_id",
                table: "EntrepreneurReferralAudits",
                newName: "OldEntrepreneurUserId");

            migrationBuilder.RenameColumn(
                name: "new_entrepreneur_user_id",
                table: "EntrepreneurReferralAudits",
                newName: "NewEntrepreneurUserId");

            migrationBuilder.RenameColumn(
                name: "entrepreneur_referral_id",
                table: "EntrepreneurReferralAudits",
                newName: "EntrepreneurReferralId");

            migrationBuilder.RenameColumn(
                name: "changed_by_user_id",
                table: "EntrepreneurReferralAudits",
                newName: "ChangedByUserId");

            migrationBuilder.RenameColumn(
                name: "changed_at",
                table: "EntrepreneurReferralAudits",
                newName: "ChangedAt");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "EntrepreneurPayoutBatches",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "currency",
                table: "EntrepreneurPayoutBatches",
                newName: "Currency");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "EntrepreneurPayoutBatches",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "total_amount",
                table: "EntrepreneurPayoutBatches",
                newName: "TotalAmount");

            migrationBuilder.RenameColumn(
                name: "settlement_date",
                table: "EntrepreneurPayoutBatches",
                newName: "SettlementDate");

            migrationBuilder.RenameColumn(
                name: "processed_by_user_id",
                table: "EntrepreneurPayoutBatches",
                newName: "ProcessedByUserId");

            migrationBuilder.RenameColumn(
                name: "payout_reference",
                table: "EntrepreneurPayoutBatches",
                newName: "PayoutReference");

            migrationBuilder.RenameColumn(
                name: "paid_at",
                table: "EntrepreneurPayoutBatches",
                newName: "PaidAt");

            migrationBuilder.RenameColumn(
                name: "earning_count",
                table: "EntrepreneurPayoutBatches",
                newName: "EarningCount");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "EntrepreneurPayoutBatches",
                newName: "CreatedAt");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "EntrepreneurReferralAudits",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "EntrepreneurPayoutBatches",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                table: "EntrepreneurPayoutBatches",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                table: "EntrepreneurPayoutBatches",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AlterColumn<string>(
                name: "PayoutReference",
                table: "EntrepreneurPayoutBatches",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_EntrepreneurReferralAudits",
                table: "EntrepreneurReferralAudits",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EntrepreneurPayoutBatches",
                table: "EntrepreneurPayoutBatches",
                column: "Id");
        }
    }
}
