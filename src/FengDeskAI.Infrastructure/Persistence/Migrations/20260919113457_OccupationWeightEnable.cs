using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// P6.6 (ADR <c>occupation-product-fit-v1.md</c>): bật trục nghề — <c>OCCUPATION_WEIGHT</c> 0 → 0.20 sau khi
    /// soát golden set (<c>OccupationGoldenSetTests</c>). Canh <c>value = 0</c> để không đè admin đã chỉnh tay.
    /// </summary>
    public partial class OccupationWeightEnable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
            => migrationBuilder.Sql("UPDATE scoring_params SET value = 0.200, updated_at = now() WHERE code = 'OCCUPATION_WEIGHT' AND value = 0.000 AND is_deleted = false;");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => migrationBuilder.Sql("UPDATE scoring_params SET value = 0.000, updated_at = now() WHERE code = 'OCCUPATION_WEIGHT' AND value = 0.200 AND is_deleted = false;");
    }
}
