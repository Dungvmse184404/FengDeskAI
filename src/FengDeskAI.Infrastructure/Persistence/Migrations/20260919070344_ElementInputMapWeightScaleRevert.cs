using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Sửa dữ liệu <c>element_input_map</c> bị lẫn hai thang phiếu.
    ///
    /// <para>
    /// Commit <c>9fdf853</c> (2026-09-03) đặt <c>weightScale = 8.0</c> trong
    /// <c>seed-data/element-input-map.json</c>, trong khi <c>ElementInputMapSeeder</c> cố ý <b>không</b>
    /// cập nhật weight của dòng đã có. Kết quả: tag seed trước 09-03 nặng ≤ 1 phiếu, tag seed sau
    /// (<c>Laptop</c>, <c>OfficeChair</c>, …) nặng 8 phiếu — cùng một bước "hiện trạng phòng", tick hai
    /// tag mà một cái đè cái kia gấp 8 lần. Nền phòng (<c>INTERIOR_PRIOR_VOTES = 3</c>) và chủ nhân
    /// (<c>PERSON_PRESENCE_VOTES_* = 3/2/0</c>) được cân trên giả định <b>tag ≈ 1 phiếu</b>, nên thang 8
    /// phá cân đó cho mọi nguồn (xem <c>docs/glossary-scoring.md</c> §7.3).
    /// </para>
    ///
    /// <para>
    /// Weight hợp lệ của một dòng map tối đa là <c>1.0</c> (tỉ trọng hành của một tín hiệu), nên mọi dòng
    /// <c>weight &gt; 1.0</c> chắc chắn là dòng đã nhân 8 — chia lại đúng 8. Không canh theo ngày tạo vì
    /// môi trường khác có thể seed vào ngày khác. Kèm gỡ 7 dòng <c>BUDGET_*</c> trong
    /// <c>scoring_params</c>: tàn dư của mô hình "ngân sách tỉ trọng" đã bị §12 thay bằng mô hình phiếu,
    /// không còn đường code nào đọc (không có trong <c>ScoringParamCodes</c> lẫn seed).
    /// </para>
    ///
    /// <para>
    /// <c>Down</c> không nhân ngược: không phân biệt được dòng nào từng bị scale, và trạng thái "lẫn thang"
    /// không phải trạng thái muốn quay về.
    /// </para>
    /// </summary>
    public partial class ElementInputMapWeightScaleRevert : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE element_input_map
                SET weight = ROUND(weight / 8, 3),
                    updated_at = NOW()
                WHERE weight > 1.0;
                """);

            migrationBuilder.Sql("""
                DELETE FROM scoring_params
                WHERE code LIKE 'BUDGET\_%' ESCAPE '\';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cố ý để trống — xem ghi chú ở summary.
        }
    }
}
