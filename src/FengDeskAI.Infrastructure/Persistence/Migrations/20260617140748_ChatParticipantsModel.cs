using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChatParticipantsModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 0. Gỡ FK/index ràng buộc trên các cột sắp đổi ──
            migrationBuilder.DropForeignKey(name: "FK_chat_messages_users_sender_user_id", table: "chat_messages");
            migrationBuilder.DropForeignKey(name: "FK_chatboxes_users_recipient_user_id", table: "chatboxes");
            migrationBuilder.DropForeignKey(name: "FK_chatboxes_users_sender_user_id", table: "chatboxes");
            migrationBuilder.DropIndex(name: "IX_chatboxes_recipient_user_id", table: "chatboxes");
            migrationBuilder.DropIndex(name: "IX_chatboxes_sender_user_id_recipient_user_id", table: "chatboxes");
            migrationBuilder.DropIndex(name: "IX_chat_messages_chatbox_id_is_read", table: "chat_messages");

            // ── 1. chat_messages: thêm sender_type, BACKFILL từ is_from_ai, rồi mới bỏ cột cũ ──
            migrationBuilder.AddColumn<string>(
                name: "sender_type", table: "chat_messages",
                type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "User");
            migrationBuilder.Sql(
                "UPDATE chat_messages SET sender_type = CASE WHEN is_from_ai THEN 'AiBot' ELSE 'User' END;");

            migrationBuilder.DropColumn(name: "is_from_ai", table: "chat_messages");
            migrationBuilder.DropColumn(name: "is_read", table: "chat_messages");
            migrationBuilder.DropColumn(name: "read_at", table: "chat_messages");
            migrationBuilder.DropColumn(name: "sender_role", table: "chat_messages");

            migrationBuilder.RenameColumn(name: "sender_user_id", table: "chat_messages", newName: "sender_id");
            migrationBuilder.RenameIndex(name: "IX_chat_messages_sender_user_id", table: "chat_messages", newName: "IX_chat_messages_sender_id");

            // ── 2. chatboxes: thêm cột mới ──
            migrationBuilder.AddColumn<bool>(
                name: "is_group", table: "chatboxes", type: "boolean", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<string>(
                name: "title", table: "chatboxes", type: "character varying(200)", maxLength: 200, nullable: true);

            migrationBuilder.RenameColumn(name: "sender_user_id", table: "chatboxes", newName: "created_by_user_id");
            migrationBuilder.RenameIndex(name: "IX_chatboxes_sender_user_id", table: "chatboxes", newName: "IX_chatboxes_created_by_user_id");

            migrationBuilder.CreateTable(
                name: "chatbox_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chatbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    participant_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_muted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    last_read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chatbox_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_chatbox_participants_chatboxes_chatbox_id",
                        column: x => x.chatbox_id,
                        principalTable: "chatboxes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chatbox_participants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chatbox_participants_chatbox_id_user_id",
                table: "chatbox_participants",
                columns: new[] { "chatbox_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chatbox_participants_user_id",
                table: "chatbox_participants",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_chat_messages_users_sender_id",
                table: "chat_messages",
                column: "sender_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_chatboxes_users_created_by_user_id",
                table: "chatboxes",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // ── 3. BACKFILL participants từ dữ liệu cũ (type/recipient vẫn còn) ──
            // Người tạo (sender cũ) = Owner cho mọi phòng.
            migrationBuilder.Sql(@"
INSERT INTO chatbox_participants (id, chatbox_id, user_id, participant_type, role, is_muted, is_hidden, joined_at)
SELECT gen_random_uuid(), id, created_by_user_id, 'Customer', 'Owner', false, false, now()
FROM chatboxes;

-- Phòng Direct (type=0): người nhận = Member.
INSERT INTO chatbox_participants (id, chatbox_id, user_id, participant_type, role, is_muted, is_hidden, joined_at)
SELECT gen_random_uuid(), id, recipient_user_id, 'Customer', 'Member', false, false, now()
FROM chatboxes WHERE type = 0 AND recipient_user_id IS NOT NULL;

-- Phòng Assistant (type=1): thêm AiBot (user_id NULL).
INSERT INTO chatbox_participants (id, chatbox_id, user_id, participant_type, role, is_muted, is_hidden, joined_at)
SELECT gen_random_uuid(), id, NULL, 'AiBot', 'Member', false, false, now()
FROM chatboxes WHERE type = 1;");

            // ── 4. Bỏ cột cũ sau khi đã backfill ──
            migrationBuilder.DropColumn(name: "recipient_user_id", table: "chatboxes");
            migrationBuilder.DropColumn(name: "type", table: "chatboxes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chat_messages_users_sender_id",
                table: "chat_messages");

            migrationBuilder.DropForeignKey(
                name: "FK_chatboxes_users_created_by_user_id",
                table: "chatboxes");

            migrationBuilder.DropTable(
                name: "chatbox_participants");

            migrationBuilder.DropColumn(
                name: "is_group",
                table: "chatboxes");

            migrationBuilder.DropColumn(
                name: "title",
                table: "chatboxes");

            migrationBuilder.DropColumn(
                name: "sender_type",
                table: "chat_messages");

            migrationBuilder.RenameColumn(
                name: "created_by_user_id",
                table: "chatboxes",
                newName: "sender_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_chatboxes_created_by_user_id",
                table: "chatboxes",
                newName: "IX_chatboxes_sender_user_id");

            migrationBuilder.RenameColumn(
                name: "sender_id",
                table: "chat_messages",
                newName: "sender_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_chat_messages_sender_id",
                table: "chat_messages",
                newName: "IX_chat_messages_sender_user_id");

            migrationBuilder.AddColumn<Guid>(
                name: "recipient_user_id",
                table: "chatboxes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "type",
                table: "chatboxes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_from_ai",
                table: "chat_messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_read",
                table: "chat_messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "read_at",
                table: "chat_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sender_role",
                table: "chat_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_chatboxes_recipient_user_id",
                table: "chatboxes",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_chatboxes_sender_user_id_recipient_user_id",
                table: "chatboxes",
                columns: new[] { "sender_user_id", "recipient_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_chatbox_id_is_read",
                table: "chat_messages",
                columns: new[] { "chatbox_id", "is_read" });

            migrationBuilder.AddForeignKey(
                name: "FK_chat_messages_users_sender_user_id",
                table: "chat_messages",
                column: "sender_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_chatboxes_users_recipient_user_id",
                table: "chatboxes",
                column: "recipient_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_chatboxes_users_sender_user_id",
                table: "chatboxes",
                column: "sender_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
