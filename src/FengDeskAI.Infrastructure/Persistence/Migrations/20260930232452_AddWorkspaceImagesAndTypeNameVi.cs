using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceImagesAndTypeNameVi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "name_vi",
                table: "workspace_types",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "workspace_profile_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_profile_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_profile_images_workspace_profiles_workspace_profi~",
                        column: x => x.workspace_profile_id,
                        principalTable: "workspace_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_profile_images_workspace_profile_id",
                table: "workspace_profile_images",
                column: "workspace_profile_id");

            // Điền sẵn tên tiếng Việt cho loại hệ thống — WorkspaceTypeSeeder cũng đồng bộ cột này, nhưng
            // DB chỉ chạy migration (chưa seed) vẫn phải có nhãn đúng ngay.
            migrationBuilder.Sql(@"
                UPDATE workspace_types AS t SET name_vi = v.name_vi
                FROM (VALUES
                    ('Personal Desk', 'Bàn làm việc cá nhân'),
                    ('Home Office', 'Phòng làm việc tại nhà'),
                    ('Private Office', 'Văn phòng riêng'),
                    ('Meeting Room', 'Phòng họp'),
                    ('Co-working Booth', 'Khoang làm việc chung'),
                    ('Open Workspace', 'Khu làm việc mở'),
                    ('Reception / Lounge', 'Sảnh lễ tân'),
                    ('Kitchen', 'Bếp'),
                    ('Living Room', 'Phòng khách'),
                    ('Bedroom', 'Phòng ngủ'),
                    ('Dining Room', 'Phòng ăn'),
                    ('Kids Room', 'Phòng trẻ em'),
                    ('Balcony', 'Ban công'),
                    ('Home Gym', 'Góc tập tại nhà'),
                    ('Altar Room', 'Phòng thờ'),
                    ('Bathroom', 'Phòng tắm'),
                    ('Study Room', 'Phòng học'),
                    ('Home Theater', 'Phòng giải trí'),
                    ('Walk-in Closet', 'Phòng thay đồ'),
                    ('Garage', 'Nhà để xe'),
                    ('Rooftop Garden', 'Sân thượng'),
                    ('Guest Room', 'Phòng khách lưu trú'),
                    ('Meditation Room', 'Phòng thiền'),
                    ('Laundry Room', 'Phòng giặt')
                ) AS v(name, name_vi)
                WHERE t.name = v.name AND t.is_system_seeded;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workspace_profile_images");

            migrationBuilder.DropColumn(
                name: "name_vi",
                table: "workspace_types");
        }
    }
}
