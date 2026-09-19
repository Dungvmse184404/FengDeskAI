using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FengDeskAI.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// <b>Baseline gộp 43 migration đầu</b> (2026-06-16 → 2026-08-02, id cuối
    /// <c>20260802073311_NormalizeModel3DQueueFlow</c>) thành một bước tạo schema — sinh từ model tại
    /// commit <c>5252f8f</c> (probe <c>migrations add</c> ở đó rỗng ⇒ model ≡ snapshot #43), không viết tay.
    ///
    /// <para>
    /// <b>Vì sao attribute mang id cũ chứ không phải id mới:</b> EF chỉ so <c>[Migration]</c> với
    /// <c>__EFMigrationsHistory</c>. DB nào đã có dòng <c>20260802073311_NormalizeModel3DQueueFlow</c>
    /// (dev, test, prod Supabase — tất cả đều đã qua #43) thì coi baseline là <i>đã áp</i> và bỏ qua; 42 dòng
    /// history cũ còn lại EF không biết tới và không đụng. DB trống thì chạy nguyên khối này rồi tiếp 14
    /// migration sau. Nhờ vậy squash không cần chạm remote — remote vẫn áp 14 migration còn thiếu lúc deploy
    /// như cũ. Muốn gộp tiếp thì lặp lại quy trình này <b>sau khi</b> remote đã ở tip (xem
    /// <c>docs/adr/migration-squash-2026-09.md</c>).
    /// </para>
    ///
    /// <para>
    /// Ba thứ nằm ngoài model mà 43 migration cũ tạo bằng SQL tay, giữ lại ở cuối <c>Up</c>: extension
    /// <c>unaccent</c> (tìm kiếm không dấu), 5 dòng lookup <c>elements</c> (đích của FK), và 7 FK
    /// <c>ON UPDATE CASCADE</c> để đổi code hành/style/vibe lan truyền. Dữ liệu backfill của các migration cũ
    /// (owner cửa hàng, trạng thái staff, participant chat…) không cần cho DB trống nên không mang theo.
    /// </para>
    /// </summary>
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "authorization_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_value_json = table.Column<string>(type: "jsonb", nullable: true),
                    new_value_json = table.Column<string>(type: "jsonb", nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authorization_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_categories_categories_parent_id",
                        column: x => x.parent_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "element_input_map",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    input_kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    input_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(4,3)", nullable: false, defaultValue: 1.0m),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_element_input_map", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "elements",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_elements", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "feng_shui_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    object_element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    relation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    score = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feng_shui_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "garden_stores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    hotline = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    opening_hours = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ahamove_service_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ghn_shop_id = table.Column<int>(type: "integer", nullable: true),
                    ghn_service_type_id = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garden_stores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "provinces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    code = table.Column<int>(type: "integer", nullable: false),
                    ghn_province_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provinces", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scoring_params",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    value = table.Column<decimal>(type: "numeric(5,3)", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scoring_params", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shipping_webhook",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    is_processed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipping_webhook", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "styles",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_styles", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    date_of_birth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    birth_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    gender = table.Column<int>(type: "integer", nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    role = table.Column<int>(type: "integer", nullable: false),
                    balance = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    token_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    auth_provider = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    google_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vibes",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vibes", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "work_purpose_element_modifiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("PK_work_purpose_element_modifiers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workspace_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_public = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    personal_weight = table.Column<decimal>(type: "numeric(4,2)", nullable: false, defaultValue: 1.0m),
                    scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Private"),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    garden_store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    size_class = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    element_tho = table.Column<decimal>(type: "numeric(4,3)", nullable: true),
                    element_kim = table.Column<decimal>(type: "numeric(4,3)", nullable: true),
                    element_thuy = table.Column<decimal>(type: "numeric(4,3)", nullable: true),
                    element_moc = table.Column<decimal>(type: "numeric(4,3)", nullable: true),
                    element_hoa = table.Column<decimal>(type: "numeric(4,3)", nullable: true),
                    is_vector_overridden = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_products_garden_stores_garden_store_id",
                        column: x => x.garden_store_id,
                        principalTable: "garden_stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "districts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    province_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    code = table.Column<int>(type: "integer", nullable: false),
                    ghn_district_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_districts", x => x.id);
                    table.ForeignKey(
                        name: "FK_districts_provinces_province_id",
                        column: x => x.province_id,
                        principalTable: "provinces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "carts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_carts", x => x.id);
                    table.ForeignKey(
                        name: "FK_carts_users_customer_id",
                        column: x => x.customer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "garden_staff_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    garden_store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invited_by = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    invited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    unassigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_garden_staff_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_garden_staff_assignments_garden_stores_garden_store_id",
                        column: x => x.garden_store_id,
                        principalTable: "garden_stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_garden_staff_assignments_users_staff_id",
                        column: x => x.staff_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    location_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    workspace_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    style_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    lighting = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    desk_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    desk_orientation = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    room_facing_direction = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    work_purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    feng_shui_element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    desk_area = table.Column<int>(type: "integer", nullable: true),
                    entrance_direction = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    toilet_direction = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    dark_directions = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_profiles_styles_style_code",
                        column: x => x.style_code,
                        principalTable: "styles",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workspace_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workspace_profiles_workspace_types_workspace_type_id",
                        column: x => x.workspace_type_id,
                        principalTable: "workspace_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "workspace_type_elements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(4,3)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_type_elements", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_type_elements_workspace_types_workspace_type_id",
                        column: x => x.workspace_type_id,
                        principalTable: "workspace_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chatboxes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_group = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_ai_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_support = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    garden_store_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chatboxes", x => x.id);
                    table.ForeignKey(
                        name: "FK_chatboxes_garden_stores_garden_store_id",
                        column: x => x.garden_store_id,
                        principalTable: "garden_stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_chatboxes_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_chatboxes_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_categories",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_categories", x => new { x.product_id, x.category_id });
                    table.ForeignKey(
                        name: "FK_product_categories_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_categories_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_element",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_element", x => new { x.product_id, x.element });
                    table.ForeignKey(
                        name: "FK_product_element_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_element_inputs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    input_kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    input_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_element_inputs", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_element_inputs_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_product_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_images_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    stock = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    sku = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    weight_gram = table.Column<int>(type: "integer", nullable: false, defaultValue: 500),
                    length_cm = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    width_cm = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    height_cm = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_styles",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    style_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_styles", x => new { x.product_id, x.style_code });
                    table.ForeignKey(
                        name: "FK_product_styles_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_styles_styles_style_code",
                        column: x => x.style_code,
                        principalTable: "styles",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_tags",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_tags", x => new { x.product_id, x.tag_id });
                    table.ForeignKey(
                        name: "FK_product_tags_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_vibes",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vibe_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_vibes", x => new { x.product_id, x.vibe_code });
                    table.ForeignKey(
                        name: "FK_product_vibes_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_vibes_vibes_vibe_code",
                        column: x => x.vibe_code,
                        principalTable: "vibes",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviews", x => x.id);
                    table.ForeignKey(
                        name: "FK_reviews_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reviews_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    district_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    code = table.Column<int>(type: "integer", nullable: false),
                    ghn_ward_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wards", x => x.id);
                    table.ForeignKey(
                        name: "FK_wards_districts_district_id",
                        column: x => x.district_id,
                        principalTable: "districts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recommendations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_element = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    kua_number = table.Column<int>(type: "integer", nullable: true),
                    kua_group = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    personal_weight = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendations", x => x.id);
                    table.ForeignKey(
                        name: "FK_recommendations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_recommendations_workspace_profiles_workspace_profile_id",
                        column: x => x.workspace_profile_id,
                        principalTable: "workspace_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recommendations_workspace_types_workspace_type_id",
                        column: x => x.workspace_type_id,
                        principalTable: "workspace_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "workspace_profile_inputs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    input_kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    input_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_profile_inputs", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_profile_inputs_workspace_profiles_workspace_profi~",
                        column: x => x.workspace_profile_id,
                        principalTable: "workspace_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chatbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sender_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sender_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_chat_messages_chatboxes_chatbox_id",
                        column: x => x.chatbox_id,
                        principalTable: "chatboxes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chat_messages_users_sender_id",
                        column: x => x.sender_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

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

            migrationBuilder.CreateTable(
                name: "model3d_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    request_type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    source_image_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    meshy_task_id = table.Column<string>(type: "text", nullable: true),
                    assigned_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    internal_failure_reason = table.Column<string>(type: "text", nullable: true),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model3d_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_model3d_requests_product_images_product_image_id",
                        column: x => x.product_image_id,
                        principalTable: "product_images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_model3d_requests_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_model3ds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    source_image_url = table.Column<string>(type: "text", nullable: false),
                    meshy_task_id = table.Column<string>(type: "text", nullable: true),
                    model_url = table.Column<string>(type: "text", nullable: true),
                    thumbnail_url = table.Column<string>(type: "text", nullable: true),
                    progress = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_model3ds", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_model3ds_product_images_product_image_id",
                        column: x => x.product_image_id,
                        principalTable: "product_images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_product_model3ds_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cart_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cart_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_cart_items_carts_cart_id",
                        column: x => x.cart_id,
                        principalTable: "carts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cart_items_product_items_product_item_id",
                        column: x => x.product_item_id,
                        principalTable: "product_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stores_address",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward_id = table.Column<Guid>(type: "uuid", nullable: false),
                    street_address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(10,8)", precision: 10, scale: 8, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(11,8)", precision: 11, scale: 8, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sender_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sender_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stores_address", x => x.id);
                    table.ForeignKey(
                        name: "FK_stores_address_garden_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "garden_stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_stores_address_wards_ward_id",
                        column: x => x.ward_id,
                        principalTable: "wards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_address",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward_id = table.Column<Guid>(type: "uuid", nullable: false),
                    street_address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    recipient_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    recipient_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(10,8)", precision: 10, scale: 8, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(11,8)", precision: 11, scale: 8, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_address", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_address_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_address_wards_ward_id",
                        column: x => x.ward_id,
                        principalTable: "wards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_score = table.Column<decimal>(type: "numeric(6,3)", nullable: false),
                    base_rank = table.Column<int>(type: "integer", nullable: false),
                    final_rank = table.Column<int>(type: "integer", nullable: false),
                    match_facts = table.Column<string>(type: "jsonb", nullable: false),
                    caution_facts = table.Column<string>(type: "jsonb", nullable: true),
                    ai_explanation = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_recommendation_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recommendation_items_recommendations_recommendation_id",
                        column: x => x.recommendation_id,
                        principalTable: "recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    detail = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_recommendation_logs_recommendations_recommendation_id",
                        column: x => x.recommendation_id,
                        principalTable: "recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipping_address_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payment_method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "PayOS"),
                    subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total_shipping_fee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_orders_user_address_shipping_address_id",
                        column: x => x.shipping_address_id,
                        principalTable: "user_address",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_users_customer_id",
                        column: x => x.customer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    garden_store_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tracking_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider_order_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    shipping_provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tracking_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    shipping_fee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    is_exchange = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    shipped_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    estimated_delivery_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_deliveries_garden_stores_garden_store_id",
                        column: x => x.garden_store_id,
                        principalTable: "garden_stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_deliveries_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_deliveries_users_assigned_staff_id",
                        column: x => x.assigned_staff_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "order_status_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_status_log", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_status_log_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transaction",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_code = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    payment_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provider_transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction", x => x.id);
                    table.ForeignKey(
                        name: "FK_transaction_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "delivery_progress_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<int>(type: "integer", nullable: false),
                    from_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    to_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    raw_payload = table.Column<string>(type: "jsonb", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    logged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_progress_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_delivery_progress_logs_deliveries_delivery_id",
                        column: x => x.delivery_id,
                        principalTable: "deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_items_deliveries_delivery_id",
                        column: x => x.delivery_id,
                        principalTable: "deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_order_items_product_items_product_item_id",
                        column: x => x.product_item_id,
                        principalTable: "product_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "return_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reason_detail = table.Column<string>(type: "text", nullable: true),
                    refund_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    refund_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bank_account_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bank_account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    return_tracking_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    vendor_response = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    vendor_response_deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    evidence_deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_reason = table.Column<string>(type: "text", nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replacement_delivery_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_return_requests_deliveries_delivery_id",
                        column: x => x.delivery_id,
                        principalTable: "deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_requests_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workspace_product_placements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    placed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_product_placements", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_product_placements_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalTable: "order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workspace_product_placements_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workspace_product_placements_workspace_profiles_workspace_p~",
                        column: x => x.workspace_profile_id,
                        principalTable: "workspace_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refunds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    gateway = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "payos"),
                    provider_refund_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_manual = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    manual_reason = table.Column<string>(type: "text", nullable: true),
                    evidence_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    performed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    processed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refunds", x => x.id);
                    table.ForeignKey(
                        name: "FK_refunds_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refunds_return_requests_return_request_id",
                        column: x => x.return_request_id,
                        principalTable: "return_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_refunds_transaction_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "transaction",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "return_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    exchange_product_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_return_items_order_items_order_item_id",
                        column: x => x.order_item_id,
                        principalTable: "order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_items_product_items_exchange_product_item_id",
                        column: x => x.exchange_product_item_id,
                        principalTable: "product_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_return_items_return_requests_return_request_id",
                        column: x => x.return_request_id,
                        principalTable: "return_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "return_request_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    image_url = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_request_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_return_request_images_return_requests_return_request_id",
                        column: x => x.return_request_id,
                        principalTable: "return_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "return_status_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_status_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_return_status_logs_return_requests_return_request_id",
                        column: x => x.return_request_id,
                        principalTable: "return_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vendor_liabilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    garden_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refund_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    dispute_reason = table.Column<string>(type: "text", nullable: true),
                    dispute_deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendor_liabilities", x => x.id);
                    table.ForeignKey(
                        name: "FK_vendor_liabilities_garden_stores_garden_id",
                        column: x => x.garden_id,
                        principalTable: "garden_stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_vendor_liabilities_refunds_refund_id",
                        column: x => x.refund_id,
                        principalTable: "refunds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vendor_liabilities_return_requests_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "return_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_authorization_audit_logs_actor_user_id",
                table: "authorization_audit_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_authorization_audit_logs_created_at",
                table: "authorization_audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_authorization_audit_logs_resource_type_resource_id",
                table: "authorization_audit_logs",
                columns: new[] { "resource_type", "resource_id" });

            migrationBuilder.CreateIndex(
                name: "IX_cart_items_product_item_id",
                table: "cart_items",
                column: "product_item_id");

            migrationBuilder.CreateIndex(
                name: "UX_cart_items_cart_product_item",
                table: "cart_items",
                columns: new[] { "cart_id", "product_item_id" },
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_carts_customer_id",
                table: "carts",
                column: "customer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_categories_parent_id",
                table: "categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_message_images_chat_message_id",
                table: "chat_message_images",
                column: "chat_message_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_chatbox_id_created_at",
                table: "chat_messages",
                columns: new[] { "chatbox_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_sender_id",
                table: "chat_messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "IX_chat_room_data_consents_chatbox_id_granter_user_id",
                table: "chat_room_data_consents",
                columns: new[] { "chatbox_id", "granter_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chatbox_participants_chatbox_id_user_id",
                table: "chatbox_participants",
                columns: new[] { "chatbox_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chatbox_participants_user_id",
                table: "chatbox_participants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_chatboxes_created_by_user_id",
                table: "chatboxes",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_chatboxes_garden_store_id",
                table: "chatboxes",
                column: "garden_store_id");

            migrationBuilder.CreateIndex(
                name: "IX_chatboxes_product_id",
                table: "chatboxes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_assigned_staff_id",
                table: "deliveries",
                column: "assigned_staff_id");

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_garden_store_id",
                table: "deliveries",
                column: "garden_store_id");

            migrationBuilder.CreateIndex(
                name: "IX_deliveries_order_id",
                table: "deliveries",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_progress_logs_delivery_id",
                table: "delivery_progress_logs",
                column: "delivery_id");

            migrationBuilder.CreateIndex(
                name: "IX_districts_code",
                table: "districts",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_districts_province_id",
                table: "districts",
                column: "province_id");

            migrationBuilder.CreateIndex(
                name: "IX_element_input_map_input_kind_input_code_element",
                table: "element_input_map",
                columns: new[] { "input_kind", "input_code", "element" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_feng_shui_rules_subject_element_object_element",
                table: "feng_shui_rules",
                columns: new[] { "subject_element", "object_element" },
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_garden_staff_assignments_staff_id",
                table: "garden_staff_assignments",
                column: "staff_id");

            migrationBuilder.CreateIndex(
                name: "UX_garden_staff_active",
                table: "garden_staff_assignments",
                columns: new[] { "garden_store_id", "staff_id" },
                unique: true,
                filter: "status IN ('Pending', 'Accepted') AND is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_garden_store_owners_garden_store_id_owner_user_id",
                table: "garden_store_owners",
                columns: new[] { "garden_store_id", "owner_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_garden_store_owners_owner_user_id",
                table: "garden_store_owners",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_model3d_requests_product_id",
                table: "model3d_requests",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_model3d_requests_product_image_id",
                table: "model3d_requests",
                column: "product_image_id");

            migrationBuilder.CreateIndex(
                name: "IX_model3d_requests_status",
                table: "model3d_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id_created_at",
                table: "notifications",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id_is_read",
                table: "notifications",
                columns: new[] { "user_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "IX_order_items_delivery_id",
                table: "order_items",
                column: "delivery_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_order_id",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_items_product_item_id",
                table: "order_items",
                column: "product_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_order_status_log_order_id",
                table: "order_status_log",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_customer_id",
                table: "orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_orders_shipping_address_id",
                table: "orders",
                column: "shipping_address_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_categories_category_id",
                table: "product_categories",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_element_product_id_is_primary",
                table: "product_element",
                columns: new[] { "product_id", "is_primary" });

            migrationBuilder.CreateIndex(
                name: "IX_product_element_inputs_product_id",
                table: "product_element_inputs",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_images_product_id",
                table: "product_images",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_items_product_id",
                table: "product_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_items_sku",
                table: "product_items",
                column: "sku",
                unique: true,
                filter: "sku IS NOT NULL AND is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_product_model3ds_product_id",
                table: "product_model3ds",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_model3ds_product_image_id",
                table: "product_model3ds",
                column: "product_image_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_styles_style_code",
                table: "product_styles",
                column: "style_code");

            migrationBuilder.CreateIndex(
                name: "IX_product_tags_tag_id",
                table: "product_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_vibes_vibe_code",
                table: "product_vibes",
                column: "vibe_code");

            migrationBuilder.CreateIndex(
                name: "IX_products_garden_store_id",
                table: "products",
                column: "garden_store_id");

            migrationBuilder.CreateIndex(
                name: "IX_provinces_code",
                table: "provinces",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_items_product_id",
                table: "recommendation_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_items_recommendation_id",
                table: "recommendation_items",
                column: "recommendation_id");

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_logs_recommendation_id",
                table: "recommendation_logs",
                column: "recommendation_id");

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_user_id",
                table: "recommendations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_workspace_profile_id",
                table: "recommendations",
                column: "workspace_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_recommendations_workspace_type_id",
                table: "recommendations",
                column: "workspace_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token",
                table: "refresh_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id_is_revoked",
                table: "refresh_tokens",
                columns: new[] { "user_id", "is_revoked" });

            migrationBuilder.CreateIndex(
                name: "IX_refunds_idempotency_key",
                table: "refunds",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refunds_order_id",
                table: "refunds",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_return_request_id",
                table: "refunds",
                column: "return_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refunds_transaction_id",
                table: "refunds",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_return_items_exchange_product_item_id",
                table: "return_items",
                column: "exchange_product_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_return_items_order_item_id",
                table: "return_items",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_return_items_return_request_id",
                table: "return_items",
                column: "return_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_return_request_images_return_request_id",
                table: "return_request_images",
                column: "return_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_return_requests_customer_id",
                table: "return_requests",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_return_requests_delivery_id",
                table: "return_requests",
                column: "delivery_id");

            migrationBuilder.CreateIndex(
                name: "IX_return_requests_order_id",
                table: "return_requests",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_return_status_logs_return_request_id",
                table: "return_status_logs",
                column: "return_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_reviews_product_id",
                table: "reviews",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "UX_reviews_user_product",
                table: "reviews",
                columns: new[] { "user_id", "product_id" },
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_scoring_params_code",
                table: "scoring_params",
                column: "code",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_shipping_webhook_is_processed",
                table: "shipping_webhook",
                column: "is_processed");

            migrationBuilder.CreateIndex(
                name: "IX_stores_address_store_id",
                table: "stores_address",
                column: "store_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stores_address_ward_id",
                table: "stores_address",
                column: "ward_id");

            migrationBuilder.CreateIndex(
                name: "IX_tags_name",
                table: "tags",
                column: "name",
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_order_code",
                table: "transaction",
                column: "order_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transaction_order_id",
                table: "transaction",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_address_ward_id",
                table: "user_address",
                column: "ward_id");

            migrationBuilder.CreateIndex(
                name: "UX_user_address_user_default",
                table: "user_address",
                column: "user_id",
                unique: true,
                filter: "is_default = TRUE AND is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_google_id",
                table: "users",
                column: "google_id",
                unique: true,
                filter: "google_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_users_phone",
                table: "users",
                column: "phone",
                unique: true,
                filter: "phone IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_liabilities_garden_id",
                table: "vendor_liabilities",
                column: "garden_id");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_liabilities_refund_id",
                table: "vendor_liabilities",
                column: "refund_id");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_liabilities_status",
                table: "vendor_liabilities",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_vendor_liabilities_ticket_id",
                table: "vendor_liabilities",
                column: "ticket_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wards_code",
                table: "wards",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wards_district_id",
                table: "wards",
                column: "district_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_purpose_element_modifiers_work_purpose_element",
                table: "work_purpose_element_modifiers",
                columns: new[] { "work_purpose", "element" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_product_placements_order_item_id",
                table: "workspace_product_placements",
                column: "order_item_id",
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_product_placements_product_id",
                table: "workspace_product_placements",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_product_placements_workspace_profile_id_user_id",
                table: "workspace_product_placements",
                columns: new[] { "workspace_profile_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_profile_inputs_workspace_profile_id",
                table: "workspace_profile_inputs",
                column: "workspace_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_profiles_style_code",
                table: "workspace_profiles",
                column: "style_code");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_profiles_workspace_type_id",
                table: "workspace_profiles",
                column: "workspace_type_id");

            migrationBuilder.CreateIndex(
                name: "UX_workspace_profiles_user_default",
                table: "workspace_profiles",
                column: "user_id",
                unique: true,
                filter: "is_default = TRUE AND is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_type_elements_workspace_type_id_source_element",
                table: "workspace_type_elements",
                columns: new[] { "workspace_type_id", "source", "element" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_types_name",
                table: "workspace_types",
                column: "name");

            // ── Ngoài model: extension + lookup + FK ON UPDATE CASCADE (xem summary) ──
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");

            migrationBuilder.Sql("""
                INSERT INTO elements (code, name, is_active, sort_order) VALUES
                  ('Kim','Kim (Metal)',true,1),
                  ('Moc','Mộc (Wood)',true,2),
                  ('Thuy','Thủy (Water)',true,3),
                  ('Hoa','Hỏa (Fire)',true,4),
                  ('Tho','Thổ (Earth)',true,5)
                ON CONFLICT (code) DO NOTHING;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE product_element
                  ADD CONSTRAINT "FK_product_element_elements_element"
                  FOREIGN KEY (element) REFERENCES elements (code) ON UPDATE CASCADE ON DELETE RESTRICT;
                ALTER TABLE workspace_profiles
                  ADD CONSTRAINT "FK_workspace_profiles_elements_feng_shui_element"
                  FOREIGN KEY (feng_shui_element) REFERENCES elements (code) ON UPDATE CASCADE ON DELETE RESTRICT;
                ALTER TABLE feng_shui_rules
                  ADD CONSTRAINT "FK_feng_shui_rules_elements_subject"
                  FOREIGN KEY (subject_element) REFERENCES elements (code) ON UPDATE CASCADE ON DELETE RESTRICT;
                ALTER TABLE feng_shui_rules
                  ADD CONSTRAINT "FK_feng_shui_rules_elements_object"
                  FOREIGN KEY (object_element) REFERENCES elements (code) ON UPDATE CASCADE ON DELETE RESTRICT;

                ALTER TABLE product_styles DROP CONSTRAINT "FK_product_styles_styles_style_code";
                ALTER TABLE product_styles ADD CONSTRAINT "FK_product_styles_styles_style_code"
                  FOREIGN KEY (style_code) REFERENCES styles (code) ON UPDATE CASCADE ON DELETE RESTRICT;
                ALTER TABLE product_vibes DROP CONSTRAINT "FK_product_vibes_vibes_vibe_code";
                ALTER TABLE product_vibes ADD CONSTRAINT "FK_product_vibes_vibes_vibe_code"
                  FOREIGN KEY (vibe_code) REFERENCES vibes (code) ON UPDATE CASCADE ON DELETE RESTRICT;
                ALTER TABLE workspace_profiles DROP CONSTRAINT "FK_workspace_profiles_styles_style_code";
                ALTER TABLE workspace_profiles ADD CONSTRAINT "FK_workspace_profiles_styles_style_code"
                  FOREIGN KEY (style_code) REFERENCES styles (code) ON UPDATE CASCADE ON DELETE RESTRICT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authorization_audit_logs");

            migrationBuilder.DropTable(
                name: "cart_items");

            migrationBuilder.DropTable(
                name: "chat_message_images");

            migrationBuilder.DropTable(
                name: "chat_room_data_consents");

            migrationBuilder.DropTable(
                name: "chatbox_participants");

            migrationBuilder.DropTable(
                name: "delivery_progress_logs");

            migrationBuilder.DropTable(
                name: "element_input_map");

            migrationBuilder.DropTable(
                name: "elements");

            migrationBuilder.DropTable(
                name: "feng_shui_rules");

            migrationBuilder.DropTable(
                name: "garden_staff_assignments");

            migrationBuilder.DropTable(
                name: "garden_store_owners");

            migrationBuilder.DropTable(
                name: "model3d_requests");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "order_status_log");

            migrationBuilder.DropTable(
                name: "product_categories");

            migrationBuilder.DropTable(
                name: "product_element");

            migrationBuilder.DropTable(
                name: "product_element_inputs");

            migrationBuilder.DropTable(
                name: "product_model3ds");

            migrationBuilder.DropTable(
                name: "product_styles");

            migrationBuilder.DropTable(
                name: "product_tags");

            migrationBuilder.DropTable(
                name: "product_vibes");

            migrationBuilder.DropTable(
                name: "recommendation_items");

            migrationBuilder.DropTable(
                name: "recommendation_logs");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "return_items");

            migrationBuilder.DropTable(
                name: "return_request_images");

            migrationBuilder.DropTable(
                name: "return_status_logs");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "scoring_params");

            migrationBuilder.DropTable(
                name: "shipping_webhook");

            migrationBuilder.DropTable(
                name: "stores_address");

            migrationBuilder.DropTable(
                name: "vendor_liabilities");

            migrationBuilder.DropTable(
                name: "work_purpose_element_modifiers");

            migrationBuilder.DropTable(
                name: "workspace_product_placements");

            migrationBuilder.DropTable(
                name: "workspace_profile_inputs");

            migrationBuilder.DropTable(
                name: "workspace_type_elements");

            migrationBuilder.DropTable(
                name: "carts");

            migrationBuilder.DropTable(
                name: "chat_messages");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "product_images");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "vibes");

            migrationBuilder.DropTable(
                name: "recommendations");

            migrationBuilder.DropTable(
                name: "refunds");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "chatboxes");

            migrationBuilder.DropTable(
                name: "workspace_profiles");

            migrationBuilder.DropTable(
                name: "return_requests");

            migrationBuilder.DropTable(
                name: "transaction");

            migrationBuilder.DropTable(
                name: "product_items");

            migrationBuilder.DropTable(
                name: "styles");

            migrationBuilder.DropTable(
                name: "workspace_types");

            migrationBuilder.DropTable(
                name: "deliveries");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "garden_stores");

            migrationBuilder.DropTable(
                name: "user_address");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "wards");

            migrationBuilder.DropTable(
                name: "districts");

            migrationBuilder.DropTable(
                name: "provinces");

            migrationBuilder.Sql("DROP EXTENSION IF EXISTS unaccent;");
        }
    }
}
