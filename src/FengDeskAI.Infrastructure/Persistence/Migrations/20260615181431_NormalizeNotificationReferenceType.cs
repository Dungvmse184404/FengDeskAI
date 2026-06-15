using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeNotificationReferenceType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "reference_type",
                table: "notifications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "reference_type",
                table: "notifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);
        }
    }
}
