using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewOrderItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_reviews_user_product",
                table: "reviews");

            migrationBuilder.AddColumn<Guid>(
                name: "order_item_id",
                table: "reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_reviews_user_product",
                table: "reviews",
                columns: new[] { "user_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "UX_reviews_order_item",
                table: "reviews",
                column: "order_item_id",
                unique: true,
                filter: "is_deleted = FALSE AND order_item_id IS NOT NULL");

            // Gắn đánh giá cũ vào dòng đơn của chính người viết. Seeder chạy lại mỗi deploy để vá đánh giá do
            // container cũ tạo trong khoảng migrate → đổi container.
            migrationBuilder.Sql(Seeding.OrderSnapshotBackfillSeeder.ReviewOrderItemsSql);

            migrationBuilder.AddForeignKey(
                name: "FK_reviews_order_items_order_item_id",
                table: "reviews",
                column: "order_item_id",
                principalTable: "order_items",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_reviews_order_items_order_item_id",
                table: "reviews");

            migrationBuilder.DropIndex(
                name: "IX_reviews_user_product",
                table: "reviews");

            migrationBuilder.DropIndex(
                name: "UX_reviews_order_item",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "order_item_id",
                table: "reviews");

            migrationBuilder.CreateIndex(
                name: "UX_reviews_user_product",
                table: "reviews",
                columns: new[] { "user_id", "product_id" },
                unique: true,
                filter: "is_deleted = FALSE");
        }
    }
}
