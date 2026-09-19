using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// N3 — nghề nghiệp thành trục thứ ba (ADR <c>occupation-product-fit-v1.md</c>).
    ///
    /// <para>
    /// <c>occupation_element_modifiers(delta)</c> → <c>occupation_element_profiles(share)</c>: cùng hình
    /// dạng bảng nhưng con số đổi nghĩa (delta có dấu → tỉ trọng Σ=1), nên đổi tên để cái tên nói đúng
    /// thứ nó chứa. DROP + CREATE thay vì RENAME vì bảng cũ <b>rỗng có chủ ý</b> (P5.4 không seed delta,
    /// chờ chuyên gia) — không có dữ liệu để bảo toàn. Nếu môi trường nào đã nhập delta tay qua
    /// <c>PUT …/modifiers</c> thì delta đó không quy đổi được sang share, phải nhập lại theo hồ sơ.
    /// </para>
    ///
    /// <para>
    /// <c>scoring_params</c>: gỡ <c>OCCUPATION_SHARE</c> (N1 không còn đường code nào đọc) và chèn
    /// <c>OCCUPATION_WEIGHT = 0</c> (kill-switch, cùng cách <c>PersonPresenceVotes</c> đã đi).
    /// </para>
    /// </summary>
    public partial class OccupationAxisN3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "occupation_element_modifiers");

            migrationBuilder.CreateTable(
                name: "occupation_element_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occupation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    share = table.Column<decimal>(type: "numeric(4,3)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_occupation_element_profiles", x => x.id);
                    table.CheckConstraint("CK_occupation_element_profiles_share", "share >= 0 AND share <= 1");
                    table.ForeignKey(
                        name: "FK_occupation_element_profiles_occupations_occupation_id",
                        column: x => x.occupation_id,
                        principalTable: "occupations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_occupation_element_profiles_occupation_id_element",
                table: "occupation_element_profiles",
                columns: new[] { "occupation_id", "element" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.Sql("DELETE FROM scoring_params WHERE code = 'OCCUPATION_SHARE';");
            migrationBuilder.Sql("""
                INSERT INTO scoring_params (id, code, value, description, created_at, updated_at, is_deleted)
                SELECT gen_random_uuid(), 'OCCUPATION_WEIGHT', 0.00,
                       'Wo - phần nghề nghiệp trong hướng chấm điểm: d = (1-Wp-Wo)·ĝ + Wp·r + Wo·ô (phòng), (1-Wo)·n̂ + Wo·ô (Carry). Seed 0 = kill-switch; đích 0.20 sau golden set.',
                       now(), now(), false
                 WHERE NOT EXISTS (SELECT 1 FROM scoring_params WHERE code = 'OCCUPATION_WEIGHT');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM scoring_params WHERE code = 'OCCUPATION_WEIGHT';");
            migrationBuilder.Sql("""
                INSERT INTO scoring_params (id, code, value, description, created_at, updated_at, is_deleted)
                SELECT gen_random_uuid(), 'OCCUPATION_SHARE', 0.00,
                       'Tỉ trọng delta nghề nghiệp bẻ vào vector điểm quan hệ r (v3.2 §11). Seed 0 = kill-switch.',
                       now(), now(), false
                 WHERE NOT EXISTS (SELECT 1 FROM scoring_params WHERE code = 'OCCUPATION_SHARE');
                """);

            migrationBuilder.DropTable(
                name: "occupation_element_profiles");

            migrationBuilder.CreateTable(
                name: "occupation_element_modifiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occupation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    delta = table.Column<decimal>(type: "numeric(4,3)", nullable: false),
                    element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_occupation_element_modifiers", x => x.id);
                    table.ForeignKey(
                        name: "FK_occupation_element_modifiers_occupations_occupation_id",
                        column: x => x.occupation_id,
                        principalTable: "occupations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_occupation_element_modifiers_occupation_id_element",
                table: "occupation_element_modifiers",
                columns: new[] { "occupation_id", "element" },
                unique: true,
                filter: "is_deleted = false");
        }
    }
}
