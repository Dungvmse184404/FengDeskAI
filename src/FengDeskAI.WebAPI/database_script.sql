CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

CREATE TABLE users (
    id uuid NOT NULL,
    email character varying(255) NOT NULL,
    password_hash text NOT NULL,
    full_name character varying(255) NOT NULL,
    date_of_birth timestamp with time zone,
    gender integer NOT NULL,
    phone character varying(20),
    role integer NOT NULL,
    balance numeric(12,3) NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_users" PRIMARY KEY (id)
);

CREATE TABLE refresh_tokens (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    token text NOT NULL,
    expires_at timestamp with time zone NOT NULL,
    is_revoked boolean NOT NULL DEFAULT FALSE,
    revoked_at timestamp with time zone,
    replaced_by_token text,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_refresh_tokens" PRIMARY KEY (id),
    CONSTRAINT "FK_refresh_tokens_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_refresh_tokens_token" ON refresh_tokens (token);

CREATE INDEX "IX_refresh_tokens_user_id_is_revoked" ON refresh_tokens (user_id, is_revoked);

CREATE UNIQUE INDEX "IX_users_email" ON users (email);

CREATE UNIQUE INDEX "IX_users_phone" ON users (phone) WHERE phone IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260607092654_InitialAuth', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE TABLE workspace_profiles (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    name character varying(100) NOT NULL,
    location_type character varying(30) NOT NULL,
    style character varying(30) NOT NULL,
    lighting character varying(30) NOT NULL,
    desk_type character varying(30) NOT NULL,
    desk_orientation character varying(15) NOT NULL,
    room_facing_direction character varying(15) NOT NULL,
    work_purpose character varying(30) NOT NULL,
    feng_shui_element character varying(10) NOT NULL,
    desk_area integer NOT NULL,
    is_default boolean NOT NULL DEFAULT FALSE,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_workspace_profiles" PRIMARY KEY (id),
    CONSTRAINT "FK_workspace_profiles_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX "UX_workspace_profiles_user_default" ON workspace_profiles (user_id) WHERE is_default = TRUE AND is_deleted = FALSE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260608071640_WorkspaceProfiles', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE TABLE provinces (
    id uuid NOT NULL,
    name character varying(255) NOT NULL,
    code integer NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_provinces" PRIMARY KEY (id)
);

CREATE TABLE districts (
    id uuid NOT NULL,
    province_id uuid NOT NULL,
    name character varying(255) NOT NULL,
    code integer NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_districts" PRIMARY KEY (id),
    CONSTRAINT "FK_districts_provinces_province_id" FOREIGN KEY (province_id) REFERENCES provinces (id) ON DELETE CASCADE
);

CREATE TABLE wards (
    id uuid NOT NULL,
    district_id uuid NOT NULL,
    name character varying(255) NOT NULL,
    code integer NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_wards" PRIMARY KEY (id),
    CONSTRAINT "FK_wards_districts_district_id" FOREIGN KEY (district_id) REFERENCES districts (id) ON DELETE CASCADE
);

