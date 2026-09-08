using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// v3.2 §10.8 — chèn 4 tham số PHIẾU mới cho DB đã seed từ trước.
    ///
    /// <para>
    /// <c>ScoringParamSeeder</c> chỉ chèn code mới, nên với DB dựng mới thì seed lo được. Nhưng DB đã
    /// tồn tại chỉ chạy migration chứ không chạy seeder, và thiếu row thì engine rơi về default trong
    /// code — đúng giá trị này, nhưng admin sẽ không thấy dòng nào để chỉnh. Chèn ở đây để bảng
    /// <c>scoring_params</c> phản ánh đủ mọi tham số engine đang đọc.
    /// </para>
    ///
    /// <para>
    /// <c>WHERE NOT EXISTS</c> giữ tính idempotent: DB nào đã seed sau khi file JSON có 4 mã này thì
    /// migration không đụng tới.
    /// </para>
    /// </summary>
    public partial class PersonPresenceVotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Insert(migrationBuilder, "INTERIOR_PRIOR_VOTES", "3.00",
                "Số phiếu của nền phòng khi dựng vector hiện trạng — prior Dirichlet, nặng ngang 3 tag thật.");
            Insert(migrationBuilder, "PERSON_PRESENCE_VOTES_PRIVATE", "3.00",
                "Số phiếu của CHỦ NHÂN phòng ở không gian Private — 3 = ngang nền phòng.");
            Insert(migrationBuilder, "PERSON_PRESENCE_VOTES_SHARED", "2.00",
                "Như trên, ở không gian Shared — nhẹ hơn vì phòng chia với người khác.");
            Insert(migrationBuilder, "PERSON_PRESENCE_VOTES_PUBLIC", "0.00",
                "Như trên, ở không gian Public — luôn 0: không gian chung không có chủ nhân.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                "DELETE FROM scoring_params WHERE code IN " +
                "('INTERIOR_PRIOR_VOTES','PERSON_PRESENCE_VOTES_PRIVATE'," +
                "'PERSON_PRESENCE_VOTES_SHARED','PERSON_PRESENCE_VOTES_PUBLIC');");

        private static void Insert(MigrationBuilder builder, string code, string value, string description) =>
            builder.Sql($"""
                INSERT INTO scoring_params (id, code, value, description, created_at, updated_at, is_deleted)
                SELECT gen_random_uuid(), '{code}', {value}, '{description}', now(), now(), false
                 WHERE NOT EXISTS (SELECT 1 FROM scoring_params WHERE code = '{code}');
                """);
    }
}
