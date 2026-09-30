-- =============================================================
-- FengDeskAI - Database schema (PostgreSQL)
--   dotnet ef dbcontext script -p src/FengDeskAI.Infrastructure -s src/FengDeskAI.WebAPI
-- Lastest migration: 20260923104220_PayoutCreditedAt
-- Content: 64 tables, PK/PF, index.
-- =============================================================

CREATE EXTENSION IF NOT EXISTS unaccent;

CREATE TABLE authorization_audit_logs (
    id uuid NOT NULL,
    actor_user_id uuid NOT NULL,
    action character varying(100) NOT NULL,
    resource_type character varying(100) NOT NULL,
    resource_id uuid,
    old_value_json jsonb,
    new_value_json jsonb,
    reason character varying(1000),
    ip_address character varying(64),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_authorization_audit_logs" PRIMARY KEY (id)
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


CREATE TABLE element_input_map (
    id uuid NOT NULL,
    input_kind character varying(10) NOT NULL,
    input_code character varying(30) NOT NULL,
    label_vi character varying(80),
    visibility character varying(10) NOT NULL DEFAULT 'Pending',
    element character varying(10) NOT NULL,
    weight numeric(4,3) NOT NULL DEFAULT 1.0,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_element_input_map" PRIMARY KEY (id)
);


CREATE TABLE elements (
    code character varying(10) NOT NULL,
    name character varying(100) NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    sort_order integer NOT NULL DEFAULT 0,
    CONSTRAINT "PK_elements" PRIMARY KEY (code)
);


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


CREATE TABLE garden_stores (
    id uuid NOT NULL,
    name character varying(255) NOT NULL,
    description text,
    hotline character varying(20) NOT NULL,
    opening_hours character varying(100),
    is_active boolean NOT NULL DEFAULT TRUE,
    ahamove_service_id character varying(50),
    ghn_shop_id integer,
    ghn_service_type_id integer NOT NULL DEFAULT 2,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_garden_stores" PRIMARY KEY (id)
);


CREATE TABLE occupations (
    id uuid NOT NULL,
    code character varying(30) NOT NULL,
    name_vi character varying(100) NOT NULL,
    description character varying(500),
    is_active boolean NOT NULL DEFAULT TRUE,
    is_system_seeded boolean NOT NULL DEFAULT FALSE,
    sort_order integer NOT NULL DEFAULT 0,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_occupations" PRIMARY KEY (id)
);


CREATE TABLE provinces (
    id uuid NOT NULL,
    name character varying(255) NOT NULL,
    code integer NOT NULL,
    ghn_province_id integer,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_provinces" PRIMARY KEY (id)
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


CREATE TABLE styles (
    code character varying(30) NOT NULL,
    name character varying(100) NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    sort_order integer NOT NULL DEFAULT 0,
    CONSTRAINT "PK_styles" PRIMARY KEY (code)
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


CREATE TABLE vibes (
    code character varying(20) NOT NULL,
    name character varying(100) NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    sort_order integer NOT NULL DEFAULT 0,
    CONSTRAINT "PK_vibes" PRIMARY KEY (code)
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


CREATE TABLE workspace_types (
    id uuid NOT NULL,
    name character varying(100) NOT NULL,
    description character varying(500),
    is_public boolean NOT NULL DEFAULT FALSE,
    personal_weight numeric(4,2) NOT NULL DEFAULT 1.0,
    scope character varying(20) NOT NULL DEFAULT 'Private',
    is_system_seeded boolean NOT NULL DEFAULT FALSE,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_workspace_types" PRIMARY KEY (id)
);


CREATE TABLE products (
    id uuid NOT NULL,
    garden_store_id uuid NOT NULL,
    name character varying(255) NOT NULL,
    description text,
    is_active boolean NOT NULL DEFAULT TRUE,
    placement character varying(20) NOT NULL DEFAULT 'Desk',
    element_tho numeric(4,3),
    element_kim numeric(4,3),
    element_thuy numeric(4,3),
    element_moc numeric(4,3),
    element_hoa numeric(4,3),
    is_vector_overridden boolean NOT NULL DEFAULT FALSE,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_products" PRIMARY KEY (id),
    CONSTRAINT "FK_products_garden_stores_garden_store_id" FOREIGN KEY (garden_store_id) REFERENCES garden_stores (id) ON DELETE CASCADE
);


CREATE TABLE occupation_element_profiles (
    id uuid NOT NULL,
    occupation_id uuid NOT NULL,
    element character varying(10) NOT NULL,
    share numeric(4,3) NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_occupation_element_profiles" PRIMARY KEY (id),
    CONSTRAINT "CK_occupation_element_profiles_share" CHECK (share >= 0 AND share <= 1),
    CONSTRAINT "FK_occupation_element_profiles_occupations_occupation_id" FOREIGN KEY (occupation_id) REFERENCES occupations (id) ON DELETE CASCADE
);


CREATE TABLE users (
    id uuid NOT NULL,
    email character varying(255) NOT NULL,
    password_hash text,
    full_name character varying(255) NOT NULL,
    date_of_birth timestamp with time zone,
    birth_time time without time zone,
    gender integer NOT NULL,
    phone character varying(20),
    role integer NOT NULL,
    balance numeric(12,3) NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    token_version integer NOT NULL DEFAULT 0,
    auth_provider integer NOT NULL DEFAULT 0,
    google_id character varying(255),
    occupation_id uuid,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_users" PRIMARY KEY (id),
    CONSTRAINT "FK_users_occupations_occupation_id" FOREIGN KEY (occupation_id) REFERENCES occupations (id) ON DELETE RESTRICT
);


CREATE TABLE districts (
    id uuid NOT NULL,
    province_id uuid NOT NULL,
    name character varying(255) NOT NULL,
    code integer NOT NULL,
    ghn_district_id integer,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_districts" PRIMARY KEY (id),
    CONSTRAINT "FK_districts_provinces_province_id" FOREIGN KEY (province_id) REFERENCES provinces (id) ON DELETE CASCADE
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


CREATE TABLE product_aspirations (
    product_id uuid NOT NULL,
    aspiration character varying(20) NOT NULL,
    is_approved boolean NOT NULL DEFAULT FALSE,
    approved_by uuid,
    approved_at timestamp with time zone,
    CONSTRAINT "PK_product_aspirations" PRIMARY KEY (product_id, aspiration),
    CONSTRAINT "FK_product_aspirations_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE
);


CREATE TABLE product_categories (
    product_id uuid NOT NULL,
    category_id uuid NOT NULL,
    CONSTRAINT "PK_product_categories" PRIMARY KEY (product_id, category_id),
    CONSTRAINT "FK_product_categories_categories_category_id" FOREIGN KEY (category_id) REFERENCES categories (id) ON DELETE CASCADE,
    CONSTRAINT "FK_product_categories_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE
);


CREATE TABLE product_element (
    product_id uuid NOT NULL,
    element character varying(10) NOT NULL,
    is_primary boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_product_element" PRIMARY KEY (product_id, element),
    CONSTRAINT "FK_product_element_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE
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
    size_class character varying(10),
    weight_gram integer NOT NULL DEFAULT 500,
    length_cm integer NOT NULL DEFAULT 10,
    width_cm integer NOT NULL DEFAULT 10,
    height_cm integer NOT NULL DEFAULT 10,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_product_items" PRIMARY KEY (id),
    CONSTRAINT "FK_product_items_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE
);


CREATE TABLE product_styles (
    product_id uuid NOT NULL,
    style_code character varying(30) NOT NULL,
    CONSTRAINT "PK_product_styles" PRIMARY KEY (product_id, style_code),
    CONSTRAINT "FK_product_styles_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE,
    CONSTRAINT "FK_product_styles_styles_style_code" FOREIGN KEY (style_code) REFERENCES styles (code) ON DELETE RESTRICT
);


CREATE TABLE product_tags (
    product_id uuid NOT NULL,
    tag_id uuid NOT NULL,
    CONSTRAINT "PK_product_tags" PRIMARY KEY (product_id, tag_id),
    CONSTRAINT "FK_product_tags_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE,
    CONSTRAINT "FK_product_tags_tags_tag_id" FOREIGN KEY (tag_id) REFERENCES tags (id) ON DELETE CASCADE
);


CREATE TABLE product_vibes (
    product_id uuid NOT NULL,
    vibe_code character varying(20) NOT NULL,
    CONSTRAINT "PK_product_vibes" PRIMARY KEY (product_id, vibe_code),
    CONSTRAINT "FK_product_vibes_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE,
    CONSTRAINT "FK_product_vibes_vibes_vibe_code" FOREIGN KEY (vibe_code) REFERENCES vibes (code) ON DELETE RESTRICT
);


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


CREATE TABLE chatboxes (
    id uuid NOT NULL,
    is_group boolean NOT NULL DEFAULT TRUE,
    title character varying(200),
    created_by_user_id uuid NOT NULL,
    product_id uuid,
    is_ai_enabled boolean NOT NULL DEFAULT FALSE,
    is_support boolean NOT NULL DEFAULT FALSE,
    garden_store_id uuid,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_chatboxes" PRIMARY KEY (id),
    CONSTRAINT "FK_chatboxes_garden_stores_garden_store_id" FOREIGN KEY (garden_store_id) REFERENCES garden_stores (id) ON DELETE SET NULL,
    CONSTRAINT "FK_chatboxes_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE SET NULL,
    CONSTRAINT "FK_chatboxes_users_created_by_user_id" FOREIGN KEY (created_by_user_id) REFERENCES users (id) ON DELETE CASCADE
);


CREATE TABLE garden_staff_assignments (
    id uuid NOT NULL,
    garden_store_id uuid NOT NULL,
    staff_id uuid NOT NULL,
    invited_by uuid NOT NULL,
    status character varying(20) NOT NULL DEFAULT 'Pending',
    invited_at timestamp with time zone NOT NULL,
    responded_at timestamp with time zone,
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


CREATE TABLE notifications (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    type character varying(30) NOT NULL,
    title character varying(200) NOT NULL,
    message character varying(1000) NOT NULL,
    is_read boolean NOT NULL DEFAULT FALSE,
    read_at timestamp with time zone,
    reference_id uuid,
    reference_type character varying(30),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_notifications" PRIMARY KEY (id),
    CONSTRAINT "FK_notifications_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
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


CREATE TABLE workspace_profiles (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    name character varying(100) NOT NULL,
    location_type character varying(30) NOT NULL,
    workspace_type_id uuid,
    style_code character varying(30) NOT NULL,
    lighting character varying(30),
    desk_type character varying(30),
    desk_orientation character varying(15),
    room_facing_direction character varying(15),
    work_purpose character varying(30) NOT NULL,
    feng_shui_element character varying(10),
    desk_area integer,
    entrance_direction character varying(15),
    toilet_direction character varying(15),
    dark_directions jsonb NOT NULL DEFAULT ('[]'::jsonb),
    is_default boolean NOT NULL DEFAULT FALSE,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_workspace_profiles" PRIMARY KEY (id),
    CONSTRAINT "FK_workspace_profiles_styles_style_code" FOREIGN KEY (style_code) REFERENCES styles (code) ON DELETE RESTRICT,
    CONSTRAINT "FK_workspace_profiles_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
    CONSTRAINT "FK_workspace_profiles_workspace_types_workspace_type_id" FOREIGN KEY (workspace_type_id) REFERENCES workspace_types (id) ON DELETE SET NULL
);


CREATE TABLE wards (
    id uuid NOT NULL,
    district_id uuid NOT NULL,
    name character varying(255) NOT NULL,
    code integer NOT NULL,
    ghn_ward_code character varying(20),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_wards" PRIMARY KEY (id),
    CONSTRAINT "FK_wards_districts_district_id" FOREIGN KEY (district_id) REFERENCES districts (id) ON DELETE CASCADE
);


CREATE TABLE model3d_requests (
    id uuid NOT NULL,
    product_id uuid NOT NULL,
    product_image_id uuid,
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
    CONSTRAINT "FK_model3d_requests_product_images_product_image_id" FOREIGN KEY (product_image_id) REFERENCES product_images (id) ON DELETE SET NULL,
    CONSTRAINT "FK_model3d_requests_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE
);


CREATE TABLE product_model3ds (
    id uuid NOT NULL,
    product_id uuid NOT NULL,
    product_image_id uuid,
    status text NOT NULL,
    source_image_url text NOT NULL,
    meshy_task_id text,
    model_url text,
    thumbnail_url text,
    progress integer NOT NULL DEFAULT 0,
    error_message text,
    is_enabled boolean NOT NULL DEFAULT TRUE,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_product_model3ds" PRIMARY KEY (id),
    CONSTRAINT "FK_product_model3ds_product_images_product_image_id" FOREIGN KEY (product_image_id) REFERENCES product_images (id) ON DELETE SET NULL,
    CONSTRAINT "FK_product_model3ds_products_product_id" FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE
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


CREATE TABLE chat_messages (
    id uuid NOT NULL,
    chatbox_id uuid NOT NULL,
    sender_id uuid,
    sender_type character varying(20) NOT NULL,
    sender_name character varying(100),
    content character varying(5000),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_chat_messages" PRIMARY KEY (id),
    CONSTRAINT "FK_chat_messages_chatboxes_chatbox_id" FOREIGN KEY (chatbox_id) REFERENCES chatboxes (id) ON DELETE CASCADE,
    CONSTRAINT "FK_chat_messages_users_sender_id" FOREIGN KEY (sender_id) REFERENCES users (id) ON DELETE SET NULL
);


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


CREATE TABLE recommendations (
    id uuid NOT NULL,
    user_id uuid NOT NULL,
    workspace_profile_id uuid,
    kind character varying(20) NOT NULL DEFAULT 'Workspace',
    workspace_type_id uuid,
    customer_element character varying(10),
    kua_number integer,
    kua_group character varying(10),
    personal_weight numeric(4,2) NOT NULL,
    formula_version character varying(10) NOT NULL DEFAULT '3.1',
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


CREATE TABLE stores_address (
    id uuid NOT NULL,
    store_id uuid NOT NULL,
    ward_id uuid NOT NULL,
    street_address character varying(255) NOT NULL,
    latitude numeric(10,8),
    longitude numeric(11,8),
    is_active boolean NOT NULL DEFAULT TRUE,
    sender_name character varying(100),
    sender_phone character varying(20),
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_stores_address" PRIMARY KEY (id),
    CONSTRAINT "FK_stores_address_garden_stores_store_id" FOREIGN KEY (store_id) REFERENCES garden_stores (id) ON DELETE CASCADE,
    CONSTRAINT "FK_stores_address_wards_ward_id" FOREIGN KEY (ward_id) REFERENCES wards (id) ON DELETE RESTRICT
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


CREATE TABLE orders (
    id uuid NOT NULL,
    customer_id uuid NOT NULL,
    shipping_address_id uuid NOT NULL,
    status character varying(50) NOT NULL,
    payment_method character varying(30) NOT NULL DEFAULT 'PayOS',
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


CREATE TABLE deliveries (
    id uuid NOT NULL,
    order_id uuid NOT NULL,
    garden_store_id uuid NOT NULL,
    assigned_staff_id uuid,
    status character varying(50) NOT NULL,
    tracking_code character varying(100),
    provider_order_id character varying(100),
    shipping_provider character varying(50),
    tracking_url character varying(500),
    shipping_fee numeric(12,2) NOT NULL,
    subtotal numeric(12,2) NOT NULL,
    is_exchange boolean NOT NULL DEFAULT FALSE,
    assigned_at timestamp with time zone,
    shipped_at timestamp with time zone,
    delivered_at timestamp with time zone,
    payout_credited_at timestamp with time zone,
    estimated_delivery_date timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    created_by uuid,
    updated_by uuid,
    is_deleted boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_deliveries" PRIMARY KEY (id),
    CONSTRAINT "FK_deliveries_garden_stores_garden_store_id" FOREIGN KEY (garden_store_id) REFERENCES garden_stores (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_deliveries_orders_order_id" FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE CASCADE,
    CONSTRAINT "FK_deliveries_users_assigned_staff_id" FOREIGN KEY (assigned_staff_id) REFERENCES users (id) ON DELETE SET NULL
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


CREATE TABLE order_items (
    id uuid NOT NULL,
    order_id uuid NOT NULL,
    product_item_id uuid NOT NULL,
    delivery_id uuid,
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
    vendor_response character varying(20) NOT NULL DEFAULT 'Pending',
    vendor_response_deadline timestamp with time zone,
    evidence_deadline timestamp with time zone,
    decided_by uuid,
    decided_at timestamp with time zone,
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


CREATE TABLE refunds (
    id uuid NOT NULL,
    return_request_id uuid NOT NULL,
    order_id uuid NOT NULL,
    transaction_id uuid,
    amount numeric(12,2) NOT NULL,
    method character varying(20) NOT NULL,
    status character varying(20) NOT NULL,
    gateway character varying(30) NOT NULL DEFAULT 'payos',
    provider_refund_id character varying(100),
    idempotency_key character varying(120) NOT NULL,
    retry_count integer NOT NULL DEFAULT 0,
    is_manual boolean NOT NULL DEFAULT FALSE,
    manual_reason text,
    evidence_url character varying(500),
    performed_by uuid,
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


CREATE INDEX "IX_authorization_audit_logs_actor_user_id" ON authorization_audit_logs (actor_user_id);


CREATE INDEX "IX_authorization_audit_logs_created_at" ON authorization_audit_logs (created_at);


CREATE INDEX "IX_authorization_audit_logs_resource_type_resource_id" ON authorization_audit_logs (resource_type, resource_id);


CREATE INDEX "IX_cart_items_product_item_id" ON cart_items (product_item_id);


CREATE UNIQUE INDEX "UX_cart_items_cart_product_item" ON cart_items (cart_id, product_item_id) WHERE is_deleted = FALSE;


CREATE UNIQUE INDEX "IX_carts_customer_id" ON carts (customer_id);


CREATE INDEX "IX_categories_parent_id" ON categories (parent_id);


CREATE INDEX "IX_chat_message_images_chat_message_id" ON chat_message_images (chat_message_id);


CREATE INDEX "IX_chat_messages_chatbox_id_created_at" ON chat_messages (chatbox_id, created_at);


CREATE INDEX "IX_chat_messages_sender_id" ON chat_messages (sender_id);


CREATE UNIQUE INDEX "IX_chat_room_data_consents_chatbox_id_granter_user_id" ON chat_room_data_consents (chatbox_id, granter_user_id);


CREATE UNIQUE INDEX "IX_chatbox_participants_chatbox_id_user_id" ON chatbox_participants (chatbox_id, user_id);


CREATE INDEX "IX_chatbox_participants_user_id" ON chatbox_participants (user_id);


CREATE INDEX "IX_chatboxes_created_by_user_id" ON chatboxes (created_by_user_id);


CREATE INDEX "IX_chatboxes_garden_store_id" ON chatboxes (garden_store_id);


CREATE INDEX "IX_chatboxes_product_id" ON chatboxes (product_id);


CREATE INDEX "IX_deliveries_assigned_staff_id" ON deliveries (assigned_staff_id);


CREATE INDEX "IX_deliveries_garden_store_id" ON deliveries (garden_store_id);


CREATE INDEX "IX_deliveries_order_id" ON deliveries (order_id);


CREATE INDEX ix_deliveries_payout_scan ON deliveries (status, payout_credited_at, delivered_at);


CREATE INDEX "IX_delivery_progress_logs_delivery_id" ON delivery_progress_logs (delivery_id);


CREATE UNIQUE INDEX "IX_districts_code" ON districts (code);


CREATE INDEX "IX_districts_province_id" ON districts (province_id);


CREATE UNIQUE INDEX "IX_element_input_map_input_kind_input_code_element" ON element_input_map (input_kind, input_code, element) WHERE is_deleted = false;


CREATE INDEX "IX_element_input_map_visibility_created_by" ON element_input_map (visibility, created_by) WHERE is_deleted = false;


CREATE UNIQUE INDEX "IX_feng_shui_rules_subject_element_object_element" ON feng_shui_rules (subject_element, object_element) WHERE is_deleted = FALSE;


CREATE INDEX "IX_garden_staff_assignments_staff_id" ON garden_staff_assignments (staff_id);


CREATE UNIQUE INDEX "UX_garden_staff_active" ON garden_staff_assignments (garden_store_id, staff_id) WHERE status IN ('Pending', 'Accepted') AND is_deleted = FALSE;


CREATE UNIQUE INDEX "IX_garden_store_owners_garden_store_id_owner_user_id" ON garden_store_owners (garden_store_id, owner_user_id);


CREATE INDEX "IX_garden_store_owners_owner_user_id" ON garden_store_owners (owner_user_id);


CREATE INDEX "IX_model3d_requests_product_id" ON model3d_requests (product_id);


CREATE INDEX "IX_model3d_requests_product_image_id" ON model3d_requests (product_image_id);


CREATE INDEX "IX_model3d_requests_status" ON model3d_requests (status);


CREATE INDEX "IX_notifications_user_id_created_at" ON notifications (user_id, created_at);


CREATE INDEX "IX_notifications_user_id_is_read" ON notifications (user_id, is_read);


CREATE UNIQUE INDEX "IX_occupation_element_profiles_occupation_id_element" ON occupation_element_profiles (occupation_id, element) WHERE is_deleted = false;


CREATE UNIQUE INDEX "IX_occupations_code" ON occupations (code) WHERE is_deleted = false;


CREATE INDEX "IX_order_items_delivery_id" ON order_items (delivery_id);


CREATE INDEX "IX_order_items_order_id" ON order_items (order_id);


CREATE INDEX "IX_order_items_product_item_id" ON order_items (product_item_id);


CREATE INDEX "IX_order_status_log_order_id" ON order_status_log (order_id);


CREATE INDEX "IX_orders_customer_id" ON orders (customer_id);


CREATE INDEX "IX_orders_shipping_address_id" ON orders (shipping_address_id);


CREATE INDEX "IX_product_aspirations_aspiration" ON product_aspirations (aspiration) WHERE is_approved;


CREATE INDEX "IX_product_categories_category_id" ON product_categories (category_id);


CREATE INDEX "IX_product_element_product_id_is_primary" ON product_element (product_id, is_primary);


CREATE INDEX "IX_product_element_inputs_product_id" ON product_element_inputs (product_id);


CREATE INDEX "IX_product_images_product_id" ON product_images (product_id);


CREATE INDEX "IX_product_items_product_id" ON product_items (product_id);


CREATE UNIQUE INDEX "IX_product_items_sku" ON product_items (sku) WHERE sku IS NOT NULL AND is_deleted = FALSE;


CREATE INDEX "IX_product_model3ds_product_id" ON product_model3ds (product_id);


CREATE UNIQUE INDEX "IX_product_model3ds_product_image_id" ON product_model3ds (product_image_id);


CREATE INDEX "IX_product_styles_style_code" ON product_styles (style_code);


CREATE INDEX "IX_product_tags_tag_id" ON product_tags (tag_id);


CREATE INDEX "IX_product_vibes_vibe_code" ON product_vibes (vibe_code);


CREATE INDEX "IX_products_garden_store_id" ON products (garden_store_id);


CREATE UNIQUE INDEX "IX_provinces_code" ON provinces (code);


CREATE INDEX "IX_recommendation_items_product_id" ON recommendation_items (product_id);


CREATE INDEX "IX_recommendation_items_recommendation_id" ON recommendation_items (recommendation_id);


CREATE INDEX "IX_recommendation_logs_recommendation_id" ON recommendation_logs (recommendation_id);


CREATE INDEX "IX_recommendations_user_id" ON recommendations (user_id);


CREATE INDEX "IX_recommendations_workspace_profile_id" ON recommendations (workspace_profile_id);


CREATE INDEX "IX_recommendations_workspace_type_id" ON recommendations (workspace_type_id);


CREATE UNIQUE INDEX "IX_refresh_tokens_token" ON refresh_tokens (token);


CREATE INDEX "IX_refresh_tokens_user_id_is_revoked" ON refresh_tokens (user_id, is_revoked);


CREATE UNIQUE INDEX "IX_refunds_idempotency_key" ON refunds (idempotency_key);


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


CREATE INDEX "IX_reviews_product_id" ON reviews (product_id);


CREATE UNIQUE INDEX "UX_reviews_user_product" ON reviews (user_id, product_id) WHERE is_deleted = FALSE;


CREATE UNIQUE INDEX "IX_scoring_params_code" ON scoring_params (code) WHERE is_deleted = false;


CREATE INDEX "IX_shipping_webhook_is_processed" ON shipping_webhook (is_processed);


CREATE UNIQUE INDEX "IX_stores_address_store_id" ON stores_address (store_id);


CREATE INDEX "IX_stores_address_ward_id" ON stores_address (ward_id);


CREATE UNIQUE INDEX "IX_tags_name" ON tags (name) WHERE is_deleted = FALSE;


CREATE UNIQUE INDEX "IX_transaction_order_code" ON transaction (order_code);


CREATE INDEX "IX_transaction_order_id" ON transaction (order_id);


CREATE INDEX "IX_user_address_ward_id" ON user_address (ward_id);


CREATE UNIQUE INDEX "UX_user_address_user_default" ON user_address (user_id) WHERE is_default = TRUE AND is_deleted = FALSE;


CREATE UNIQUE INDEX "IX_users_email" ON users (email);


CREATE UNIQUE INDEX "IX_users_google_id" ON users (google_id) WHERE google_id IS NOT NULL;


CREATE INDEX "IX_users_occupation_id" ON users (occupation_id);


CREATE UNIQUE INDEX "IX_users_phone" ON users (phone) WHERE phone IS NOT NULL;


CREATE INDEX "IX_vendor_liabilities_garden_id" ON vendor_liabilities (garden_id);


CREATE INDEX "IX_vendor_liabilities_refund_id" ON vendor_liabilities (refund_id);


CREATE INDEX "IX_vendor_liabilities_status" ON vendor_liabilities (status);


CREATE UNIQUE INDEX "IX_vendor_liabilities_ticket_id" ON vendor_liabilities (ticket_id);


CREATE UNIQUE INDEX "IX_wards_code" ON wards (code);


CREATE INDEX "IX_wards_district_id" ON wards (district_id);


CREATE UNIQUE INDEX "IX_work_purpose_element_modifiers_work_purpose_element" ON work_purpose_element_modifiers (work_purpose, element) WHERE is_deleted = false;


CREATE UNIQUE INDEX "IX_workspace_product_placements_order_item_id" ON workspace_product_placements (order_item_id) WHERE is_deleted = FALSE;


CREATE INDEX "IX_workspace_product_placements_product_id" ON workspace_product_placements (product_id);


CREATE INDEX "IX_workspace_product_placements_workspace_profile_id_user_id" ON workspace_product_placements (workspace_profile_id, user_id);


CREATE INDEX "IX_workspace_profile_inputs_workspace_profile_id" ON workspace_profile_inputs (workspace_profile_id);


CREATE INDEX "IX_workspace_profiles_style_code" ON workspace_profiles (style_code);


CREATE INDEX "IX_workspace_profiles_workspace_type_id" ON workspace_profiles (workspace_type_id);


CREATE UNIQUE INDEX "UX_workspace_profiles_user_default" ON workspace_profiles (user_id) WHERE is_default = TRUE AND is_deleted = FALSE;


CREATE UNIQUE INDEX "IX_workspace_type_elements_workspace_type_id_source_element" ON workspace_type_elements (workspace_type_id, source, element) WHERE is_deleted = false;


CREATE INDEX "IX_workspace_types_name" ON workspace_types (name);


