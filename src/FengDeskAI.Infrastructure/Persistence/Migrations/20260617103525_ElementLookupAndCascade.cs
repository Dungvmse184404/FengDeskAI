using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ElementLookupAndCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "elements",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_elements", x => x.code);
                });

            // Seed 5 ngũ hành (code khớp enum FengShuiElement) TRƯỚC khi gắn FK.
            migrationBuilder.Sql(@"
INSERT INTO elements (code, name, is_active, sort_order) VALUES
  ('Kim','Kim (Metal)',true,1),
  ('Moc','Mộc (Wood)',true,2),
  ('Thuy','Thủy (Water)',true,3),
  ('Hoa','Hỏa (Fire)',true,4),
  ('Tho','Thổ (Earth)',true,5)
ON CONFLICT (code) DO NOTHING;");

            // FK các cột ngũ hành → elements.code (CLR vẫn là enum-string, engine giữ nguyên),
            // ON UPDATE CASCADE để đổi code lan truyền; ON DELETE RESTRICT để không xoá hành đang dùng.
            migrationBuilder.Sql(@"
ALTER TABLE product_element
  ADD CONSTRAINT ""FK_product_element_elements_element""
  FOREIGN KEY (element) REFERENCES elements (code) ON UPDATE CASCADE ON DELETE RESTRICT;

ALTER TABLE workspace_profiles
  ADD CONSTRAINT ""FK_workspace_profiles_elements_feng_shui_element""
  FOREIGN KEY (feng_shui_element) REFERENCES elements (code) ON UPDATE CASCADE ON DELETE RESTRICT;

ALTER TABLE feng_shui_rules
  ADD CONSTRAINT ""FK_feng_shui_rules_elements_subject""
  FOREIGN KEY (subject_element) REFERENCES elements (code) ON UPDATE CASCADE ON DELETE RESTRICT;

ALTER TABLE feng_shui_rules
  ADD CONSTRAINT ""FK_feng_shui_rules_elements_object""
  FOREIGN KEY (object_element) REFERENCES elements (code) ON UPDATE CASCADE ON DELETE RESTRICT;");

            // Nâng cấp FK style/vibe sang ON UPDATE CASCADE (giữ DELETE RESTRICT) — để rename code lan truyền.
            migrationBuilder.Sql(@"
ALTER TABLE product_styles DROP CONSTRAINT ""FK_product_styles_styles_style_code"";
ALTER TABLE product_styles ADD CONSTRAINT ""FK_product_styles_styles_style_code""
  FOREIGN KEY (style_code) REFERENCES styles (code) ON UPDATE CASCADE ON DELETE RESTRICT;

ALTER TABLE product_vibes DROP CONSTRAINT ""FK_product_vibes_vibes_vibe_code"";
ALTER TABLE product_vibes ADD CONSTRAINT ""FK_product_vibes_vibes_vibe_code""
  FOREIGN KEY (vibe_code) REFERENCES vibes (code) ON UPDATE CASCADE ON DELETE RESTRICT;

ALTER TABLE workspace_profiles DROP CONSTRAINT ""FK_workspace_profiles_styles_style_code"";
ALTER TABLE workspace_profiles ADD CONSTRAINT ""FK_workspace_profiles_styles_style_code""
  FOREIGN KEY (style_code) REFERENCES styles (code) ON UPDATE CASCADE ON DELETE RESTRICT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Gỡ FK ngũ hành + hạ style/vibe về NO ACTION trước khi drop bảng elements.
            migrationBuilder.Sql(@"
ALTER TABLE product_element DROP CONSTRAINT IF EXISTS ""FK_product_element_elements_element"";
ALTER TABLE workspace_profiles DROP CONSTRAINT IF EXISTS ""FK_workspace_profiles_elements_feng_shui_element"";
ALTER TABLE feng_shui_rules DROP CONSTRAINT IF EXISTS ""FK_feng_shui_rules_elements_subject"";
ALTER TABLE feng_shui_rules DROP CONSTRAINT IF EXISTS ""FK_feng_shui_rules_elements_object"";

ALTER TABLE product_styles DROP CONSTRAINT ""FK_product_styles_styles_style_code"";
ALTER TABLE product_styles ADD CONSTRAINT ""FK_product_styles_styles_style_code""
  FOREIGN KEY (style_code) REFERENCES styles (code) ON DELETE RESTRICT;

ALTER TABLE product_vibes DROP CONSTRAINT ""FK_product_vibes_vibes_vibe_code"";
ALTER TABLE product_vibes ADD CONSTRAINT ""FK_product_vibes_vibes_vibe_code""
  FOREIGN KEY (vibe_code) REFERENCES vibes (code) ON DELETE RESTRICT;

ALTER TABLE workspace_profiles DROP CONSTRAINT ""FK_workspace_profiles_styles_style_code"";
ALTER TABLE workspace_profiles ADD CONSTRAINT ""FK_workspace_profiles_styles_style_code""
  FOREIGN KEY (style_code) REFERENCES styles (code) ON DELETE RESTRICT;");

            migrationBuilder.DropTable(
                name: "elements");
        }
    }
}
