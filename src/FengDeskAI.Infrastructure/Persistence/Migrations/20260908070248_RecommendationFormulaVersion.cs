using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecommendationFormulaVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "formula_version",
                table: "recommendations",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "3.1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "formula_version",
                table: "recommendations");
        }
    }
}
