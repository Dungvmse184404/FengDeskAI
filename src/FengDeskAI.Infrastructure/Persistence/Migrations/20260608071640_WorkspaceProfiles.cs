using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workspace_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    location_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    style = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    lighting = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    desk_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    desk_orientation = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    room_facing_direction = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    work_purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    feng_shui_element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    desk_area = table.Column<int>(type: "integer", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_workspace_profiles_user_default",
                table: "workspace_profiles",
                column: "user_id",
                unique: true,
                filter: "is_default = TRUE AND is_deleted = FALSE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workspace_profiles");
        }
    }
}
