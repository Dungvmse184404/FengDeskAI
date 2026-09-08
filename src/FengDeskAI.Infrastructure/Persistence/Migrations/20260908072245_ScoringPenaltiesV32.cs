using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// v3.2 §8.3 — nhân đôi 4 penalty cho DB ĐÃ seed từ trước.
    ///
    /// <para>
    /// <c>ScoringParamSeeder</c> chỉ CHÈN code mới, không đồng bộ giá trị của code đã có — cố ý, vì
    /// <c>scoring_params</c> là bảng admin chỉnh runtime qua <c>PUT /api/admin/scoring/params/{code}</c>;
    /// seeder ghi đè mỗi lần khởi động sẽ xoá sạch hiệu chỉnh của admin. Hệ quả: sửa
    /// <c>seed-data/scoring-params.json</c> chỉ có tác dụng với DB dựng mới.
    /// </para>
    ///
    /// <para>
    /// Nhưng ×2 penalty KHÔNG phải hiệu chỉnh tuỳ ý — nó là nửa còn lại của việc nở miền <c>gapScore</c>
    /// từ ±0.5 lên ±1.0. Bỏ qua thì penalty chỉ còn nặng bằng một nửa so với thiết kế. Nên đây là
    /// <b>data migration</b>, không phải việc của seeder.
    /// </para>
    ///
    /// <para>
    /// <b>Chỉ đụng row còn nguyên giá trị v3.1.</b> Admin đã tự chỉnh sang số khác thì giữ nguyên —
    /// mệnh đề <c>WHERE value = &lt;giá trị cũ&gt;</c> vừa tôn trọng hiệu chỉnh vừa làm migration
    /// idempotent (DB seed sau khi file JSON đã đổi cũng không bị đụng tới).
    /// </para>
    /// </summary>
    public partial class ScoringPenaltiesV32 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Rescale(migrationBuilder, "USER_CONFLICT_PENALTY", from: "0.300", to: "0.600");
            Rescale(migrationBuilder, "DIRECTION_PENALTY", from: "0.150", to: "0.300");
            Rescale(migrationBuilder, "VIBE_MISMATCH_PENALTY", from: "0.200", to: "0.400");
            Rescale(migrationBuilder, "VIBE_UNKNOWN_PENALTY", from: "0.050", to: "0.100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Rescale(migrationBuilder, "USER_CONFLICT_PENALTY", from: "0.600", to: "0.300");
            Rescale(migrationBuilder, "DIRECTION_PENALTY", from: "0.300", to: "0.150");
            Rescale(migrationBuilder, "VIBE_MISMATCH_PENALTY", from: "0.400", to: "0.200");
            Rescale(migrationBuilder, "VIBE_UNKNOWN_PENALTY", from: "0.100", to: "0.050");
        }

        private static void Rescale(MigrationBuilder builder, string code, string from, string to) =>
            builder.Sql($"""
                UPDATE scoring_params
                   SET value = {to}, updated_at = now()
                 WHERE code = '{code}' AND value = {from} AND is_deleted = false;
                """);
    }
}
