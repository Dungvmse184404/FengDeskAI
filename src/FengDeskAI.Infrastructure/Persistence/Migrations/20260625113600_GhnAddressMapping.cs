using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GhnAddressMapping : Migration
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

            migrationBuilder.AddColumn<string>(
                name: "sender_name",
                table: "stores_address",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sender_phone",
                table: "stores_address",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ghn_province_id",
                table: "provinces",
                type: "integer",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_wards_ghn_ward_code",
                table: "wards",
                column: "ghn_ward_code");

            migrationBuilder.CreateIndex(
                name: "IX_districts_ghn_district_id",
                table: "districts",
                column: "ghn_district_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_wards_ghn_ward_code",
                table: "wards");

            migrationBuilder.DropIndex(
                name: "IX_districts_ghn_district_id",
                table: "districts");

            migrationBuilder.DropColumn(
                name: "ghn_ward_code",
                table: "wards");

            migrationBuilder.DropColumn(
                name: "sender_name",
                table: "stores_address");

            migrationBuilder.DropColumn(
                name: "sender_phone",
                table: "stores_address");

            migrationBuilder.DropColumn(
                name: "ghn_province_id",
                table: "provinces");

            migrationBuilder.DropColumn(
                name: "ghn_shop_id",
                table: "garden_stores");

            migrationBuilder.DropColumn(
                name: "ghn_district_id",
                table: "districts");
        }
    }
}
