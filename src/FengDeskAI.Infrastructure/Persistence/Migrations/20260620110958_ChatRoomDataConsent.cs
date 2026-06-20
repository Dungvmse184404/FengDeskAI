using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChatRoomDataConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chat_room_data_consents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chatbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    share_profile = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    share_workspaces = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    share_orders = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_room_data_consents", x => x.id);
                    table.ForeignKey(
                        name: "FK_chat_room_data_consents_chatboxes_chatbox_id",
                        column: x => x.chatbox_id,
                        principalTable: "chatboxes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chat_room_data_consents_chatbox_id_granter_user_id",
                table: "chat_room_data_consents",
                columns: new[] { "chatbox_id", "granter_user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_room_data_consents");
        }
    }
}
