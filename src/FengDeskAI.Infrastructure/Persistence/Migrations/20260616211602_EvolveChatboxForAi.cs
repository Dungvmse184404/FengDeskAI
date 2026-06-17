using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EvolveChatboxForAi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chat_messages_users_sender_user_id",
                table: "chat_messages");

            migrationBuilder.AlterColumn<Guid>(
                name: "recipient_user_id",
                table: "chatboxes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "product_id",
                table: "chatboxes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "type",
                table: "chatboxes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "sender_user_id",
                table: "chat_messages",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "content",
                table: "chat_messages",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(5000)",
                oldMaxLength: 5000);

            migrationBuilder.AddColumn<bool>(
                name: "is_from_ai",
                table: "chat_messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "sender_name",
                table: "chat_messages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sender_role",
                table: "chat_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "chat_message_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_message_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_chat_message_images_chat_messages_chat_message_id",
                        column: x => x.chat_message_id,
                        principalTable: "chat_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chatboxes_product_id",
                table: "chatboxes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_message_images_chat_message_id",
                table: "chat_message_images",
                column: "chat_message_id");

            migrationBuilder.AddForeignKey(
                name: "FK_chat_messages_users_sender_user_id",
                table: "chat_messages",
                column: "sender_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_chatboxes_products_product_id",
                table: "chatboxes",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chat_messages_users_sender_user_id",
                table: "chat_messages");

            migrationBuilder.DropForeignKey(
                name: "FK_chatboxes_products_product_id",
                table: "chatboxes");

            migrationBuilder.DropTable(
                name: "chat_message_images");

            migrationBuilder.DropIndex(
                name: "IX_chatboxes_product_id",
                table: "chatboxes");

            migrationBuilder.DropColumn(
                name: "product_id",
                table: "chatboxes");

            migrationBuilder.DropColumn(
                name: "type",
                table: "chatboxes");

            migrationBuilder.DropColumn(
                name: "is_from_ai",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "sender_name",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "sender_role",
                table: "chat_messages");

            migrationBuilder.AlterColumn<Guid>(
                name: "recipient_user_id",
                table: "chatboxes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "sender_user_id",
                table: "chat_messages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "content",
                table: "chat_messages",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(5000)",
                oldMaxLength: 5000,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_chat_messages_users_sender_user_id",
                table: "chat_messages",
                column: "sender_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
