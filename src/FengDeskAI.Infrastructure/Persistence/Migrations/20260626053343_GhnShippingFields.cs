using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GhnShippingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ghn_ward_code",
                table: "wards",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ghn_service_type_id",
                table: "garden_stores",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "ghn_shop_id",
                table: "garden_stores",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ghn_district_id",
                table: "districts",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ghn_ward_code",
                table: "wards");

            migrationBuilder.DropColumn(
                name: "ghn_service_type_id",
                table: "garden_stores");

            migrationBuilder.DropColumn(
                name: "ghn_shop_id",
                table: "garden_stores");

            migrationBuilder.DropColumn(
                name: "ghn_district_id",
                table: "districts");
        }
    }
}
