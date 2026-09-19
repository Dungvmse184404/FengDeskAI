using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Tag do user tự tạo ở bước intake trước đây ghi THẲNG vào vocabulary dùng chung — 1 user gõ sai
    /// là cả cộng đồng (và AI intake) học theo. Thêm cột phạm vi hiển thị 3 mức:
    /// <c>Pending</c> (chờ admin xem) · <c>Personal</c> (admin đã xem, giữ riêng cho người tạo) ·
    /// <c>Public</c> (tag chính thức).
    /// <para>
    /// Pending và Personal hiển thị giống nhau (chỉ người tạo thấy); tách ra để HÀNG ĐỢI DUYỆT
    /// (= Pending) luôn rút được về 0, thay vì phình mãi vì tag admin đã xem nhưng cố ý không duyệt.
    /// </para>
    /// Row đang có (seed hệ thống + tag user tạo trước đây) backfill = <c>Public</c> để không đổi hành vi cũ.
    /// </summary>
    public partial class ElementInputApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "visibility",
                table: "element_input_map",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.Sql("UPDATE element_input_map SET visibility = 'Public';");

            migrationBuilder.CreateIndex(
                name: "IX_element_input_map_visibility_created_by",
                table: "element_input_map",
                columns: new[] { "visibility", "created_by" },
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_element_input_map_visibility_created_by",
                table: "element_input_map");

            migrationBuilder.DropColumn(
                name: "visibility",
                table: "element_input_map");
        }
    }
}
