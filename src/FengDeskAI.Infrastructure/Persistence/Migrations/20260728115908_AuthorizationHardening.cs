using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuthorizationHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "token_version",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "assigned_staff_id",
                table: "deliveries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "authorization_audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    OldValueJson = table.Column<string>(type: "jsonb", nullable: true),
                    NewValueJson = table.Column<string>(type: "jsonb", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authorization_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_assigned_staff_id",
                table: "deliveries",
                column: "assigned_staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_authorization_audit_logs_ActorUserId",
                table: "authorization_audit_logs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_authorization_audit_logs_CreatedAt",
                table: "authorization_audit_logs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_authorization_audit_logs_ResourceType_ResourceId",
                table: "authorization_audit_logs",
                columns: new[] { "ResourceType", "ResourceId" });

            migrationBuilder.AddForeignKey(
                name: "FK_deliveries_users_assigned_staff_id",
                table: "deliveries",
                column: "assigned_staff_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_deliveries_users_assigned_staff_id",
                table: "deliveries");

            migrationBuilder.DropTable(
                name: "authorization_audit_logs");

            migrationBuilder.DropIndex(
                name: "IX_deliveries_assigned_staff_id",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "token_version",
                table: "users");

            migrationBuilder.DropColumn(
                name: "assigned_staff_id",
                table: "deliveries");
        }
    }
}
