using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OccupationP5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "occupation_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "occupations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name_vi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_occupations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "occupation_element_modifiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occupation_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_occupation_element_modifiers", x => x.id);
                    table.ForeignKey(
                        name: "FK_occupation_element_modifiers_occupations_occupation_id",
                        column: x => x.occupation_id,
                        principalTable: "occupations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_occupation_id",
                table: "users",
                column: "occupation_id");

            migrationBuilder.CreateIndex(
                name: "IX_occupation_element_modifiers_occupation_id_element",
                table: "occupation_element_modifiers",
                columns: new[] { "occupation_id", "element" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_occupations_code",
                table: "occupations",
                column: "code",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.AddForeignKey(
                name: "FK_users_occupations_occupation_id",
                table: "users",
                column: "occupation_id",
                principalTable: "occupations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_occupations_occupation_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "occupation_element_modifiers");

            migrationBuilder.DropTable(
                name: "occupations");

            migrationBuilder.DropIndex(
                name: "IX_users_occupation_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "occupation_id",
                table: "users");
        }
    }
}
