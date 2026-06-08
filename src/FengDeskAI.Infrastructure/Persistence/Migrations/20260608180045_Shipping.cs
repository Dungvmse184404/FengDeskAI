using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Shipping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "delivery_progress_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<int>(type: "integer", nullable: false),
                    from_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    to_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    raw_payload = table.Column<string>(type: "jsonb", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    logged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_progress_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_delivery_progress_logs_deliveries_delivery_id",
                        column: x => x.delivery_id,
                        principalTable: "deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shipping_webhook",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    is_processed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipping_webhook", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_delivery_progress_logs_delivery_id",
                table: "delivery_progress_logs",
                column: "delivery_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipping_webhook_is_processed",
                table: "shipping_webhook",
                column: "is_processed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery_progress_logs");

            migrationBuilder.DropTable(
                name: "shipping_webhook");
        }
    }
}
