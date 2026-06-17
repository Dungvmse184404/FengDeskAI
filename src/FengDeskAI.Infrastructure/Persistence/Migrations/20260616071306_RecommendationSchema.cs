using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecommendationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "workspace_type_id",
                table: "workspace_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "feng_shui_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    object_element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    relation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    score = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feng_shui_rules", x => x.id);
                });

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

            migrationBuilder.CreateTable(
                name: "product_styles",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    style = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_styles", x => new { x.product_id, x.style });
                    table.ForeignKey(
                        name: "FK_product_styles_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_vibes",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vibe = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_vibes", x => new { x.product_id, x.vibe });
                    table.ForeignKey(
                        name: "FK_product_vibes_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_public = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    personal_weight = table.Column<decimal>(type: "numeric(4,2)", nullable: false, defaultValue: 1.0m),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recommendations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    kua_number = table.Column<int>(type: "integer", nullable: true),
                    kua_group = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    personal_weight = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendations", x => x.id);
                    table.ForeignKey(
                        name: "FK_recommendations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recommendations_workspace_profiles_workspace_profile_id",
                        column: x => x.workspace_profile_id,
                        principalTable: "workspace_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recommendations_workspace_types_workspace_type_id",
                        column: x => x.workspace_type_id,
                        principalTable: "workspace_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_score = table.Column<decimal>(type: "numeric(6,3)", nullable: false),
                    base_rank = table.Column<int>(type: "integer", nullable: false),
                    final_rank = table.Column<int>(type: "integer", nullable: false),
                    match_facts = table.Column<string>(type: "jsonb", nullable: false),
                    caution_facts = table.Column<string>(type: "jsonb", nullable: true),
                    ai_explanation = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_recommendation_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recommendation_items_recommendations_recommendation_id",
                        column: x => x.recommendation_id,
                        principalTable: "recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    detail = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_recommendation_logs_recommendations_recommendation_id",
                        column: x => x.recommendation_id,
                        principalTable: "recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_profiles_workspace_type_id",
                table: "workspace_profiles",
                column: "workspace_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_feng_shui_rules_subject_element_object_element",
                table: "feng_shui_rules",
                columns: new[] { "subject_element", "object_element" },
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_items_product_id",
                table: "recommendation_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_items_recommendation_id",
                table: "recommendation_items",
                column: "recommendation_id");

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_logs_recommendation_id",
                table: "recommendation_logs",
                column: "recommendation_id");

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_user_id",
                table: "recommendations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_workspace_profile_id",
                table: "recommendations",
                column: "workspace_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_workspace_type_id",
                table: "recommendations",
                column: "workspace_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_types_name",
                table: "workspace_types",
                column: "name");

            migrationBuilder.AddForeignKey(
                name: "FK_workspace_profiles_workspace_types_workspace_type_id",
                table: "workspace_profiles",
                column: "workspace_type_id",
                principalTable: "workspace_types",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_workspace_profiles_workspace_types_workspace_type_id",
                table: "workspace_profiles");

            migrationBuilder.DropTable(
                name: "feng_shui_rules");

            migrationBuilder.DropTable(
                name: "product_feng_shui");

            migrationBuilder.DropTable(
                name: "product_styles");

            migrationBuilder.DropTable(
                name: "product_vibes");

            migrationBuilder.DropTable(
                name: "recommendation_items");

            migrationBuilder.DropTable(
                name: "recommendation_logs");

            migrationBuilder.DropTable(
                name: "recommendations");

            migrationBuilder.DropTable(
                name: "workspace_types");

            migrationBuilder.DropIndex(
                name: "IX_workspace_profiles_workspace_type_id",
                table: "workspace_profiles");

            migrationBuilder.DropColumn(
                name: "workspace_type_id",
                table: "workspace_profiles");
        }
    }
}
