using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alpha.API.Migrations
{
    /// <inheritdoc />
    public partial class OrderRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_orders_driver_id",
                table: "orders",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_supplier_id",
                table: "orders",
                column: "supplier_id");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_drivers_driver_id",
                table: "orders",
                column: "driver_id",
                principalTable: "drivers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_suppliers_supplier_id",
                table: "orders",
                column: "supplier_id",
                principalTable: "suppliers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_drivers_driver_id",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_suppliers_supplier_id",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_driver_id",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_supplier_id",
                table: "orders");
        }
    }
}
