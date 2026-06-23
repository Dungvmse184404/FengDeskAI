using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GardenStoreOwners : Migration
    {
        // Flag UserRole.GardenOwner = 1 << 4
        private const int GardenOwnerFlag = 16;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Tạo bảng nối trước khi bỏ owner_id (để backfill được).
            migrationBuilder.CreateTable(
                name: "garden_store_owners",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    garden_store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garden_store_owners", x => x.id);
                    table.ForeignKey(
                        name: "FK_garden_store_owners_garden_stores_garden_store_id",
                        column: x => x.garden_store_id,
                        principalTable: "garden_stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_garden_store_owners_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_garden_store_owners_garden_store_id_owner_user_id",
                table: "garden_store_owners",
                columns: new[] { "garden_store_id", "owner_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_garden_store_owners_owner_user_id",
                table: "garden_store_owners",
                column: "owner_user_id");

            // 2) Backfill: owner cũ (owner_id) trở thành owner chính (is_primary = true).
            migrationBuilder.Sql(@"
                INSERT INTO garden_store_owners (id, garden_store_id, owner_user_id, is_primary, assigned_at, created_at, updated_at, is_deleted)
                SELECT gen_random_uuid(), s.id, s.owner_id, true, now(), now(), now(), false
                FROM garden_stores s;");

            // 3) Cấp flag GardenOwner cho các owner hiện hữu.
            migrationBuilder.Sql($@"
                UPDATE users SET role = role | {GardenOwnerFlag}
                WHERE id IN (SELECT owner_id FROM garden_stores)
                  AND (role & {GardenOwnerFlag}) = 0;");

            // 4) Bỏ owner_id khỏi garden_stores.
            migrationBuilder.DropForeignKey(
                name: "FK_garden_stores_users_owner_id",
                table: "garden_stores");

            migrationBuilder.DropIndex(
                name: "IX_garden_stores_owner_id",
                table: "garden_stores");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "garden_stores");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Khôi phục owner_id từ owner chính rồi mới bỏ bảng nối.
            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "garden_stores",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE garden_stores s
                SET owner_id = o.owner_user_id
                FROM garden_store_owners o
                WHERE o.garden_store_id = s.id AND o.is_primary = true AND o.is_deleted = false;");

            // Phòng trường hợp còn null (store không có owner primary) — gán Guid rỗng để cột non-null.
            migrationBuilder.Sql(
                "UPDATE garden_stores SET owner_id = '00000000-0000-0000-0000-000000000000' WHERE owner_id IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "owner_id",
                table: "garden_stores",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropTable(
                name: "garden_store_owners");

            migrationBuilder.CreateIndex(
                name: "IX_garden_stores_owner_id",
                table: "garden_stores",
                column: "owner_id");

            migrationBuilder.AddForeignKey(
                name: "FK_garden_stores_users_owner_id",
                table: "garden_stores",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
