using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerAndPlatformFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "carrier_shipping_fee",
                table: "deliveries",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "commission_rate",
                table: "deliveries",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    garden_store_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    available_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: true),
                    refund_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vendor_liability_id = table.Column<Guid>(type: "uuid", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.id);
                    table.CheckConstraint("ck_ledger_entries_garden_account", "(account = 'GardenStore' AND garden_store_id IS NOT NULL) OR (account = 'Platform' AND garden_store_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_ledger_entries_deliveries_delivery_id",
                        column: x => x.delivery_id,
                        principalTable: "deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ledger_entries_garden_stores_garden_store_id",
                        column: x => x.garden_store_id,
                        principalTable: "garden_stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ledger_entries_refunds_refund_id",
                        column: x => x.refund_id,
                        principalTable: "refunds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ledger_entries_vendor_liabilities_vendor_liability_id",
                        column: x => x.vendor_liability_id,
                        principalTable: "vendor_liabilities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_balance",
                table: "ledger_entries",
                columns: new[] { "account", "garden_store_id", "available_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_delivery_id",
                table: "ledger_entries",
                column: "delivery_id");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_garden_store_id",
                table: "ledger_entries",
                column: "garden_store_id");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_refund_id",
                table: "ledger_entries",
                column: "refund_id");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_vendor_liability_id",
                table: "ledger_entries",
                column: "vendor_liability_id");

            migrationBuilder.CreateIndex(
                name: "ux_ledger_entries_idempotency_key",
                table: "ledger_entries",
                column: "idempotency_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "carrier_shipping_fee",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "commission_rate",
                table: "deliveries");
        }
    }
}
