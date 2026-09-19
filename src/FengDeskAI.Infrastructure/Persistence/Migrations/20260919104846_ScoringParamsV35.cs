using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// v3.5 (ADR <c>current-tag-votes-cap-v3.5.md</c>): tham số trần phiếu tag + hạ trọng số bản mệnh.
    ///
    /// <para>
    /// <c>TAG_VOTES_CAP = 5</c> chèn nếu thiếu (seeder cũng chèn được, nhưng migration giữ mọi môi trường
    /// cùng nhịp). <c>PERSONAL_WEIGHT_PRIVATE</c> 0.50 → 0.30 và <c>SHARED</c> 0.30 → 0.20 đi kèm mệnh đề
    /// <c>WHERE value IN (&lt;các giá trị seed cũ&gt;)</c> như <c>ScoringPenaltiesV32</c>: admin đã chỉnh tay thì giữ
    /// nguyên, còn giá trị seed thì được kéo theo. <c>ScoringParamSeeder</c> chỉ chèn row thiếu, không đổi
    /// giá trị đã có — nên đổi số phải đi bằng migration.
    /// </para>
    ///
    /// <para>
    /// Canh <b>hai</b> giá trị cũ, không phải một: v3.1 seed <c>0.00</c> (kill-switch) rồi đổi seed lên
    /// <c>0.50</c>/<c>0.30</c> mà không có migration ⇒ môi trường seed trước đó (DB test, 2026-09-03) vẫn
    /// <c>0.000</c> — trục cá nhân tắt suốt trong khi code default nói 0.50. Đây chính là migration mà v3.1
    /// còn thiếu; từ đây mọi môi trường về cùng một số.
    /// </para>
    /// </summary>
    public partial class ScoringParamsV35 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO scoring_params (id, code, value, description, created_at, updated_at, is_deleted)
                SELECT gen_random_uuid(), 'TAG_VOTES_CAP', 5.00,
                       'v3.5 — trần TỔNG phiếu tag khi dựng current: Σ phiếu tag > cap thì mọi tag nhân cap/Σ. 5 = tối đa ngang nền + chủ nhân. ≤ 0 = tắt.',
                       now(), now(), false
                 WHERE NOT EXISTS (SELECT 1 FROM scoring_params WHERE code = 'TAG_VOTES_CAP');
                """);

            Rescale(migrationBuilder, "PERSONAL_WEIGHT_PRIVATE", from: "0.000, 0.500", to: "0.300");
            Rescale(migrationBuilder, "PERSONAL_WEIGHT_SHARED", from: "0.000, 0.300", to: "0.200");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Rescale(migrationBuilder, "PERSONAL_WEIGHT_PRIVATE", from: "0.300", to: "0.500");
            Rescale(migrationBuilder, "PERSONAL_WEIGHT_SHARED", from: "0.200", to: "0.300");

            migrationBuilder.Sql("DELETE FROM scoring_params WHERE code = 'TAG_VOTES_CAP';");
        }

        private static void Rescale(MigrationBuilder builder, string code, string from, string to) =>
            builder.Sql($"""
                UPDATE scoring_params
                   SET value = {to}, updated_at = now()
                 WHERE code = '{code}' AND value IN ({from}) AND is_deleted = false;
                """);
    }
}
