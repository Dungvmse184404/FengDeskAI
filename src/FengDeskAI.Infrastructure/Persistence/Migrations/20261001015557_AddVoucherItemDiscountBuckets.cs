using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherItemDiscountBuckets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "platform_item_discount",
                table: "orders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "seller_item_discount",
                table: "orders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "platform_item_discount",
                table: "order_store_charges",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "seller_item_discount",
                table: "order_store_charges",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "platform_item_discount",
                table: "deliveries",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "seller_item_discount",
                table: "deliveries",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "platform_item_discount",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "seller_item_discount",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "platform_item_discount",
                table: "order_store_charges");

            migrationBuilder.DropColumn(
                name: "seller_item_discount",
                table: "order_store_charges");

            migrationBuilder.DropColumn(
                name: "platform_item_discount",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "seller_item_discount",
                table: "deliveries");
        }
    }
}
