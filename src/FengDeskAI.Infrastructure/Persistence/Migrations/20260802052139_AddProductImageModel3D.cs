using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImageModel3D : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_product_model3ds_product_id",
                table: "product_model3ds");

            migrationBuilder.AddColumn<Guid>(
                name: "product_image_id",
                table: "product_model3ds",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "product_image_id",
                table: "model3d_requests",
                type: "uuid",
                nullable: true);

            // Gắn dữ liệu cũ vào đúng ảnh nguồn; fallback về ảnh đầu tiên nếu URL cũ không còn khớp.
            migrationBuilder.Sql("""
                UPDATE product_model3ds AS model
                SET product_image_id = COALESCE(
                    (SELECT image.id FROM product_images AS image
                     WHERE image.product_id = model.product_id
                       AND image.url = model.source_image_url AND NOT image.is_deleted
                     ORDER BY image.sort_order, image.created_at LIMIT 1),
                    (SELECT image.id FROM product_images AS image
                     WHERE image.product_id = model.product_id AND NOT image.is_deleted
                     ORDER BY image.sort_order, image.created_at LIMIT 1)
                );

                UPDATE model3d_requests AS request
                SET product_image_id = COALESCE(
                    (SELECT image.id FROM product_images AS image
                     WHERE image.product_id = request.product_id
                       AND image.id = ANY(request.source_image_ids) AND NOT image.is_deleted
                     ORDER BY array_position(request.source_image_ids, image.id) LIMIT 1),
                    (SELECT model.product_image_id FROM product_model3ds AS model
                     WHERE model.product_id = request.product_id AND NOT model.is_deleted
                     ORDER BY model.updated_at DESC LIMIT 1),
                    (SELECT image.id FROM product_images AS image
                     WHERE image.product_id = request.product_id AND NOT image.is_deleted
                     ORDER BY image.sort_order, image.created_at LIMIT 1)
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_product_model3ds_product_id",
                table: "product_model3ds",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_model3ds_product_image_id",
                table: "product_model3ds",
                column: "product_image_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_model3d_requests_product_image_id",
                table: "model3d_requests",
                column: "product_image_id");

            migrationBuilder.AddForeignKey(
                name: "FK_model3d_requests_product_images_product_image_id",
                table: "model3d_requests",
                column: "product_image_id",
                principalTable: "product_images",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_product_model3ds_product_images_product_image_id",
                table: "product_model3ds",
                column: "product_image_id",
                principalTable: "product_images",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_model3d_requests_product_images_product_image_id",
                table: "model3d_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_product_model3ds_product_images_product_image_id",
                table: "product_model3ds");

            migrationBuilder.DropIndex(
                name: "IX_product_model3ds_product_id",
                table: "product_model3ds");

            migrationBuilder.DropIndex(
                name: "IX_product_model3ds_product_image_id",
                table: "product_model3ds");

            migrationBuilder.DropIndex(
                name: "IX_model3d_requests_product_image_id",
                table: "model3d_requests");

            migrationBuilder.DropColumn(
                name: "product_image_id",
                table: "product_model3ds");

            migrationBuilder.DropColumn(
                name: "product_image_id",
                table: "model3d_requests");

            migrationBuilder.CreateIndex(
                name: "IX_product_model3ds_product_id",
                table: "product_model3ds",
                column: "product_id",
                unique: true);
        }
    }
}
