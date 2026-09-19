using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Thêm cột nhãn tiếng Việt cho element_input_map — dùng làm DẪN CHỨNG hiển thị trên FE
    /// (chip tag, tooltip radar, 3 dòng nhận định) thay vì lộ code kỹ thuật (Wood/FishTank).
    /// Nullable: row cũ backfill qua seeder; tag do user tự tạo lưu đúng chữ user gõ.
    /// </summary>
    public partial class ElementInputLabelVi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "label_vi",
                table: "element_input_map",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "label_vi",
                table: "element_input_map");
        }
    }
}
