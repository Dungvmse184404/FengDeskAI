using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveSizeClassToProductItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "size_class",
                table: "products");

            migrationBuilder.AddColumn<string>(
                name: "size_class",
                table: "product_items",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "size_class",
                table: "product_items");

            migrationBuilder.AddColumn<string>(
                name: "size_class",
                table: "products",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }
    }
}
