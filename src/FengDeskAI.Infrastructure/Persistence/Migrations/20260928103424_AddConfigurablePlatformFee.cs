using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurablePlatformFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "commission_rate",
                table: "orders",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                // 0.08 = mức cố định trước đây: đơn cũ đã được báo giá/tính trần voucher theo đúng số này, và đơn do
                // container CŨ tạo trong lúc CI vừa migrate xong chưa kịp đổi container cũng nhận đúng số đó.
                // Model KHÔNG khai default (xem OrderConfiguration) nên code mới luôn ghi tỉ lệ tường minh.
                defaultValue: 0.08m);

            migrationBuilder.CreateTable(
                name: "platform_fee_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    commission_rate = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_fee_rates", x => x.id);
                    table.CheckConstraint("ck_platform_fee_rates_range", "commission_rate >= 0 AND commission_rate <= 0.3");
                });

            migrationBuilder.CreateIndex(
                name: "ix_platform_fee_rates_effective_from",
                table: "platform_fee_rates",
                column: "effective_from");

            // Mức khởi tạo = hằng số 8% trước đây; Manager đổi qua PUT /api/platform/fee-policy.
            migrationBuilder.Sql("""
                INSERT INTO platform_fee_rates (id, commission_rate, effective_from, note, created_at, updated_at, is_deleted)
                VALUES (gen_random_uuid(), 0.08, TIMESTAMPTZ '2026-01-01 00:00:00+00', 'Mức khởi tạo (trước đây cố định 8%)', now(), now(), false);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_fee_rates");

            migrationBuilder.DropColumn(
                name: "commission_rate",
                table: "orders");
        }
    }
}
