using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuthorizationAuditSnakeCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Reason",
                table: "authorization_audit_logs",
                newName: "reason");

            migrationBuilder.RenameColumn(
                name: "Action",
                table: "authorization_audit_logs",
                newName: "action");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "authorization_audit_logs",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedBy",
                table: "authorization_audit_logs",
                newName: "updated_by");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "authorization_audit_logs",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "ResourceType",
                table: "authorization_audit_logs",
                newName: "resource_type");

            migrationBuilder.RenameColumn(
                name: "ResourceId",
                table: "authorization_audit_logs",
                newName: "resource_id");

            migrationBuilder.RenameColumn(
                name: "OldValueJson",
                table: "authorization_audit_logs",
                newName: "old_value_json");

            migrationBuilder.RenameColumn(
                name: "NewValueJson",
                table: "authorization_audit_logs",
                newName: "new_value_json");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "authorization_audit_logs",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "IpAddress",
                table: "authorization_audit_logs",
                newName: "ip_address");

            migrationBuilder.RenameColumn(
                name: "CreatedBy",
                table: "authorization_audit_logs",
                newName: "created_by");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "authorization_audit_logs",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "ActorUserId",
                table: "authorization_audit_logs",
                newName: "actor_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_authorization_audit_logs_ResourceType_ResourceId",
                table: "authorization_audit_logs",
                newName: "IX_authorization_audit_logs_resource_type_resource_id");

            migrationBuilder.RenameIndex(
                name: "IX_authorization_audit_logs_CreatedAt",
                table: "authorization_audit_logs",
                newName: "IX_authorization_audit_logs_created_at");

            migrationBuilder.RenameIndex(
                name: "IX_authorization_audit_logs_ActorUserId",
                table: "authorization_audit_logs",
                newName: "IX_authorization_audit_logs_actor_user_id");

            migrationBuilder.AlterColumn<bool>(
                name: "is_deleted",
                table: "authorization_audit_logs",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "reason",
                table: "authorization_audit_logs",
                newName: "Reason");

            migrationBuilder.RenameColumn(
                name: "action",
                table: "authorization_audit_logs",
                newName: "Action");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "authorization_audit_logs",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_by",
                table: "authorization_audit_logs",
                newName: "UpdatedBy");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "authorization_audit_logs",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "resource_type",
                table: "authorization_audit_logs",
                newName: "ResourceType");

            migrationBuilder.RenameColumn(
                name: "resource_id",
                table: "authorization_audit_logs",
                newName: "ResourceId");

            migrationBuilder.RenameColumn(
                name: "old_value_json",
                table: "authorization_audit_logs",
                newName: "OldValueJson");

            migrationBuilder.RenameColumn(
                name: "new_value_json",
                table: "authorization_audit_logs",
                newName: "NewValueJson");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                table: "authorization_audit_logs",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "ip_address",
                table: "authorization_audit_logs",
                newName: "IpAddress");

            migrationBuilder.RenameColumn(
                name: "created_by",
                table: "authorization_audit_logs",
                newName: "CreatedBy");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "authorization_audit_logs",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "actor_user_id",
                table: "authorization_audit_logs",
                newName: "ActorUserId");

            migrationBuilder.RenameIndex(
                name: "IX_authorization_audit_logs_resource_type_resource_id",
                table: "authorization_audit_logs",
                newName: "IX_authorization_audit_logs_ResourceType_ResourceId");

            migrationBuilder.RenameIndex(
                name: "IX_authorization_audit_logs_created_at",
                table: "authorization_audit_logs",
                newName: "IX_authorization_audit_logs_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_authorization_audit_logs_actor_user_id",
                table: "authorization_audit_logs",
                newName: "IX_authorization_audit_logs_ActorUserId");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "authorization_audit_logs",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);
        }
    }
}
