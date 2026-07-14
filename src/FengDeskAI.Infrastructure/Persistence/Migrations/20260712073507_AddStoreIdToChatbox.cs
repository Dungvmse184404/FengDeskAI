using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreIdToChatbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "garden_store_id",
                table: "chatboxes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_chatboxes_garden_store_id",
                table: "chatboxes",
                column: "garden_store_id");

            migrationBuilder.AddForeignKey(
                name: "FK_chatboxes_garden_stores_garden_store_id",
                table: "chatboxes",
                column: "garden_store_id",
                principalTable: "garden_stores",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chatboxes_garden_stores_garden_store_id",
                table: "chatboxes");

            migrationBuilder.DropIndex(
                name: "IX_chatboxes_garden_store_id",
                table: "chatboxes");

            migrationBuilder.DropColumn(
                name: "garden_store_id",
                table: "chatboxes");
        }
    }
}
