using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Chuyển GardenStaffAssignment thành invitation flow: thêm Status/RespondedAt,
    /// rename AssignedBy→InvitedBy, AssignedAt→InvitedAt, backfill Status từ IsActive rồi drop IsActive.
    /// Row cũ đang active → Status=Accepted; row đã unassign → Status=Revoked.
    /// </summary>
    public partial class StaffInvitationFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop index cũ (theo is_active) trước để thao tác cột thoải mái.
            migrationBuilder.DropIndex(
                name: "UX_garden_staff_active",
                table: "garden_staff_assignments");

            // 1) Rename cột cũ (không mất data).
            migrationBuilder.RenameColumn(
                name: "assigned_by",
                table: "garden_staff_assignments",
                newName: "invited_by");

            migrationBuilder.RenameColumn(
                name: "assigned_at",
                table: "garden_staff_assignments",
                newName: "invited_at");

            // 2) Thêm cột mới.
            migrationBuilder.AddColumn<DateTime>(
                name: "responded_at",
                table: "garden_staff_assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "garden_staff_assignments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            // 3) BACKFILL status từ is_active TRƯỚC KHI drop cột.
            //    - is_active = true  → Accepted (đã đang làm)
            //    - is_active = false → Revoked  (đã bị gỡ)
            //    Assumption: schema cũ không có "Pending" — assignment cũ tạo là active ngay.
            migrationBuilder.Sql(@"
                UPDATE garden_staff_assignments
                SET status = CASE WHEN is_active THEN 'Accepted' ELSE 'Revoked' END;
            ");
            // Với row đã Accepted trước đó, responded_at coi như trùng invited_at (best-effort).
            migrationBuilder.Sql(@"
                UPDATE garden_staff_assignments
                SET responded_at = invited_at
                WHERE status = 'Accepted';
            ");

            // 4) Giờ mới drop is_active.
            migrationBuilder.DropColumn(
                name: "is_active",
                table: "garden_staff_assignments");

            // 5) Tạo lại unique index theo status mới (chỉ 1 assignment sống — Pending hoặc Accepted — mỗi staff/store).
            migrationBuilder.CreateIndex(
                name: "UX_garden_staff_active",
                table: "garden_staff_assignments",
                columns: new[] { "garden_store_id", "staff_id" },
                unique: true,
                filter: "status IN ('Pending', 'Accepted') AND is_deleted = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_garden_staff_active",
                table: "garden_staff_assignments");

            // Add is_active lại + backfill từ status (chỉ Accepted mới coi là active).
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "garden_staff_assignments",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(@"
                UPDATE garden_staff_assignments
                SET is_active = (status = 'Accepted');
            ");

            migrationBuilder.DropColumn(
                name: "responded_at",
                table: "garden_staff_assignments");

            migrationBuilder.DropColumn(
                name: "status",
                table: "garden_staff_assignments");

            migrationBuilder.RenameColumn(
                name: "invited_by",
                table: "garden_staff_assignments",
                newName: "assigned_by");

            migrationBuilder.RenameColumn(
                name: "invited_at",
                table: "garden_staff_assignments",
                newName: "assigned_at");

            migrationBuilder.CreateIndex(
                name: "UX_garden_staff_active",
                table: "garden_staff_assignments",
                columns: new[] { "garden_store_id", "staff_id" },
                unique: true,
                filter: "is_active = TRUE AND is_deleted = FALSE");
        }
    }
}