CREATE TABLE user_address (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    ward_id uuid NOT NULL,
    street_address character varying(255) NOT NULL,
    recipient_name character varying(255) NOT NULL,
    recipient_phone character varying(20) NOT NULL,
    latitude numeric(10,8),
    longitude numeric(11,8),
    is_default boolean NOT NULL DEFAULT FALSE,
    label character varying(50),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_user_address" PRIMARY KEY (id),
    CONSTRAINT "FK_user_address_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
    CONSTRAINT "FK_user_address_wards_ward_id" FOREIGN KEY (ward_id) REFERENCES wards (id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_districts_code" ON districts (code);

CREATE INDEX "IX_districts_province_id" ON districts (province_id);

CREATE UNIQUE INDEX "IX_provinces_code" ON provinces (code);

CREATE INDEX "IX_user_address_ward_id" ON user_address (ward_id);

CREATE UNIQUE INDEX "UX_user_address_user_default" ON user_address (user_id) WHERE is_default = TRUE AND is_deleted = FALSE;

CREATE UNIQUE INDEX "IX_wards_code" ON wards (code);

CREATE INDEX "IX_wards_district_id" ON wards (district_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260608155940_Geography', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE TABLE carts (
    id uuid NOT NULL,
    customer_id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_carts" PRIMARY KEY (id),
    CONSTRAINT "FK_carts_users_customer_id" FOREIGN KEY (customer_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE TABLE categories (
    id uuid NOT NULL,
    name character varying(100) NOT NULL,
    description text,
    parent_id uuid,
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_categories" PRIMARY KEY (id),
    CONSTRAINT "FK_categories_categories_parent_id" FOREIGN KEY (parent_id) REFERENCES categories (id) ON DELETE RESTRICT
);

CREATE TABLE garden_stores (
    id uuid NOT NULL,
    owner_id uuid NOT NULL,
    name character varying(255) NOT NULL,
    description text,
    hotline character varying(20) NOT NULL,
    opening_hours character varying(100),
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_garden_stores" PRIMARY KEY (id),
    CONSTRAINT "FK_garden_stores_users_owner_id" FOREIGN KEY (owner_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE TABLE orders (
    id uuid NOT NULL,
    customer_id uuid NOT NULL,
    shipping_address_id uuid NOT NULL,
    status character varying(50) NOT NULL,
    subtotal numeric(12,2) NOT NULL,
    total_shipping_fee numeric(12,2) NOT NULL,
    total_amount numeric(12,2) NOT NULL,
    note text,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_orders" PRIMARY KEY (id),
    CONSTRAINT "FK_orders_user_address_shipping_address_id" FOREIGN KEY (shipping_address_id) REFERENCES user_address (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_orders_users_customer_id" FOREIGN KEY (customer_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE TABLE tags (
    id uuid NOT NULL,
    name character varying(50) NOT NULL,
    description text,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_tags" PRIMARY KEY (id)
);

CREATE TABLE garden_staff_assignments (
    id uuid NOT NULL,
    garden_store_id uuid NOT NULL,
    staff_id uuid NOT NULL,
    assigned_by uuid NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    assigned_at timestamp with time zone NOT NULL,
    unassigned_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_garden_staff_assignments" PRIMARY KEY (id),
    CONSTRAINT "FK_garden_staff_assignments_garden_stores_garden_store_id" FOREIGN KEY (garden_store_id) REFERENCES garden_stores (id) ON DELETE CASCADE,
    CONSTRAINT "FK_garden_staff_assignments_users_staff_id" FOREIGN KEY (staff_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE TABLE products (
    id uuid NOT NULL,
    garden_store_id uuid NOT NULL,
    name character varying(255) NOT NULL,
    description text,
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_products" PRIMARY KEY (id),
    CONSTRAINT "FK_products_garden_stores_garden_store_id" FOREIGN KEY (garden_store_id) REFERENCES garden_stores (id) ON DELETE CASCADE
);

CREATE TABLE stores_address (
    id uuid NOT NULL,
    store_id uuid NOT NULL,
    ward_id uuid NOT NULL,
    street_address character varying(255) NOT NULL,
    latitude numeric(10,8),
    longitude numeric(11,8),
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_stores_address" PRIMARY KEY (id),
    CONSTRAINT "FK_stores_address_garden_stores_store_id" FOREIGN KEY (store_id) REFERENCES garden_stores (id) ON DELETE CASCADE,
    CONSTRAINT "FK_stores_address_wards_ward_id" FOREIGN KEY (ward_id) REFERENCES wards (id) ON DELETE RESTRICT
);

CREATE TABLE deliveries (
    id uuid NOT NULL,
    order_id uuid NOT NULL,
    garden_store_id uuid NOT NULL,
    status character varying(50) NOT NULL,
    tracking_code character varying(100),
    provider_order_id character varying(100),
    shipping_provider character varying(50),
    shipping_fee numeric(12,2) NOT NULL,
    subtotal numeric(12,2) NOT NULL,
    assigned_at timestamp with time zone,
    shipped_at timestamp with time zone,
    delivered_at timestamp with time zone,
    estimated_delivery_date timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_deliveries" PRIMARY KEY (id),
    CONSTRAINT "FK_deliveries_garden_stores_garden_store_id" FOREIGN KEY (garden_store_id) REFERENCES garden_stores (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_deliveries_orders_order_id" FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE CASCADE
);

CREATE TABLE order_status_log (
    id uuid NOT NULL,
    order_id uuid NOT NULL,
    from_status character varying(30),
    to_status character varying(30) NOT NULL,
    changed_by uuid,
    note text,
    changed_at timestamp with time zone NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_order_status_log" PRIMARY KEY (id),
    CONSTRAINT "FK_order_status_log_orders_order_id" FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE CASCADE
);

CREATE TABLE product_categories (
    product_id uuid NOT NULL,
    category_id uuid NOT NULL,
    CONSTRAINT "PK_product_categories" PRIMARY KEY (product_id, category_id),
    CONSTRAINT "FK_product_categories_categories_category_id" FOREIGN KEY (category_id) REFERENCES categories (id) ON DELETE CASCADE,
    CONSTRAINT "FK_product_categories_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE
);

CREATE TABLE product_images (
    id uuid NOT NULL,
    product_id uuid NOT NULL,
    url text NOT NULL,
    sort_order integer NOT NULL DEFAULT 0,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_product_images" PRIMARY KEY (id),
    CONSTRAINT "FK_product_images_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE
);

CREATE TABLE product_items (
    id uuid NOT NULL,
    product_id uuid NOT NULL,
    name character varying(100),
    price numeric(12,2) NOT NULL,
    stock integer NOT NULL DEFAULT 0,
    sku character varying(20),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_product_items" PRIMARY KEY (id),
    CONSTRAINT "FK_product_items_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE
);

CREATE TABLE product_tags (
    product_id uuid NOT NULL,
    tag_id uuid NOT NULL,
    CONSTRAINT "PK_product_tags" PRIMARY KEY (product_id, tag_id),
    CONSTRAINT "FK_product_tags_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE,
    CONSTRAINT "FK_product_tags_tags_tag_id" FOREIGN KEY (tag_id) REFERENCES tags (id) ON DELETE CASCADE
);

CREATE TABLE cart_items (
    id uuid NOT NULL,
    cart_id uuid NOT NULL,
    product_item_id uuid NOT NULL,
    quantity integer NOT NULL,
    added_at timestamp with time zone NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_cart_items" PRIMARY KEY (id),
    CONSTRAINT "FK_cart_items_carts_cart_id" FOREIGN KEY (cart_id) REFERENCES carts (id) ON DELETE CASCADE,
    CONSTRAINT "FK_cart_items_product_items_product_item_id" FOREIGN KEY (product_item_id) REFERENCES product_items (id) ON DELETE RESTRICT
);

CREATE TABLE order_items (
    id uuid NOT NULL,
    order_id uuid NOT NULL,
    product_item_id uuid NOT NULL,
    delivery_id uuid NOT NULL,
    product_name character varying(255) NOT NULL,
    unit_price numeric(12,2) NOT NULL,
    quantity integer NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_order_items" PRIMARY KEY (id),
    CONSTRAINT "FK_order_items_deliveries_delivery_id" FOREIGN KEY (delivery_id) REFERENCES deliveries (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_order_items_orders_order_id" FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE CASCADE,
    CONSTRAINT "FK_order_items_product_items_product_item_id" FOREIGN KEY (product_item_id) REFERENCES product_items (id) ON DELETE RESTRICT
);

CREATE INDEX "IX_cart_items_product_item_id" ON cart_items (product_item_id);

CREATE UNIQUE INDEX "UX_cart_items_cart_product_item" ON cart_items (cart_id, product_item_id) WHERE is_deleted = FALSE;

CREATE UNIQUE INDEX "IX_carts_customer_id" ON carts (customer_id);

CREATE INDEX "IX_categories_parent_id" ON categories (parent_id);

CREATE INDEX "IX_deliveries_garden_store_id" ON deliveries (garden_store_id);

CREATE INDEX "IX_deliveries_order_id" ON deliveries (order_id);

CREATE INDEX "IX_garden_staff_assignments_staff_id" ON garden_staff_assignments (staff_id);

CREATE UNIQUE INDEX "UX_garden_staff_active" ON garden_staff_assignments (garden_store_id, staff_id) WHERE is_active = TRUE AND is_deleted = FALSE;

CREATE INDEX "IX_garden_stores_owner_id" ON garden_stores (owner_id);

CREATE INDEX "IX_order_items_delivery_id" ON order_items (delivery_id);

CREATE INDEX "IX_order_items_order_id" ON order_items (order_id);

CREATE INDEX "IX_order_items_product_item_id" ON order_items (product_item_id);

CREATE INDEX "IX_order_status_log_order_id" ON order_status_log (order_id);

CREATE INDEX "IX_orders_customer_id" ON orders (customer_id);

CREATE INDEX "IX_orders_shipping_address_id" ON orders (shipping_address_id);

CREATE INDEX "IX_product_categories_category_id" ON product_categories (category_id);

CREATE INDEX "IX_product_images_product_id" ON product_images (product_id);

CREATE INDEX "IX_product_items_product_id" ON product_items (product_id);

CREATE UNIQUE INDEX "IX_product_items_sku" ON product_items (sku) WHERE sku IS NOT NULL AND is_deleted = FALSE;

CREATE INDEX "IX_product_tags_tag_id" ON product_tags (tag_id);

CREATE INDEX "IX_products_garden_store_id" ON products (garden_store_id);

CREATE UNIQUE INDEX "IX_stores_address_store_id" ON stores_address (store_id);

CREATE INDEX "IX_stores_address_ward_id" ON stores_address (ward_id);

CREATE UNIQUE INDEX "IX_tags_name" ON tags (name) WHERE is_deleted = FALSE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260608175317_VendorCatalogSales', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE TABLE delivery_progress_logs (
    id uuid NOT NULL,
    delivery_id uuid NOT NULL,
    source_type integer NOT NULL,
    from_status character varying(50),
    to_status character varying(50),
    raw_payload jsonb,
    note text,
    logged_at timestamp with time zone NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_delivery_progress_logs" PRIMARY KEY (id),
    CONSTRAINT "FK_delivery_progress_logs_deliveries_delivery_id" FOREIGN KEY (delivery_id) REFERENCES deliveries (id) ON DELETE CASCADE
);

CREATE TABLE shipping_webhook (
    id uuid NOT NULL,
    provider character varying(50),
    event_type character varying(100),
    payload jsonb NOT NULL,
    is_processed boolean NOT NULL DEFAULT FALSE,
    received_at timestamp with time zone NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_shipping_webhook" PRIMARY KEY (id)
);

CREATE INDEX "IX_delivery_progress_logs_delivery_id" ON delivery_progress_logs (delivery_id);

CREATE INDEX "IX_shipping_webhook_is_processed" ON shipping_webhook (is_processed);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260608180045_Shipping', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE TABLE transaction (
    id uuid NOT NULL,
    order_id uuid NOT NULL,
    order_code bigint NOT NULL,
    amount numeric(12,2) NOT NULL,
    payment_method character varying(20) NOT NULL,
    status character varying(20) NOT NULL,
    provider_transaction_id character varying(100),
    paid_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_transaction" PRIMARY KEY (id),
    CONSTRAINT "FK_transaction_orders_order_id" FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_transaction_order_code" ON transaction (order_code);

CREATE INDEX "IX_transaction_order_id" ON transaction (order_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260609134602_Payment', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE orders ADD payment_method character varying(30) NOT NULL DEFAULT 'PayOS';

ALTER TABLE order_items ALTER COLUMN delivery_id DROP NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260610041927_CodAndOrderExpiration', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE TABLE notifications (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    type character varying(30) NOT NULL,
    title character varying(200) NOT NULL,
    message character varying(1000) NOT NULL,
    is_read boolean NOT NULL DEFAULT FALSE,
    read_at timestamp with time zone,
    reference_id uuid,
    reference_type character varying(50),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_notifications" PRIMARY KEY (id),
    CONSTRAINT "FK_notifications_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE INDEX "IX_notifications_user_id_created_at" ON notifications (user_id, created_at);

CREATE INDEX "IX_notifications_user_id_is_read" ON notifications (user_id, is_read);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260611113949_AddNotification', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE TABLE chatboxes (
    id uuid NOT NULL,
    sender_user_id uuid NOT NULL,
    recipient_user_id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_chatboxes" PRIMARY KEY (id),
    CONSTRAINT "FK_chatboxes_users_recipient_user_id" FOREIGN KEY (recipient_user_id) REFERENCES users (id) ON DELETE CASCADE,
    CONSTRAINT "FK_chatboxes_users_sender_user_id" FOREIGN KEY (sender_user_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE TABLE chat_messages (
    id uuid NOT NULL,
    chatbox_id uuid NOT NULL,
    sender_user_id uuid NOT NULL,
    content character varying(5000) NOT NULL,
    is_read boolean NOT NULL DEFAULT FALSE,
    read_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_chat_messages" PRIMARY KEY (id),
    CONSTRAINT "FK_chat_messages_chatboxes_chatbox_id" FOREIGN KEY (chatbox_id) REFERENCES chatboxes (id) ON DELETE CASCADE,
    CONSTRAINT "FK_chat_messages_users_sender_user_id" FOREIGN KEY (sender_user_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE INDEX "IX_chat_messages_chatbox_id_created_at" ON chat_messages (chatbox_id, created_at);

CREATE INDEX "IX_chat_messages_chatbox_id_is_read" ON chat_messages (chatbox_id, is_read);

CREATE INDEX "IX_chat_messages_sender_user_id" ON chat_messages (sender_user_id);

CREATE INDEX "IX_chatboxes_recipient_user_id" ON chatboxes (recipient_user_id);

CREATE INDEX "IX_chatboxes_sender_user_id" ON chatboxes (sender_user_id);

CREATE UNIQUE INDEX "IX_chatboxes_sender_user_id_recipient_user_id" ON chatboxes (sender_user_id, recipient_user_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260612131528_AddChat', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE TABLE reviews (
    id uuid NOT NULL,
    content character varying(2000) NOT NULL,
    rating integer NOT NULL,
    user_id uuid NOT NULL,
    product_id uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_reviews" PRIMARY KEY (id),
    CONSTRAINT "FK_reviews_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE,
    CONSTRAINT "FK_reviews_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE INDEX "IX_reviews_product_id" ON reviews (product_id);

CREATE UNIQUE INDEX "UX_reviews_user_product" ON reviews (user_id, product_id) WHERE is_deleted = FALSE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260615132303_AddReviewEntity', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE notifications ALTER COLUMN reference_type TYPE character varying(30);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260615181431_NormalizeNotificationReferenceType', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE workspace_profiles ADD workspace_type_id uuid;

CREATE TABLE feng_shui_rules (
    id uuid NOT NULL,
    subject_element character varying(10) NOT NULL,
    object_element character varying(10) NOT NULL,
    relation character varying(20) NOT NULL,
    score numeric(4,2) NOT NULL,
    description character varying(500),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_feng_shui_rules" PRIMARY KEY (id)
);

CREATE TABLE product_feng_shui (
    product_id uuid NOT NULL,
    primary_element character varying(10) NOT NULL,
    secondary_element character varying(10),
    size_class character varying(10) NOT NULL,
    CONSTRAINT "PK_product_feng_shui" PRIMARY KEY (product_id),
    CONSTRAINT "FK_product_feng_shui_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE
);

CREATE TABLE product_styles (
    product_id uuid NOT NULL,
    style character varying(30) NOT NULL,
    CONSTRAINT "PK_product_styles" PRIMARY KEY (product_id, style),
    CONSTRAINT "FK_product_styles_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE
);

CREATE TABLE product_vibes (
    product_id uuid NOT NULL,
    vibe character varying(20) NOT NULL,
    CONSTRAINT "PK_product_vibes" PRIMARY KEY (product_id, vibe),
    CONSTRAINT "FK_product_vibes_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE
);

CREATE TABLE workspace_types (
    id uuid NOT NULL,
    name character varying(100) NOT NULL,
    description character varying(500),
    is_public boolean NOT NULL DEFAULT FALSE,
    personal_weight numeric(4,2) NOT NULL DEFAULT 1.0,
    is_system_seeded boolean NOT NULL DEFAULT FALSE,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_workspace_types" PRIMARY KEY (id)
);

CREATE TABLE recommendations (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    workspace_profile_id uuid NOT NULL,
    workspace_type_id uuid,
    customer_element character varying(10),
    kua_number integer,
    kua_group character varying(10),
    personal_weight numeric(4,2) NOT NULL,
    status character varying(20) NOT NULL,
    summary text,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_recommendations" PRIMARY KEY (id),
    CONSTRAINT "FK_recommendations_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
    CONSTRAINT "FK_recommendations_workspace_profiles_workspace_profile_id" FOREIGN KEY (workspace_profile_id) REFERENCES workspace_profiles (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_recommendations_workspace_types_workspace_type_id" FOREIGN KEY (workspace_type_id) REFERENCES workspace_types (id) ON DELETE SET NULL
);

CREATE TABLE recommendation_items (
    id uuid NOT NULL,
    recommendation_id uuid NOT NULL,
    product_id uuid NOT NULL,
    base_score numeric(6,3) NOT NULL,
    base_rank integer NOT NULL,
    final_rank integer NOT NULL,
    match_facts jsonb NOT NULL,
    caution_facts jsonb,
    ai_explanation text,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_recommendation_items" PRIMARY KEY (id),
    CONSTRAINT "FK_recommendation_items_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_recommendation_items_recommendations_recommendation_id" FOREIGN KEY (recommendation_id) REFERENCES recommendations (id) ON DELETE CASCADE
);

CREATE TABLE recommendation_logs (
    id uuid NOT NULL,
    recommendation_id uuid NOT NULL,
    stage character varying(50) NOT NULL,
    detail jsonb,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_recommendation_logs" PRIMARY KEY (id),
    CONSTRAINT "FK_recommendation_logs_recommendations_recommendation_id" FOREIGN KEY (recommendation_id) REFERENCES recommendations (id) ON DELETE CASCADE
);

CREATE INDEX "IX_workspace_profiles_workspace_type_id" ON workspace_profiles (workspace_type_id);

CREATE UNIQUE INDEX "IX_feng_shui_rules_subject_element_object_element" ON feng_shui_rules (subject_element, object_element) WHERE is_deleted = FALSE;

CREATE INDEX "IX_recommendation_items_product_id" ON recommendation_items (product_id);

CREATE INDEX "IX_recommendation_items_recommendation_id" ON recommendation_items (recommendation_id);

CREATE INDEX "IX_recommendation_logs_recommendation_id" ON recommendation_logs (recommendation_id);

CREATE INDEX "IX_recommendations_user_id" ON recommendations (user_id);

CREATE INDEX "IX_recommendations_workspace_profile_id" ON recommendations (workspace_profile_id);

CREATE INDEX "IX_recommendations_workspace_type_id" ON recommendations (workspace_type_id);

CREATE INDEX "IX_workspace_types_name" ON workspace_types (name);

ALTER TABLE workspace_profiles ADD CONSTRAINT "FK_workspace_profiles_workspace_types_workspace_type_id" FOREIGN KEY (workspace_type_id) REFERENCES workspace_types (id) ON DELETE SET NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260616071306_RecommendationSchema', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE chat_messages DROP CONSTRAINT "FK_chat_messages_users_sender_user_id";

ALTER TABLE chatboxes ALTER COLUMN recipient_user_id DROP NOT NULL;

ALTER TABLE chatboxes ADD product_id uuid;

ALTER TABLE chatboxes ADD type integer NOT NULL DEFAULT 0;

ALTER TABLE chat_messages ALTER COLUMN sender_user_id DROP NOT NULL;

ALTER TABLE chat_messages ALTER COLUMN content DROP NOT NULL;

ALTER TABLE chat_messages ADD is_from_ai boolean NOT NULL DEFAULT FALSE;

ALTER TABLE chat_messages ADD sender_name character varying(100);

ALTER TABLE chat_messages ADD sender_role integer NOT NULL DEFAULT 0;

CREATE TABLE chat_message_images (
    id uuid NOT NULL,
    chat_message_id uuid NOT NULL,
    url text NOT NULL,
    sort_order integer NOT NULL DEFAULT 0,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_chat_message_images" PRIMARY KEY (id),
    CONSTRAINT "FK_chat_message_images_chat_messages_chat_message_id" FOREIGN KEY (chat_message_id) REFERENCES chat_messages (id) ON DELETE CASCADE
);

CREATE INDEX "IX_chatboxes_product_id" ON chatboxes (product_id);

CREATE INDEX "IX_chat_message_images_chat_message_id" ON chat_message_images (chat_message_id);

ALTER TABLE chat_messages ADD CONSTRAINT "FK_chat_messages_users_sender_user_id" FOREIGN KEY (sender_user_id) REFERENCES users (id) ON DELETE SET NULL;

ALTER TABLE chatboxes ADD CONSTRAINT "FK_chatboxes_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE SET NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260616211602_EvolveChatboxForAi', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE products ADD size_class character varying(10);

CREATE TABLE product_element (
    product_id uuid NOT NULL,
    element character varying(10) NOT NULL,
    is_primary boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_product_element" PRIMARY KEY (product_id, element),
    CONSTRAINT "FK_product_element_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE
);

CREATE INDEX "IX_product_element_product_id_is_primary" ON product_element (product_id, is_primary);


INSERT INTO product_element (product_id, element, is_primary)
SELECT product_id, primary_element, true FROM product_feng_shui;

INSERT INTO product_element (product_id, element, is_primary)
SELECT product_id, secondary_element, false FROM product_feng_shui
WHERE secondary_element IS NOT NULL AND secondary_element <> primary_element;

UPDATE products p SET size_class = f.size_class
FROM product_feng_shui f WHERE p.id = f.product_id;

DROP TABLE product_feng_shui;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260616212258_ProductElementsJunction', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE workspace_profiles RENAME COLUMN style TO style_code;

ALTER TABLE product_vibes RENAME COLUMN vibe TO vibe_code;

ALTER TABLE product_styles RENAME COLUMN style TO style_code;

CREATE TABLE styles (
    code character varying(30) NOT NULL,
    name character varying(100) NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    sort_order integer NOT NULL DEFAULT 0,
    CONSTRAINT "PK_styles" PRIMARY KEY (code)
);

CREATE TABLE vibes (
    code character varying(20) NOT NULL,
    name character varying(100) NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    sort_order integer NOT NULL DEFAULT 0,
    CONSTRAINT "PK_vibes" PRIMARY KEY (code)
);


INSERT INTO styles (code, name, is_active, sort_order) VALUES
  ('Modern','Hiện đại',true,1),
  ('Classic','Cổ điển',true,2),
  ('Minimal','Tối giản',true,3),
  ('Industrial','Công nghiệp',true,4),
  ('Scandinavian','Bắc Âu',true,5),
  ('Bohemian','Bohemian',true,6),
  ('Other','Khác',true,99)
ON CONFLICT (code) DO NOTHING;

INSERT INTO vibes (code, name, is_active, sort_order) VALUES
  ('Focus','Tập trung',true,1),
  ('Relax','Thư giãn',true,2),
  ('Creative','Sáng tạo',true,3),
  ('Calm','Tĩnh tại',true,4),
  ('Energize','Năng lượng',true,5)
ON CONFLICT (code) DO NOTHING;

CREATE INDEX "IX_workspace_profiles_style_code" ON workspace_profiles (style_code);

CREATE INDEX "IX_product_vibes_vibe_code" ON product_vibes (vibe_code);

CREATE INDEX "IX_product_styles_style_code" ON product_styles (style_code);

ALTER TABLE product_styles ADD CONSTRAINT "FK_product_styles_styles_style_code" FOREIGN KEY (style_code) REFERENCES styles (code) ON DELETE RESTRICT;

ALTER TABLE product_vibes ADD CONSTRAINT "FK_product_vibes_vibes_vibe_code" FOREIGN KEY (vibe_code) REFERENCES vibes (code) ON DELETE RESTRICT;

ALTER TABLE workspace_profiles ADD CONSTRAINT "FK_workspace_profiles_styles_style_code" FOREIGN KEY (style_code) REFERENCES styles (code) ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260617093852_StyleVibeLookupTables', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE TABLE elements (
    code character varying(10) NOT NULL,
    name character varying(100) NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    sort_order integer NOT NULL DEFAULT 0,
    CONSTRAINT "PK_elements" PRIMARY KEY (code)
);


INSERT INTO elements (code, name, is_active, sort_order) VALUES
  ('Kim','Kim (Metal)',true,1),
  ('Moc','Mộc (Wood)',true,2),
  ('Thuy','Thủy (Water)',true,3),
  ('Hoa','Hỏa (Fire)',true,4),
  ('Tho','Thổ (Earth)',true,5)
ON CONFLICT (code) DO NOTHING;


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

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260617103525_ElementLookupAndCascade', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE chat_messages DROP CONSTRAINT "FK_chat_messages_users_sender_user_id";

ALTER TABLE chatboxes DROP CONSTRAINT "FK_chatboxes_users_recipient_user_id";

ALTER TABLE chatboxes DROP CONSTRAINT "FK_chatboxes_users_sender_user_id";

DROP INDEX "IX_chatboxes_recipient_user_id";

DROP INDEX "IX_chatboxes_sender_user_id_recipient_user_id";

DROP INDEX "IX_chat_messages_chatbox_id_is_read";

ALTER TABLE chat_messages ADD sender_type character varying(20) NOT NULL DEFAULT 'User';

UPDATE chat_messages SET sender_type = CASE WHEN is_from_ai THEN 'AiBot' ELSE 'User' END;

ALTER TABLE chat_messages DROP COLUMN is_from_ai;

ALTER TABLE chat_messages DROP COLUMN is_read;

ALTER TABLE chat_messages DROP COLUMN read_at;

ALTER TABLE chat_messages DROP COLUMN sender_role;

ALTER TABLE chat_messages RENAME COLUMN sender_user_id TO sender_id;

ALTER INDEX "IX_chat_messages_sender_user_id" RENAME TO "IX_chat_messages_sender_id";

ALTER TABLE chatboxes ADD is_group boolean NOT NULL DEFAULT TRUE;

ALTER TABLE chatboxes ADD title character varying(200);

ALTER TABLE chatboxes RENAME COLUMN sender_user_id TO created_by_user_id;

ALTER INDEX "IX_chatboxes_sender_user_id" RENAME TO "IX_chatboxes_created_by_user_id";

CREATE TABLE chatbox_participants (
    id uuid NOT NULL,
    chatbox_id uuid NOT NULL,
    user_id uuid,
    participant_type character varying(20) NOT NULL,
    role character varying(20) NOT NULL,
    is_muted boolean NOT NULL DEFAULT FALSE,
    is_hidden boolean NOT NULL DEFAULT FALSE,
    last_read_at timestamp with time zone,
    joined_at timestamp with time zone NOT NULL,
    CONSTRAINT "PK_chatbox_participants" PRIMARY KEY (id),
    CONSTRAINT "FK_chatbox_participants_chatboxes_chatbox_id" FOREIGN KEY (chatbox_id) REFERENCES chatboxes (id) ON DELETE CASCADE,
    CONSTRAINT "FK_chatbox_participants_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_chatbox_participants_chatbox_id_user_id" ON chatbox_participants (chatbox_id, user_id);

CREATE INDEX "IX_chatbox_participants_user_id" ON chatbox_participants (user_id);

ALTER TABLE chat_messages ADD CONSTRAINT "FK_chat_messages_users_sender_id" FOREIGN KEY (sender_id) REFERENCES users (id) ON DELETE SET NULL;

ALTER TABLE chatboxes ADD CONSTRAINT "FK_chatboxes_users_created_by_user_id" FOREIGN KEY (created_by_user_id) REFERENCES users (id) ON DELETE CASCADE;


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
FROM chatboxes WHERE type = 1;

ALTER TABLE chatboxes DROP COLUMN recipient_user_id;

ALTER TABLE chatboxes DROP COLUMN type;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260617140748_ChatParticipantsModel', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE chatboxes ADD is_ai_enabled boolean NOT NULL DEFAULT FALSE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260617211758_ChatboxIsAiEnabled', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE TABLE product_model3ds (
    id uuid NOT NULL,
    product_id uuid NOT NULL,
    status text NOT NULL,
    source_image_url text NOT NULL,
    meshy_task_id text,
    model_url text,
    thumbnail_url text,
    progress integer NOT NULL DEFAULT 0,
    error_message text,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_product_model3ds" PRIMARY KEY (id),
    CONSTRAINT "FK_product_model3ds_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_product_model3ds_product_id" ON product_model3ds (product_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260620060703_ProductModel3D', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE chatboxes ADD is_support boolean NOT NULL DEFAULT FALSE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260620085249_ChatboxIsSupport', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE TABLE chat_room_data_consents (
    id uuid NOT NULL,
    chatbox_id uuid NOT NULL,
    granter_user_id uuid NOT NULL,
    share_profile boolean NOT NULL DEFAULT FALSE,
    share_workspaces boolean NOT NULL DEFAULT FALSE,
    share_orders boolean NOT NULL DEFAULT FALSE,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_chat_room_data_consents" PRIMARY KEY (id),
    CONSTRAINT "FK_chat_room_data_consents_chatboxes_chatbox_id" FOREIGN KEY (chatbox_id) REFERENCES chatboxes (id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_chat_room_data_consents_chatbox_id_granter_user_id" ON chat_room_data_consents (chatbox_id, granter_user_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260620110958_ChatRoomDataConsent', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE EXTENSION IF NOT EXISTS unaccent;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260620155312_EnableUnaccentExtension', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE TABLE return_requests (
    id uuid NOT NULL,
    order_id uuid NOT NULL,
    delivery_id uuid NOT NULL,
    customer_id uuid NOT NULL,
    type character varying(20) NOT NULL,
    status character varying(30) NOT NULL,
    reason character varying(30) NOT NULL,
    reason_detail text,
    refund_amount numeric(12,2) NOT NULL,
    refund_method character varying(20) NOT NULL,
    bank_account_name character varying(100),
    bank_account_number character varying(50),
    bank_name character varying(100),
    return_tracking_code character varying(100),
    approved_by uuid,
    approved_at timestamp with time zone,
    rejected_reason text,
    received_at timestamp with time zone,
    replacement_delivery_id uuid,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_return_requests" PRIMARY KEY (id),
    CONSTRAINT "FK_return_requests_deliveries_delivery_id" FOREIGN KEY (delivery_id) REFERENCES deliveries (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_return_requests_orders_order_id" FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT
);

CREATE TABLE refunds (
    id uuid NOT NULL,
    return_request_id uuid NOT NULL,
    order_id uuid NOT NULL,
    transaction_id uuid,
    amount numeric(12,2) NOT NULL,
    method character varying(20) NOT NULL,
    status character varying(20) NOT NULL,
    provider_refund_id character varying(100),
    processed_by uuid,
    processed_at timestamp with time zone,
    completed_at timestamp with time zone,
    note text,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_refunds" PRIMARY KEY (id),
    CONSTRAINT "FK_refunds_orders_order_id" FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_refunds_return_requests_return_request_id" FOREIGN KEY (return_request_id) REFERENCES return_requests (id) ON DELETE CASCADE,
    CONSTRAINT "FK_refunds_transaction_transaction_id" FOREIGN KEY (transaction_id) REFERENCES transaction (id) ON DELETE RESTRICT
);

CREATE TABLE return_items (
    id uuid NOT NULL,
    return_request_id uuid NOT NULL,
    order_item_id uuid NOT NULL,
    quantity integer NOT NULL,
    unit_price numeric(12,2) NOT NULL,
    exchange_product_item_id uuid,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_return_items" PRIMARY KEY (id),
    CONSTRAINT "FK_return_items_order_items_order_item_id" FOREIGN KEY (order_item_id) REFERENCES order_items (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_return_items_product_items_exchange_product_item_id" FOREIGN KEY (exchange_product_item_id) REFERENCES product_items (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_return_items_return_requests_return_request_id" FOREIGN KEY (return_request_id) REFERENCES return_requests (id) ON DELETE CASCADE
);

CREATE TABLE return_request_images (
    id uuid NOT NULL,
    return_request_id uuid NOT NULL,
    image_url text NOT NULL,
    sort_order integer NOT NULL DEFAULT 0,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_return_request_images" PRIMARY KEY (id),
    CONSTRAINT "FK_return_request_images_return_requests_return_request_id" FOREIGN KEY (return_request_id) REFERENCES return_requests (id) ON DELETE CASCADE
);

CREATE TABLE return_status_logs (
    id uuid NOT NULL,
    return_request_id uuid NOT NULL,
    from_status character varying(30),
    to_status character varying(30) NOT NULL,
    changed_by uuid,
    note text,
    changed_at timestamp with time zone NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_return_status_logs" PRIMARY KEY (id),
    CONSTRAINT "FK_return_status_logs_return_requests_return_request_id" FOREIGN KEY (return_request_id) REFERENCES return_requests (id) ON DELETE CASCADE
);

CREATE INDEX "IX_refunds_order_id" ON refunds (order_id);

CREATE UNIQUE INDEX "IX_refunds_return_request_id" ON refunds (return_request_id);

CREATE INDEX "IX_refunds_transaction_id" ON refunds (transaction_id);

CREATE INDEX "IX_return_items_exchange_product_item_id" ON return_items (exchange_product_item_id);

CREATE INDEX "IX_return_items_order_item_id" ON return_items (order_item_id);

CREATE INDEX "IX_return_items_return_request_id" ON return_items (return_request_id);

CREATE INDEX "IX_return_request_images_return_request_id" ON return_request_images (return_request_id);

CREATE INDEX "IX_return_requests_customer_id" ON return_requests (customer_id);

CREATE INDEX "IX_return_requests_delivery_id" ON return_requests (delivery_id);

CREATE INDEX "IX_return_requests_order_id" ON return_requests (order_id);

CREATE INDEX "IX_return_status_logs_return_request_id" ON return_status_logs (return_request_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260622055338_AddReturnsAndRefunds', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE TABLE garden_store_owners (
    id uuid NOT NULL,
    garden_store_id uuid NOT NULL,
    owner_user_id uuid NOT NULL,
    is_primary boolean NOT NULL DEFAULT FALSE,
    assigned_at timestamp with time zone NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_garden_store_owners" PRIMARY KEY (id),
    CONSTRAINT "FK_garden_store_owners_garden_stores_garden_store_id" FOREIGN KEY (garden_store_id) REFERENCES garden_stores (id) ON DELETE CASCADE,
    CONSTRAINT "FK_garden_store_owners_users_owner_user_id" FOREIGN KEY (owner_user_id) REFERENCES users (id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_garden_store_owners_garden_store_id_owner_user_id" ON garden_store_owners (garden_store_id, owner_user_id);

CREATE INDEX "IX_garden_store_owners_owner_user_id" ON garden_store_owners (owner_user_id);


                INSERT INTO garden_store_owners (id, garden_store_id, owner_user_id, is_primary, assigned_at, created_at, updated_at, is_deleted)
                SELECT gen_random_uuid(), s.id, s.owner_id, true, now(), now(), now(), false
                FROM garden_stores s;


                UPDATE users SET role = role | 16
                WHERE id IN (SELECT owner_id FROM garden_stores)
                  AND (role & 16) = 0;

ALTER TABLE garden_stores DROP CONSTRAINT "FK_garden_stores_users_owner_id";

DROP INDEX "IX_garden_stores_owner_id";

ALTER TABLE garden_stores DROP COLUMN owner_id;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260623171013_GardenStoreOwners', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE product_items ADD weight_gram integer NOT NULL DEFAULT 500;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260625081644_ProductItemWeight', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE product_items ADD height_cm integer NOT NULL DEFAULT 10;

ALTER TABLE product_items ADD length_cm integer NOT NULL DEFAULT 10;

ALTER TABLE product_items ADD width_cm integer NOT NULL DEFAULT 10;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260625101528_ProductItemDimensions', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE wards ADD ghn_ward_code character varying(20);

ALTER TABLE stores_address ADD sender_name character varying(100);

ALTER TABLE stores_address ADD sender_phone character varying(20);

ALTER TABLE provinces ADD ghn_province_id integer;

ALTER TABLE garden_stores ADD ghn_shop_id integer;

ALTER TABLE districts ADD ghn_district_id integer;

CREATE INDEX "IX_wards_ghn_ward_code" ON wards (ghn_ward_code);

CREATE INDEX "IX_districts_ghn_district_id" ON districts (ghn_district_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260625113600_GhnAddressMapping', '8.0.10');

COMMIT;

START TRANSACTION;

DROP INDEX "IX_wards_ghn_ward_code";

DROP INDEX "IX_districts_ghn_district_id";

ALTER TABLE wards DROP COLUMN ghn_ward_code;

ALTER TABLE provinces DROP COLUMN ghn_province_id;

ALTER TABLE garden_stores DROP COLUMN ghn_shop_id;

ALTER TABLE districts DROP COLUMN ghn_district_id;

ALTER TABLE garden_stores ADD ahamove_service_id character varying(50);

ALTER TABLE deliveries ADD tracking_url character varying(500);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260625143118_AhamoveShippingFields', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE wards ADD ghn_ward_code character varying(20);

ALTER TABLE garden_stores ADD ghn_service_type_id integer NOT NULL DEFAULT 2;

ALTER TABLE garden_stores ADD ghn_shop_id integer;

ALTER TABLE districts ADD ghn_district_id integer;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260626053343_GhnShippingFields', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE provinces ADD ghn_province_id integer;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260626095854_GhnProvinceCode', '8.0.10');

COMMIT;

START TRANSACTION;

DROP INDEX "UX_garden_staff_active";

ALTER TABLE garden_staff_assignments RENAME COLUMN assigned_by TO invited_by;

ALTER TABLE garden_staff_assignments RENAME COLUMN assigned_at TO invited_at;

ALTER TABLE garden_staff_assignments ADD responded_at timestamp with time zone;

ALTER TABLE garden_staff_assignments ADD status character varying(20) NOT NULL DEFAULT 'Pending';


                UPDATE garden_staff_assignments
                SET status = CASE WHEN is_active THEN 'Accepted' ELSE 'Revoked' END;
            


                UPDATE garden_staff_assignments
                SET responded_at = invited_at
                WHERE status = 'Accepted';
            

ALTER TABLE garden_staff_assignments DROP COLUMN is_active;

CREATE UNIQUE INDEX "UX_garden_staff_active" ON garden_staff_assignments (garden_store_id, staff_id) WHERE status IN ('Pending', 'Accepted') AND is_deleted = FALSE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260701044543_StaffInvitationFlow', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE workspace_types ADD scope character varying(20) NOT NULL DEFAULT 'Private';

ALTER TABLE workspace_profiles ADD dark_directions jsonb NOT NULL DEFAULT ('[]'::jsonb);

ALTER TABLE workspace_profiles ADD entrance_direction character varying(15);

ALTER TABLE workspace_profiles ADD toilet_direction character varying(15);

ALTER TABLE products ADD element_hoa numeric(4,3);

ALTER TABLE products ADD element_kim numeric(4,3);

ALTER TABLE products ADD element_moc numeric(4,3);

ALTER TABLE products ADD element_tho numeric(4,3);

ALTER TABLE products ADD element_thuy numeric(4,3);

ALTER TABLE products ADD is_vector_overridden boolean NOT NULL DEFAULT FALSE;

CREATE TABLE element_input_map (
    id uuid NOT NULL,
    input_kind character varying(10) NOT NULL,
    input_code character varying(30) NOT NULL,
    element character varying(10) NOT NULL,
    weight numeric(4,3) NOT NULL DEFAULT 1.0,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_element_input_map" PRIMARY KEY (id)
);

CREATE TABLE product_element_inputs (
    id uuid NOT NULL,
    product_id uuid NOT NULL,
    input_kind character varying(10) NOT NULL,
    input_code character varying(30) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_product_element_inputs" PRIMARY KEY (id),
    CONSTRAINT "FK_product_element_inputs_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE
);

CREATE TABLE scoring_params (
    id uuid NOT NULL,
    code character varying(30) NOT NULL,
    value numeric(5,3) NOT NULL,
    description character varying(200),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_scoring_params" PRIMARY KEY (id)
);

CREATE TABLE work_purpose_element_modifiers (
    id uuid NOT NULL,
    work_purpose character varying(30) NOT NULL,
    element character varying(10) NOT NULL,
    delta numeric(4,3) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_work_purpose_element_modifiers" PRIMARY KEY (id)
);

CREATE TABLE workspace_profile_inputs (
    id uuid NOT NULL,
    workspace_profile_id uuid NOT NULL,
    input_kind character varying(10) NOT NULL,
    input_code character varying(30) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_workspace_profile_inputs" PRIMARY KEY (id),
    CONSTRAINT "FK_workspace_profile_inputs_workspace_profiles_workspace_profi~" FOREIGN KEY (workspace_profile_id) REFERENCES workspace_profiles (id) ON DELETE CASCADE
);

CREATE TABLE workspace_type_elements (
    id uuid NOT NULL,
    workspace_type_id uuid NOT NULL,
    source character varying(10) NOT NULL,
    element character varying(10) NOT NULL,
    weight numeric(4,3) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_workspace_type_elements" PRIMARY KEY (id),
    CONSTRAINT "FK_workspace_type_elements_workspace_types_workspace_type_id" FOREIGN KEY (workspace_type_id) REFERENCES workspace_types (id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_element_input_map_input_kind_input_code_element" ON element_input_map (input_kind, input_code, element) WHERE is_deleted = false;

CREATE INDEX "IX_product_element_inputs_product_id" ON product_element_inputs (product_id);

CREATE UNIQUE INDEX "IX_scoring_params_code" ON scoring_params (code) WHERE is_deleted = false;

CREATE UNIQUE INDEX "IX_work_purpose_element_modifiers_work_purpose_element" ON work_purpose_element_modifiers (work_purpose, element) WHERE is_deleted = false;

CREATE INDEX "IX_workspace_profile_inputs_workspace_profile_id" ON workspace_profile_inputs (workspace_profile_id);

CREATE UNIQUE INDEX "IX_workspace_type_elements_workspace_type_id_source_element" ON workspace_type_elements (workspace_type_id, source, element) WHERE is_deleted = false;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260707160422_RecommendationScoringV3', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE workspace_profiles ALTER COLUMN room_facing_direction DROP NOT NULL;

ALTER TABLE workspace_profiles ALTER COLUMN lighting DROP NOT NULL;

ALTER TABLE workspace_profiles ALTER COLUMN feng_shui_element DROP NOT NULL;

ALTER TABLE workspace_profiles ALTER COLUMN desk_type DROP NOT NULL;

ALTER TABLE workspace_profiles ALTER COLUMN desk_orientation DROP NOT NULL;

ALTER TABLE workspace_profiles ALTER COLUMN desk_area DROP NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260709221442_WorkspaceInputRelaxation', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE chatboxes ADD garden_store_id uuid;

CREATE INDEX "IX_chatboxes_garden_store_id" ON chatboxes (garden_store_id);

ALTER TABLE chatboxes ADD CONSTRAINT "FK_chatboxes_garden_stores_garden_store_id" FOREIGN KEY (garden_store_id) REFERENCES garden_stores (id) ON DELETE SET NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260712073507_AddStoreIdToChatbox', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE return_requests ADD decided_at timestamp with time zone;

ALTER TABLE return_requests ADD decided_by uuid;

ALTER TABLE return_requests ADD evidence_deadline timestamp with time zone;

ALTER TABLE return_requests ADD vendor_response character varying(20) NOT NULL DEFAULT 'Pending';

ALTER TABLE return_requests ADD vendor_response_deadline timestamp with time zone;

ALTER TABLE refunds ADD evidence_url character varying(500);

ALTER TABLE refunds ADD gateway character varying(30) NOT NULL DEFAULT 'payos';

ALTER TABLE refunds ADD idempotency_key character varying(120) NOT NULL DEFAULT '';

ALTER TABLE refunds ADD is_manual boolean NOT NULL DEFAULT FALSE;

ALTER TABLE refunds ADD manual_reason text;

ALTER TABLE refunds ADD performed_by uuid;

ALTER TABLE refunds ADD retry_count integer NOT NULL DEFAULT 0;

ALTER TABLE deliveries ADD is_exchange boolean NOT NULL DEFAULT FALSE;

CREATE TABLE vendor_liabilities (
    id uuid NOT NULL,
    garden_id uuid NOT NULL,
    ticket_id uuid NOT NULL,
    refund_id uuid,
    amount numeric(12,2) NOT NULL,
    status character varying(20) NOT NULL,
    dispute_reason text,
    dispute_deadline timestamp with time zone NOT NULL,
    resolved_by uuid,
    resolved_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_vendor_liabilities" PRIMARY KEY (id),
    CONSTRAINT "FK_vendor_liabilities_garden_stores_garden_id" FOREIGN KEY (garden_id) REFERENCES garden_stores (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_vendor_liabilities_refunds_refund_id" FOREIGN KEY (refund_id) REFERENCES refunds (id) ON DELETE SET NULL,
    CONSTRAINT "FK_vendor_liabilities_return_requests_ticket_id" FOREIGN KEY (ticket_id) REFERENCES return_requests (id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_refunds_idempotency_key" ON refunds (idempotency_key);

CREATE INDEX "IX_vendor_liabilities_garden_id" ON vendor_liabilities (garden_id);

CREATE INDEX "IX_vendor_liabilities_refund_id" ON vendor_liabilities (refund_id);

CREATE INDEX "IX_vendor_liabilities_status" ON vendor_liabilities (status);

CREATE UNIQUE INDEX "IX_vendor_liabilities_ticket_id" ON vendor_liabilities (ticket_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260713065010_RefactorReturnRmaFlow', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE users ADD birth_time time without time zone;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260716201930_AddUserBirthTime', '8.0.10');

COMMIT;

START TRANSACTION;

CREATE TABLE workspace_product_placements (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    workspace_profile_id uuid NOT NULL,
    order_item_id uuid NOT NULL,
    product_id uuid NOT NULL,
    placed_at timestamp with time zone NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_workspace_product_placements" PRIMARY KEY (id),
    CONSTRAINT "FK_workspace_product_placements_order_items_order_item_id" FOREIGN KEY (order_item_id) REFERENCES order_items (id) ON DELETE CASCADE,
    CONSTRAINT "FK_workspace_product_placements_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE,
    CONSTRAINT "FK_workspace_product_placements_workspace_profiles_workspace_p~" FOREIGN KEY (workspace_profile_id) REFERENCES workspace_profiles (id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_workspace_product_placements_order_item_id" ON workspace_product_placements (order_item_id) WHERE is_deleted = FALSE;

CREATE INDEX "IX_workspace_product_placements_product_id" ON workspace_product_placements (product_id);

CREATE INDEX "IX_workspace_product_placements_workspace_profile_id_user_id" ON workspace_product_placements (workspace_profile_id, user_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260716215111_WorkspaceProductPlacements', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE users ALTER COLUMN password_hash DROP NOT NULL;

ALTER TABLE users ADD auth_provider integer NOT NULL DEFAULT 0;

ALTER TABLE users ADD google_id character varying(255);

CREATE UNIQUE INDEX "IX_users_google_id" ON users (google_id) WHERE google_id IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260726231500_AddGoogleAuthToUsers', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE users ADD token_version integer NOT NULL DEFAULT 0;

ALTER TABLE deliveries ADD assigned_staff_id uuid;

CREATE TABLE authorization_audit_logs (
    "Id" uuid NOT NULL,
    "ActorUserId" uuid NOT NULL,
    "Action" character varying(100) NOT NULL,
    "ResourceType" character varying(100) NOT NULL,
    "ResourceId" uuid,
    "OldValueJson" jsonb,
    "NewValueJson" jsonb,
    "Reason" character varying(1000),
    "IpAddress" character varying(64),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid,
    "UpdatedBy" uuid,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_authorization_audit_logs" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_deliveries_assigned_staff_id" ON deliveries (assigned_staff_id);

CREATE INDEX "IX_authorization_audit_logs_ActorUserId" ON authorization_audit_logs ("ActorUserId");

CREATE INDEX "IX_authorization_audit_logs_CreatedAt" ON authorization_audit_logs ("CreatedAt");

CREATE INDEX "IX_authorization_audit_logs_ResourceType_ResourceId" ON authorization_audit_logs ("ResourceType", "ResourceId");

ALTER TABLE deliveries ADD CONSTRAINT "FK_deliveries_users_assigned_staff_id" FOREIGN KEY (assigned_staff_id) REFERENCES users (id) ON DELETE SET NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260728115908_AuthorizationHardening', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE authorization_audit_logs RENAME COLUMN "Reason" TO reason;

ALTER TABLE authorization_audit_logs RENAME COLUMN "Action" TO action;

ALTER TABLE authorization_audit_logs RENAME COLUMN "Id" TO id;

ALTER TABLE authorization_audit_logs RENAME COLUMN "UpdatedBy" TO updated_by;

ALTER TABLE authorization_audit_logs RENAME COLUMN "UpdatedAt" TO updated_at;

ALTER TABLE authorization_audit_logs RENAME COLUMN "ResourceType" TO resource_type;

ALTER TABLE authorization_audit_logs RENAME COLUMN "ResourceId" TO resource_id;

ALTER TABLE authorization_audit_logs RENAME COLUMN "OldValueJson" TO old_value_json;

ALTER TABLE authorization_audit_logs RENAME COLUMN "NewValueJson" TO new_value_json;

ALTER TABLE authorization_audit_logs RENAME COLUMN "IsDeleted" TO is_deleted;

ALTER TABLE authorization_audit_logs RENAME COLUMN "IpAddress" TO ip_address;

ALTER TABLE authorization_audit_logs RENAME COLUMN "CreatedBy" TO created_by;

ALTER TABLE authorization_audit_logs RENAME COLUMN "CreatedAt" TO created_at;

ALTER TABLE authorization_audit_logs RENAME COLUMN "ActorUserId" TO actor_user_id;

ALTER INDEX "IX_authorization_audit_logs_ResourceType_ResourceId" RENAME TO "IX_authorization_audit_logs_resource_type_resource_id";

ALTER INDEX "IX_authorization_audit_logs_CreatedAt" RENAME TO "IX_authorization_audit_logs_created_at";

ALTER INDEX "IX_authorization_audit_logs_ActorUserId" RENAME TO "IX_authorization_audit_logs_actor_user_id";

ALTER TABLE authorization_audit_logs ALTER COLUMN is_deleted SET DEFAULT FALSE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260728120212_AuthorizationAuditSnakeCase', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE product_model3ds ADD is_enabled boolean NOT NULL DEFAULT TRUE;

CREATE TABLE model3d_requests (
    id uuid NOT NULL,
    product_id uuid NOT NULL,
    request_type text NOT NULL,
    status text NOT NULL,
    requested_by uuid NOT NULL,
    source_image_ids uuid[] NOT NULL,
    meshy_task_id text,
    assigned_staff_id uuid,
    internal_failure_reason text,
    next_attempt_at timestamp with time zone,
    rejected_reason text,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_model3d_requests" PRIMARY KEY (id),
    CONSTRAINT "FK_model3d_requests_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE
);

CREATE INDEX "IX_model3d_requests_product_id" ON model3d_requests (product_id);

CREATE INDEX "IX_model3d_requests_status" ON model3d_requests (status);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260730143014_AddModel3DRequestFlow', '8.0.10');

COMMIT;

START TRANSACTION;

DROP INDEX "IX_product_model3ds_product_id";

ALTER TABLE product_model3ds ADD product_image_id uuid;

ALTER TABLE model3d_requests ADD product_image_id uuid;

UPDATE product_model3ds AS model
SET product_image_id = COALESCE(
    (SELECT image.id FROM product_images AS image
     WHERE image.product_id = model.product_id
       AND image.url = model.source_image_url AND NOT image.is_deleted
     ORDER BY image.sort_order, image.created_at LIMIT 1),
    (SELECT image.id FROM product_images AS image
     WHERE image.product_id = model.product_id AND NOT image.is_deleted
     ORDER BY image.sort_order, image.created_at LIMIT 1)
);

UPDATE model3d_requests AS request
SET product_image_id = COALESCE(
    (SELECT image.id FROM product_images AS image
     WHERE image.product_id = request.product_id
       AND image.id = ANY(request.source_image_ids) AND NOT image.is_deleted
     ORDER BY array_position(request.source_image_ids, image.id) LIMIT 1),
    (SELECT model.product_image_id FROM product_model3ds AS model
     WHERE model.product_id = request.product_id AND NOT model.is_deleted
     ORDER BY model.updated_at DESC LIMIT 1),
    (SELECT image.id FROM product_images AS image
     WHERE image.product_id = request.product_id AND NOT image.is_deleted
     ORDER BY image.sort_order, image.created_at LIMIT 1)
);

CREATE INDEX "IX_product_model3ds_product_id" ON product_model3ds (product_id);

CREATE UNIQUE INDEX "IX_product_model3ds_product_image_id" ON product_model3ds (product_image_id);

CREATE INDEX "IX_model3d_requests_product_image_id" ON model3d_requests (product_image_id);

ALTER TABLE model3d_requests ADD CONSTRAINT "FK_model3d_requests_product_images_product_image_id" FOREIGN KEY (product_image_id) REFERENCES product_images (id) ON DELETE SET NULL;

ALTER TABLE product_model3ds ADD CONSTRAINT "FK_product_model3ds_product_images_product_image_id" FOREIGN KEY (product_image_id) REFERENCES product_images (id) ON DELETE SET NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260802052139_AddProductImageModel3D', '8.0.10');

COMMIT;

START TRANSACTION;

UPDATE model3d_requests
SET status = CASE
        WHEN status = 'Queued' THEN 'AwaitingStaff'
        WHEN status = 'Processing' THEN 'InProgress'
        ELSE status
    END,
    next_attempt_at = NULL,
    updated_at = NOW()
WHERE request_type = 'Initial'
  AND status IN ('Queued', 'Processing')
  AND NOT is_deleted;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260802073311_NormalizeModel3DQueueFlow', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE recommendations ALTER COLUMN workspace_profile_id DROP NOT NULL;

ALTER TABLE recommendations ADD kind character varying(20) NOT NULL DEFAULT 'Workspace';

ALTER TABLE products ADD placement character varying(20) NOT NULL DEFAULT 'Desk';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260814122511_ProductPlacementAndPersonalRecommendation', '8.0.10');

COMMIT;

START TRANSACTION;

ALTER TABLE products DROP COLUMN size_class;

ALTER TABLE product_items ADD size_class character varying(10);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260814195923_MoveSizeClassToProductItem', '8.0.10');

COMMIT;

