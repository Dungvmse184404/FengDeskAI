using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLineSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cart_items_product_items_product_item_id",
                table: "cart_items");

            migrationBuilder.DropForeignKey(
                name: "FK_order_items_product_items_product_item_id",
                table: "order_items");

            migrationBuilder.DropForeignKey(
                name: "FK_recommendation_items_products_product_id",
                table: "recommendation_items");

            migrationBuilder.DropForeignKey(
                name: "FK_return_items_product_items_exchange_product_item_id",
                table: "return_items");

            migrationBuilder.DropForeignKey(
                name: "FK_reviews_products_product_id",
                table: "reviews");

            migrationBuilder.AlterColumn<Guid>(
                name: "product_id",
                table: "reviews",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "garden_store_id",
                table: "reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "product_name",
                table: "reviews",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "product_item_id",
                table: "order_items",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "garden_store_id",
                table: "order_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_url",
                table: "order_items",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "product_id",
                table: "order_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sku",
                table: "order_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "variant_name",
                table: "order_items",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_reviews_garden_store_id",
                table: "reviews",
                column: "garden_store_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_garden_store_id",
                table: "order_items",
                column: "garden_store_id");

            // Chụp lại dữ liệu cho đơn/đánh giá đã có (đọc qua sản phẩm, kể cả sản phẩm đã xoá mềm). Chạy TRƯỚC khi
            // đổi FK sang SET NULL. Seeder OrderSnapshotBackfillSeeder chạy lại cùng câu này ở mỗi lần deploy để vá
            // các dòng container cũ tạo trong khoảng migrate → đổi container.
            migrationBuilder.Sql(Seeding.OrderSnapshotBackfillSeeder.OrderItemsSql);
            migrationBuilder.Sql(Seeding.OrderSnapshotBackfillSeeder.ReviewsSql);

            migrationBuilder.AddForeignKey(
                name: "FK_cart_items_product_items_product_item_id",
                table: "cart_items",
                column: "product_item_id",
                principalTable: "product_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_product_items_product_item_id",
                table: "order_items",
                column: "product_item_id",
                principalTable: "product_items",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_recommendation_items_products_product_id",
                table: "recommendation_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_return_items_product_items_exchange_product_item_id",
                table: "return_items",
                column: "exchange_product_item_id",
                principalTable: "product_items",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_reviews_products_product_id",
                table: "reviews",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cart_items_product_items_product_item_id",
                table: "cart_items");

            migrationBuilder.DropForeignKey(
                name: "FK_order_items_product_items_product_item_id",
                table: "order_items");

            migrationBuilder.DropForeignKey(
                name: "FK_recommendation_items_products_product_id",
                table: "recommendation_items");

            migrationBuilder.DropForeignKey(
                name: "FK_return_items_product_items_exchange_product_item_id",
                table: "return_items");

            migrationBuilder.DropForeignKey(
                name: "FK_reviews_products_product_id",
                table: "reviews");

            migrationBuilder.DropIndex(
                name: "IX_reviews_garden_store_id",
                table: "reviews");

            migrationBuilder.DropIndex(
                name: "IX_order_items_garden_store_id",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "garden_store_id",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "product_name",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "garden_store_id",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "image_url",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "product_id",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "sku",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "variant_name",
                table: "order_items");

            migrationBuilder.AlterColumn<Guid>(
                name: "product_id",
                table: "reviews",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "product_item_id",
                table: "order_items",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_cart_items_product_items_product_item_id",
                table: "cart_items",
                column: "product_item_id",
                principalTable: "product_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_items_product_items_product_item_id",
                table: "order_items",
                column: "product_item_id",
                principalTable: "product_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_recommendation_items_products_product_id",
                table: "recommendation_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_return_items_product_items_exchange_product_item_id",
                table: "return_items",
                column: "exchange_product_item_id",
                principalTable: "product_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_reviews_products_product_id",
                table: "reviews",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
