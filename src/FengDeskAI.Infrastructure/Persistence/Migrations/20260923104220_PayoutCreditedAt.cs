using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PayoutCreditedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mốc "đã cộng tiền hàng vào số dư chủ vườn" — khoá chống cộng hai lần của PayoutCreditService.
            migrationBuilder.AddColumn<DateTime>(
                name: "payout_credited_at",
                table: "deliveries",
                type: "timestamp with time zone",
                nullable: true);

            // Worker quét đúng bộ lọc này mỗi chu kỳ (đã giao + chưa cộng + quá hạn giữ).
            migrationBuilder.CreateIndex(
                name: "ix_deliveries_payout_scan",
                table: "deliveries",
                columns: new[] { "status", "payout_credited_at", "delivered_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_deliveries_payout_scan", table: "deliveries");
            migrationBuilder.DropColumn(name: "payout_credited_at", table: "deliveries");
        }
    }
}
