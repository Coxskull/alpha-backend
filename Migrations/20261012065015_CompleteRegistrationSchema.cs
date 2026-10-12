using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Alpha.API.Migrations
{
    /// <inheritdoc />
    public partial class CompleteRegistrationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "community_builder_cities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    builder_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: true),
                    country = table.Column<string>(type: "text", nullable: false),
                    member_count = table.Column<int>(type: "integer", nullable: false),
                    active_member_count = table.Column<int>(type: "integer", nullable: false),
                    completed_transaction_count = table.Column<int>(type: "integer", nullable: false),
                    connected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_builder_cities", x => x.id);
                    table.ForeignKey(
                        name: "FK_community_builder_cities_users_builder_user_id",
                        column: x => x.builder_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_builder_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    home_city = table.Column<string>(type: "text", nullable: false),
                    home_state = table.Column<string>(type: "text", nullable: true),
                    country = table.Column<string>(type: "text", nullable: false),
                    builder_status = table.Column<string>(type: "text", nullable: false),
                    total_cities_connected = table.Column<int>(type: "integer", nullable: false),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_builder_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_community_builder_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "member_business_activity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_orders = table.Column<int>(type: "integer", nullable: false),
                    completed_service_requests = table.Column<int>(type: "integer", nullable: false),
                    completed_deliveries = table.Column<int>(type: "integer", nullable: false),
                    fulfilled_parts_orders = table.Column<int>(type: "integer", nullable: false),
                    customer_purchases = table.Column<int>(type: "integer", nullable: false),
                    gross_transaction_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    first_business_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_business_activity_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_business_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_business_activity", x => x.id);
                    table.ForeignKey(
                        name: "FK_member_business_activity_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "entrepreneur_roles",
                columns: new[] { "id", "created_at", "description", "display_name", "icon", "is_active", "is_operational_role", "role_key", "sort_order" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), null, "Vehicle Owner", null, true, false, "customer", 1 },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), null, "Motorcycle Rider", null, true, true, "driver", 2 },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), null, "Mechanic", null, true, true, "mechanic", 3 },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), null, "Auto Parts Store", null, true, true, "supplier", 4 },
                    { new Guid("00000000-0000-0000-0000-000000000005"), new DateTime(2026, 10, 12, 0, 0, 0, 0, DateTimeKind.Utc), null, "Community Builder", null, true, false, "community_builder", 5 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_community_builder_cities_builder_user_id_city_state_country",
                table: "community_builder_cities",
                columns: new[] { "builder_user_id", "city", "state", "country" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_community_builder_profiles_user_id",
                table: "community_builder_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_member_business_activity_user_id",
                table: "member_business_activity",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_builder_cities");

            migrationBuilder.DropTable(
                name: "community_builder_profiles");

            migrationBuilder.DropTable(
                name: "member_business_activity");

            migrationBuilder.DeleteData(
                table: "entrepreneur_roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "entrepreneur_roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "entrepreneur_roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "entrepreneur_roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "entrepreneur_roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"));
        }
    }
}
