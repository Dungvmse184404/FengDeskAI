using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorReturnRmaFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "decided_at",
                table: "return_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "decided_by",
                table: "return_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "evidence_deadline",
                table: "return_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vendor_response",
                table: "return_requests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "vendor_response_deadline",
                table: "return_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "evidence_url",
                table: "refunds",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gateway",
                table: "refunds",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "payos");

            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "refunds",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_manual",
                table: "refunds",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "manual_reason",
                table: "refunds",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "performed_by",
                table: "refunds",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "retry_count",
                table: "refunds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_exchange",
                table: "deliveries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "vendor_liabilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    garden_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refund_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    dispute_reason = table.Column<string>(type: "text", nullable: true),
                    dispute_deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendor_liabilities", x => x.id);
                    table.ForeignKey(
                        name: "FK_vendor_liabilities_garden_stores_garden_id",
                        column: x => x.garden_id,
                        principalTable: "garden_stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vendor_liabilities_refunds_refund_id",
                        column: x => x.refund_id,
                        principalTable: "refunds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vendor_liabilities_return_requests_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "return_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_refunds_idempotency_key",
                table: "refunds",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vendor_liabilities_garden_id",
                table: "vendor_liabilities",
                column: "garden_id");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_liabilities_refund_id",
                table: "vendor_liabilities",
                column: "refund_id");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_liabilities_status",
                table: "vendor_liabilities",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_liabilities_ticket_id",
                table: "vendor_liabilities",
                column: "ticket_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vendor_liabilities");

            migrationBuilder.DropIndex(
                name: "IX_refunds_idempotency_key",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "decided_at",
                table: "return_requests");

            migrationBuilder.DropColumn(
                name: "decided_by",
                table: "return_requests");

            migrationBuilder.DropColumn(
                name: "evidence_deadline",
                table: "return_requests");

            migrationBuilder.DropColumn(
                name: "vendor_response",
                table: "return_requests");

            migrationBuilder.DropColumn(
                name: "vendor_response_deadline",
                table: "return_requests");

            migrationBuilder.DropColumn(
                name: "evidence_url",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "gateway",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "is_manual",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "manual_reason",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "performed_by",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "retry_count",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "is_exchange",
                table: "deliveries");
        }
    }
}
