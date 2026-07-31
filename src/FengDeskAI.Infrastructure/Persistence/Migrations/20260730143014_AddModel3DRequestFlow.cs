using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModel3DRequestFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_enabled",
                table: "product_model3ds",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "model3d_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    source_image_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    meshy_task_id = table.Column<string>(type: "text", nullable: true),
                    assigned_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    internal_failure_reason = table.Column<string>(type: "text", nullable: true),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model3d_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_model3d_requests_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_model3d_requests_product_id",
                table: "model3d_requests",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_model3d_requests_status",
                table: "model3d_requests",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "model3d_requests");

            migrationBuilder.DropColumn(
                name: "is_enabled",
                table: "product_model3ds");
        }
    }
}
