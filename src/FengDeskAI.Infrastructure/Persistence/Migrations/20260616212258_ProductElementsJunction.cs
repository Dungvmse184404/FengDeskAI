using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductElementsJunction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "size_class",
                table: "products",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "product_element",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_element", x => new { x.product_id, x.element });
                    table.ForeignKey(
                        name: "FK_product_element_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_element_product_id_is_primary",
                table: "product_element",
                columns: new[] { "product_id", "is_primary" });

            // Backfill product_feng_shui (1-1) → product_element (junction) + products.size_class
            // TRƯỚC khi drop bảng cũ, tránh mất dữ liệu.
            migrationBuilder.Sql(@"
INSERT INTO product_element (product_id, element, is_primary)
SELECT product_id, primary_element, true FROM product_feng_shui;

INSERT INTO product_element (product_id, element, is_primary)
SELECT product_id, secondary_element, false FROM product_feng_shui
WHERE secondary_element IS NOT NULL AND secondary_element <> primary_element;

UPDATE products p SET size_class = f.size_class
FROM product_feng_shui f WHERE p.id = f.product_id;");

            migrationBuilder.DropTable(
                name: "product_feng_shui");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_feng_shui",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    primary_element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    secondary_element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    size_class = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_feng_shui", x => x.product_id);
                    table.ForeignKey(
                        name: "FK_product_feng_shui_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Backfill ngược: gộp product_element + products.size_class về product_feng_shui (1-1).
            migrationBuilder.Sql(@"
INSERT INTO product_feng_shui (product_id, primary_element, secondary_element, size_class)
SELECT pe.product_id,
       MAX(CASE WHEN pe.is_primary THEN pe.element END),
       MAX(CASE WHEN NOT pe.is_primary THEN pe.element END),
       COALESCE(MAX(p.size_class), 'Medium')
FROM product_element pe
JOIN products p ON p.id = pe.product_id
GROUP BY pe.product_id
HAVING MAX(CASE WHEN pe.is_primary THEN pe.element END) IS NOT NULL;");

            migrationBuilder.DropTable(
                name: "product_element");

            migrationBuilder.DropColumn(
                name: "size_class",
                table: "products");
        }
    }
}
