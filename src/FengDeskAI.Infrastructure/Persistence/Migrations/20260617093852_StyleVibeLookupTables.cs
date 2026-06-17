using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StyleVibeLookupTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "style",
                table: "workspace_profiles",
                newName: "style_code");

            migrationBuilder.RenameColumn(
                name: "vibe",
                table: "product_vibes",
                newName: "vibe_code");

            migrationBuilder.RenameColumn(
                name: "style",
                table: "product_styles",
                newName: "style_code");

            migrationBuilder.CreateTable(
                name: "styles",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_styles", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "vibes",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vibes", x => x.code);
                });

            // Seed code canonical TRƯỚC khi thêm FK, để dữ liệu cũ (product_styles/vibes, workspace_profiles)
            // có target hợp lệ. Trùng tên enum cũ nên mọi giá trị hiện có đều khớp.
            migrationBuilder.Sql(@"
INSERT INTO styles (code, name, is_active, sort_order) VALUES
  ('Modern','Hiện đại',true,1),
  ('Classic','Cổ điển',true,2),
  ('Minimal','Tối giản',true,3),
  ('Industrial','Công nghiệp',true,4),
  ('Scandinavian','Bắc Âu',true,5),
  ('Bohemian','Bohemian',true,6),
  ('Other','Khác',true,99)
ON CONFLICT (code) DO NOTHING;

INSERT INTO vibes (code, name, is_active, sort_order) VALUES
  ('Focus','Tập trung',true,1),
  ('Relax','Thư giãn',true,2),
  ('Creative','Sáng tạo',true,3),
  ('Calm','Tĩnh tại',true,4),
  ('Energize','Năng lượng',true,5)
ON CONFLICT (code) DO NOTHING;");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_profiles_style_code",
                table: "workspace_profiles",
                column: "style_code");

            migrationBuilder.CreateIndex(
                name: "IX_product_vibes_vibe_code",
                table: "product_vibes",
                column: "vibe_code");

            migrationBuilder.CreateIndex(
                name: "IX_product_styles_style_code",
                table: "product_styles",
                column: "style_code");

            migrationBuilder.AddForeignKey(
                name: "FK_product_styles_styles_style_code",
                table: "product_styles",
                column: "style_code",
                principalTable: "styles",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_product_vibes_vibes_vibe_code",
                table: "product_vibes",
                column: "vibe_code",
                principalTable: "vibes",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_workspace_profiles_styles_style_code",
                table: "workspace_profiles",
                column: "style_code",
                principalTable: "styles",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_product_styles_styles_style_code",
                table: "product_styles");

            migrationBuilder.DropForeignKey(
                name: "FK_product_vibes_vibes_vibe_code",
                table: "product_vibes");

            migrationBuilder.DropForeignKey(
                name: "FK_workspace_profiles_styles_style_code",
                table: "workspace_profiles");

            migrationBuilder.DropTable(
                name: "styles");

            migrationBuilder.DropTable(
                name: "vibes");

            migrationBuilder.DropIndex(
                name: "IX_workspace_profiles_style_code",
                table: "workspace_profiles");

            migrationBuilder.DropIndex(
                name: "IX_product_vibes_vibe_code",
                table: "product_vibes");

            migrationBuilder.DropIndex(
                name: "IX_product_styles_style_code",
                table: "product_styles");

            migrationBuilder.RenameColumn(
                name: "style_code",
                table: "workspace_profiles",
                newName: "style");

            migrationBuilder.RenameColumn(
                name: "vibe_code",
                table: "product_vibes",
                newName: "vibe");

            migrationBuilder.RenameColumn(
                name: "style_code",
                table: "product_styles",
                newName: "style");
        }
    }
}
