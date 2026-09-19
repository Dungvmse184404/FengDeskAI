using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductPlacementAndPersonalRecommendation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "workspace_profile_id",
                table: "recommendations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "recommendations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Workspace");

            migrationBuilder.AddColumn<string>(
                name: "placement",
                table: "products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Desk");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kind",
                table: "recommendations");

            migrationBuilder.DropColumn(
                name: "placement",
                table: "products");

            migrationBuilder.AlterColumn<Guid>(
                name: "workspace_profile_id",
                table: "recommendations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
