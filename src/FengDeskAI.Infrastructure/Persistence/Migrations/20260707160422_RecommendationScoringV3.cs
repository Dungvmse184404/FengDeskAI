using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecommendationScoringV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "scope",
                table: "workspace_types",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Private");

            migrationBuilder.AddColumn<string>(
                name: "dark_directions",
                table: "workspace_profiles",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "entrance_direction",
                table: "workspace_profiles",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "toilet_direction",
                table: "workspace_profiles",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "element_hoa",
                table: "products",
                type: "numeric(4,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "element_kim",
                table: "products",
                type: "numeric(4,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "element_moc",
                table: "products",
                type: "numeric(4,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "element_tho",
                table: "products",
                type: "numeric(4,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "element_thuy",
                table: "products",
                type: "numeric(4,3)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_vector_overridden",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "element_input_map",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    input_kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    input_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(4,3)", nullable: false, defaultValue: 1.0m),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_element_input_map", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_element_inputs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    input_kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    input_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_element_inputs", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_element_inputs_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scoring_params",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    value = table.Column<decimal>(type: "numeric(5,3)", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scoring_params", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "work_purpose_element_modifiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    delta = table.Column<decimal>(type: "numeric(4,3)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_purpose_element_modifiers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workspace_profile_inputs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    input_kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    input_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_profile_inputs", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_profile_inputs_workspace_profiles_workspace_profi~",
                        column: x => x.workspace_profile_id,
                        principalTable: "workspace_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_type_elements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(4,3)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_type_elements", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_type_elements_workspace_types_workspace_type_id",
                        column: x => x.workspace_type_id,
                        principalTable: "workspace_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_element_input_map_input_kind_input_code_element",
                table: "element_input_map",
                columns: new[] { "input_kind", "input_code", "element" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_product_element_inputs_product_id",
                table: "product_element_inputs",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_scoring_params_code",
                table: "scoring_params",
                column: "code",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_work_purpose_element_modifiers_work_purpose_element",
                table: "work_purpose_element_modifiers",
                columns: new[] { "work_purpose", "element" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_profile_inputs_workspace_profile_id",
                table: "workspace_profile_inputs",
                column: "workspace_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_type_elements_workspace_type_id_source_element",
                table: "workspace_type_elements",
                columns: new[] { "workspace_type_id", "source", "element" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "element_input_map");

            migrationBuilder.DropTable(
                name: "product_element_inputs");

            migrationBuilder.DropTable(
                name: "scoring_params");

            migrationBuilder.DropTable(
                name: "work_purpose_element_modifiers");

            migrationBuilder.DropTable(
                name: "workspace_profile_inputs");

            migrationBuilder.DropTable(
                name: "workspace_type_elements");

            migrationBuilder.DropColumn(
                name: "scope",
                table: "workspace_types");

            migrationBuilder.DropColumn(
                name: "dark_directions",
                table: "workspace_profiles");

            migrationBuilder.DropColumn(
                name: "entrance_direction",
                table: "workspace_profiles");

            migrationBuilder.DropColumn(
                name: "toilet_direction",
                table: "workspace_profiles");

            migrationBuilder.DropColumn(
                name: "element_hoa",
                table: "products");

            migrationBuilder.DropColumn(
                name: "element_kim",
                table: "products");

            migrationBuilder.DropColumn(
                name: "element_moc",
                table: "products");

            migrationBuilder.DropColumn(
                name: "element_tho",
                table: "products");

            migrationBuilder.DropColumn(
                name: "element_thuy",
                table: "products");

            migrationBuilder.DropColumn(
                name: "is_vector_overridden",
                table: "products");
        }
    }
}
