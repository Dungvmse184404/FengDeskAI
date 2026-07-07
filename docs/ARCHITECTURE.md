# SEP490 · FengDeskAI — System Architecture

> **Scope:** Kiến trúc tổng thể của toàn workspace `SEP490` (không phải của riêng repo con). Tập trung vào *quan hệ giữa các thành phần* và *nơi tìm cái gì* — chi tiết layer từng repo xem `FengDeskAI/CLAUDE.md`, `FengDeskAI/docs/PROJECT-GUIDE.md`, `FengDeskAI_FE/README.md`.
>
> Mã đề tài `SU26SE093` (Capstone SU26). Domain: **e-commerce phong thủy bàn làm việc + gợi ý bằng AI**.

---

## 1. Repo topology

```
D:\Projects\SEP490\
├── FengDeskAI\            # Backend monolith — .NET 8 / EF Core / PostgreSQL
├── FengDeskAI_FE\         # Frontend web app — React 19 + Vite + TS
├── Documents\             # Deliverables trường (Review1/Review2, ERD, checklists)
├── Harness\               # Dev-side tooling (docs + agent scripts, không ship)
├── .codegraph\            # SQLite index cho code intelligence (dev-only)
├── .obsidian\             # Note vault (dev-only)
├── AGENTS.md              # Rules cho AI assistants working here
├── CONTRIBUTING.md        # Branching / commit convention (Conventional Commits)
└── ARCHITECTURE.md        # (file này)
```

**AI recommendation microservice** (Python FastAPI) đã có DTO contract sẵn ở `FengDeskAI.Contracts` nhưng **service Python chưa tồn tại** — hiện dùng .NET-in-process scorer.

---

## 2. System context (C4-L1)

```
                ┌──────────────────────────────────────────┐
                │  END USER (Customer / Garden Owner /     │
                │  Garden Staff / Platform Staff / Admin)  │
                └────────────────────┬─────────────────────┘
                                     │  HTTPS
                                     ▼
                        ┌───────────────────────────┐
                        │  FengDeskAI_FE            │
                        │  (SPA, Vercel/browser)    │
                        └────────────────┬──────────┘
                                         │ REST + SignalR/hubs/chat
                                         ▼
                        ┌────────────────────────────────────┐
                        │  FengDeskAI.WebAPI  (:8080)        │
                        │  ASP.NET Core 8 · Clean Arch       │
                        └─┬─────────┬────────┬─────────┬─────┘
                          │         │        │         │
                          ▼         ▼        ▼         ▼
              ┌─────────────┐  ┌────────┐ ┌────────┐ ┌─────────────────┐
              │ PostgreSQL  │  │ SMTP   │ │ PayOS  │ │ Supabase        │
              │ (Supabase)  │  │ OTP    │ │ COD    │ │ Storage (bucket)│
              └─────────────┘  └────────┘ └────────┘ └─────────────────┘
                                                      ▲
                                                      │  Product/Chat images
                                                      │
              ┌─────────────┐  ┌──────────────────────┴─┐
              │ Ollama LLM  │  │ Meshy (image → 3D)     │
              │ chat + tool │  │     mocked hiện tại    │
              └─────────────┘  └────────────────────────┘
                    ▲
                    │
              ┌─────┴─────────────────────────────────┐
              │ AI Recommendation microservice        │
              │ (Python FastAPI                       │
              │  hiện scorer chạy trong-process .NET) │
              └───────────────────────────────────────┘
```

**Actor roles** (không có role global `GardenStaff` — xem §7):

| Actor | Cách nhận biết |
|---|---|
| Customer | mọi user đăng ký (flag `UserRole.Customer`) |
| Garden Owner | flag `UserRole.GardenOwner`, cấp tự động khi tạo store đầu tiên |
| Garden Staff (per-store) | có row `garden_staff_assignments` với `status = Accepted` |
| Platform Staff / Manager / Admin | flags `UserRole.Staff / Manager / Admin` |

---

## 3. Backend (.NET) — Clean Architecture

```
Domain  ←  Application  ←  Infrastructure  ←  WebAPI
                        ↖
                        Contracts
                        (share DTO với AI microservice)
```

### 3.1 Projects

| Project | Vai trò | Không được reference |
|---|---|---|
| `FengDeskAI.Domain` | POCO entities (kế thừa `BaseEntity`), enums, value objects — logic thuần | mọi project khác trong solution |
| `FengDeskAI.Application` | Feature theo bounded context: `DTOs/`, `Mappings/`, `Services/`. Interfaces cho repo/UoW/external. Feng-shui engine deterministic | `Microsoft.AspNetCore.*`, `Microsoft.EntityFrameworkCore` |
| `FengDeskAI.Infrastructure` | EF Core config + repos + `UnitOfWork`, tích hợp PayOS / Supabase Storage / Ollama / Meshy / SMTP, migrations | — |
| `FengDeskAI.WebAPI` | Controllers thin, SignalR hub, background workers, authorization, DI bootstrap | — |
| `FengDeskAI.Contracts` | DTO chia sẻ với AI recommendation service | — |

### 3.2 Bounded contexts (`Application/Features/<X>/`)

| Context | Trách nhiệm | Tables chính |
|---|---|---|
| **Identity** | Register 3-bước (Initiate/Verify/Finalize), login, refresh, JWT, user search | `users`, `refresh_tokens` |
| **Workspace** | Workspace profile (input phong thủy) | `workspace_profiles` |
| **Geography** | Provinces/Districts/Wards, user address book | `provinces`, `districts`, `wards`, `user_address` |
| **Vendor** | Multi-store, owners, staff invitation flow | `garden_stores`, `garden_store_owners`, `garden_staff_assignments`, `stores_address` |
| **Catalog** | Products, items, images, categories, tags, elements, styles, vibes | `products`, `product_items`, `product_images`, `categories`, `tags`, junction tables |
| **Sales** | Cart → Order (multi-store) → Deliveries | `carts`, `orders`, `order_items`, `deliveries`, `order_status_log` |
| **Payment** | PayOS gateway + COD, transaction ledger | `transactions`, `orders.payment_status` |
| **Shipping** | Webhook ingest, tracking, phí ship (calculator + estimator) | `delivery_progress_logs`, `shipping_webhook` |
| **Returns** | Yêu cầu trả hàng / hoàn tiền + ảnh bằng chứng | `returns`, `refunds` |
| **Chat** | Người↔người + người↔AI (SignalR), consent gating | `chatboxes`, `chat_messages`, `chat_message_images` |
| **CustomerCare** | Recommendation engine + AI tools (function calling) | `recommendations`, `recommendation_items`, `feng_shui_rules` |
| **Announcement** | Notifications (deep-linkable qua `ReferenceType`) | `notifications` |
| **Storage** | Upload ảnh qua Supabase Storage | (không table riêng) |

### 3.3 Patterns

- **KHÔNG dùng MediatR/CQRS.** Mỗi feature có `Services/I<X>Service.cs` + `<X>Service.cs`. Controllers thin: gọi service → `ToActionResult`.
- **Repository + UnitOfWork.** Interfaces ở `Application/Interfaces/Repositories/`, impl ở `Infrastructure/Persistence/Repositories/`. Commit qua `IUnitOfWork.SaveChangesAsync(ct)`. Cho transaction nhiều bước dùng `ExecuteInTransactionAsync`.
- **Result pattern.** Service trả `IServiceResult<T>` / `IServiceResult` (`Application/Common/Results/`). Business error dùng `Failure(statusCode, message)`, **không throw**.
- **EF Fluent API isolated.** 1 entity = 1 `Infrastructure/Persistence/Configurations/<X>Configuration.cs`. Domain KHÔNG có attribute EF.
- **Soft-delete via interceptor.** `AppDbContext.SaveChanges` chuyển `Remove()` thành `IsDeleted=true`. Query filter `HasQueryFilter(x => !x.IsDeleted)` lọc mặc định; `IgnoreQueryFilters()` khi cần include soft-deleted.
- **Audit auto-fill.** Interceptor set `CreatedAt/UpdatedAt/CreatedBy/UpdatedBy` từ `ICurrentUserService`.
- **Enum stored as string.** `HasConversion<string>().HasMaxLength(N)` — DB human-readable. Không đổi tên enum mà không migrate data.
- **Diacritic-insensitive search.** PostgreSQL `unaccent` extension (đã bật) + `AppDbContext.Unaccent()` DB function. Cho `đ→d` phải xử lý C#-side (`ProductRepository.SearchAsync`, `UserRepository.SearchAsync`).

### 3.4 Cross-cutting

- **Authorization policies** (`WebAPI/Authorization/AuthorizationPolicies.cs`): `AdminOnly`, `StaffOrAbove`, `ManagerOrAbove`, `CustomerOnly`, `GardenOwnerOrAbove`. Store-scoped ownership check ở service layer (`StoreService.IsOwnerOrAdminAsync`).
- **Background workers** (`WebAPI/Workers/`): `AiBotWorker` (queue + Ollama call), `Model3DPollingWorker` (poll Meshy), `OrderExpirationWorker` (đơn online chưa thanh toán quá hạn → cancel).
- **Realtime**: SignalR hub `/hubs/chat` cho chat. **Chưa có** notification hub — badge unread hiện qua polling 60s (Redux slice + interval).
- **Exception → HTTP.** Global filter map `UnauthorizedAccessException` → `ServiceResult.Failure(401)`.

---

## 4. Frontend (React 19)

### 4.1 Stack

React 19 + TypeScript + Vite 8 · Redux Toolkit + TanStack Query 5 · React Router 7 · Tailwind 4 · axios + custom `httpClient` với refresh-token queue · react-hook-form + zod · SignalR client · Framer Motion · sonner (toast).

### 4.2 Cấu trúc

```
src/
├── main.tsx  →  App.tsx  →  AppProviders (Redux + QueryClient + Router + SearchProvider)
├── app/
│   ├── router.tsx         # route table + <ProtectedRoute>
│   ├── ProtectedRoute.tsx # gate theo role (Customer/Staff/GardenOwner…)
│   ├── store.ts           # Redux store gộp các slice
│   └── query-client.ts    # TanStack QueryClient
├── components/{ui, layouts}/  # dùng chung: Modal, Button, ProfileLayout, AppLayout, ManagerLayout
├── features/<context>/
│   ├── api/*.api.ts       # gọi httpClient — KHÔNG gọi API trong component
│   ├── hooks/             # TanStack Query hooks (useX/useMutation)
│   ├── store/             # Redux slice (dùng cho state global: auth, cart, notification…)
│   ├── pages/             # màn hình cấp route
│   ├── components/        # component riêng feature
│   ├── types/*.d.ts       # DTO khớp BE
│   └── schemas/*.ts       # zod cho form
└── lib/httpClient.ts      # axios + refresh-token rotation + interceptors
```

**Features hiện có:** `auth`, `cart`, `category`, `chatbox`, `home`, `manager` (admin/platform staff), `notification`, `orders`, `payment`, `products`, `recommendation`, `return`, `review`, `search`, `shared`, `shop` (kênh người bán + invitation flow), `users`.

### 4.3 State management split

| Thư viện | Dùng cho |
|---|---|
| **Redux Toolkit** | Global reactive state cần hiện ở nhiều nơi cùng lúc: `auth` (token/user), `cart`, `notification` (list + unreadCount + polling). |
| **TanStack Query** | Server cache theo query key: `["shop-staff", storeId]`, `["my-store-invitations"]`, `["addresses"]`, `["products", filter]`… Mutation dùng `invalidateQueries` để đồng bộ. |
| **Component state** | Modal open/close, form draft, dropdown highlight. |

**Rule** (theo `AGENTS.md`): **không fetch API trực tiếp trong UI component**, luôn qua `api/*.ts` + hook.

### 4.4 Routing + guards

`app/router.tsx` map path → page với wrapper `<ProtectedRoute requireGardenOwner|requireStaffOrAbove|…>`. Guard chỉ check flag từ Redux `auth.user.role`; ownership per-store để BE trả 403 và toast từ `ServiceResult.message`.

---

## 5. Data flow tiêu biểu

### 5.1 Register / Login

```
FE  Initiate {email}  ─►  BE   send OTP qua MailKit/SMTP
                       ←    { message: "OTP sent" }
FE  Verify {email, otp} ─► BE  check OTP (IDistributedCache, timing-safe)
                       ←    { registrationToken, expiresAt }
FE  Finalize {token, fullName, password, gender, dob, phone} ─►
    BE  BCrypt hash → INSERT users → issue { accessToken, refreshToken, user }
```

Access token 60′, refresh 14 ngày, rotation mỗi lần refresh (`httpClient` giữ queue lúc đang refresh).

### 5.2 Recommendation

1. FE gửi `workspace_profile_id` + filter → `POST /api/recommendations`.
2. BE `RecommendationScorer` (deterministic engine ở `Application/Features/CustomerCare/Engine/`) load candidate products → chấm điểm phong thủy → rank.
3. Tùy chọn: rerank/giải thích bằng Ollama qua `AiChatService` (chỉ giải thích, **KHÔNG** đổi rank).
4. Trả `RecommendationResponse` + log `recommendation_logs`.

Engine **là source of truth về scoring** — không được để AI tự bịa rule.

### 5.3 Chat (người ↔ AI + người ↔ người)

- SignalR hub `/hubs/chat` gửi message realtime.
- Người ↔ AI: `AiBotQueue` enqueue → `AiBotWorker` gọi Ollama (`AiChat` config), model dùng tool-calling; các tool ở `CustomerCare/Tools/*` (chỉ đọc dữ liệu đã có `ChatRoomDataConsent`).
- Người ↔ người: routing trực tiếp qua hub.

### 5.4 Sales (multi-store checkout)

```
POST /api/cart/items  ─► server-side cart per user
POST /api/orders (checkout)
  ├─► split items theo garden_store_id → nhiều Delivery
  ├─► với mỗi store: DeliveryFeeEstimator (GHN /fee → fallback ShippingFeeCalculator)
  ├─► tạo Order + N Deliveries + OrderStatusLog
  └─► return { paymentUrl } nếu online, hoặc { orderId } nếu COD
```

Vendor xử lý Delivery lifecycle: `Pending → Preparing → Shipped → Delivered / Returned`. Webhook nhà vận chuyển POST `/api/shipping/webhook` (header `X-Webhook-Secret`) → cập nhật `delivery_progress_logs`.

### 5.5 Garden Staff — invitation flow (mới, xem `FengDeskAI/docs/refactor-garden-staff-management.md`)

```
Owner  POST /api/stores/{id}/staff  {staffId}
         └─► assignment = Pending  +  Notification StaffInvited
                                        └─► FE badge chuông
Staff  POST /api/stores/staff/{aid}/accept
         └─► assignment = Accepted  → có quyền nhận đơn/ship (Repo.CanManageAsync)
              +  Notification StaffInvitationAccepted về Owner
         hoặc /reject → Rejected (không có quyền)
Owner  DELETE /api/stores/{id}/staff/{aid}  → Revoked (mất quyền ngay)
```

State machine: `Pending → Accepted → Revoked` hoặc `Pending → Rejected`. Store-scoped permission (`StoreRepository.CanManageAsync`) chỉ tính `Status == Accepted`. **Không tồn tại global role `GardenStaff`** — membership là source of truth.

---

## 6. Persistence

### 6.1 PostgreSQL (Supabase)

- Naming: `snake_case` số nhiều (`product_items`, `garden_staff_assignments`).
- Base columns tự động: `id uuid`, `created_at`, `updated_at`, `created_by`, `updated_by`, `is_deleted`.
- Extensions bật sẵn: `unaccent` (search có dấu — migration `20260620155312_EnableUnaccentExtension`).
- Migration cadence: 1 feature ≈ 1 migration. Naming rõ (`StaffInvitationFlow`, `AhamoveShippingFields`…). Data-preserving khi rename column (dùng `Sql()` backfill).

### 6.2 Non-DB storage

| Loại | Nơi | Ghi chú |
|---|---|---|
| Ảnh sản phẩm / chat / bằng chứng return | Supabase Storage bucket `Fengdesk_bucket` qua `IFileStorage` | `Product_images/{id}/`, `Chat_images/{chatboxId}/` |
| OTP + registration token | `IDistributedCache` (in-memory dev, Redis prod-swap) | Timing-safe compare |
| 3D model | Meshy (đang **mock**) | polling worker |

---

## 7. Auth model

### 7.1 Roles — bit-mask `UserRole [Flags]`

```
None=0 · Customer=1 · Manager=2 · Staff=4 · Admin=8 · GardenOwner=16
```

Một user có nhiều flag. JWT emit 1 `role` claim per flag → `RequireRole("Staff", "Admin")` hoạt động đúng.

Thứ tự platform (thấp→cao): Customer < Manager < Staff < Admin. `GardenOwner` **song song** (capability). Cấp tự động khi tạo store đầu.

### 7.2 Store-scoped access

Không có global `GardenStaff`. Quyền trên 1 store cụ thể check qua:

- **Owner-chính / co-owner:** row `garden_store_owners`.
- **Staff (Accepted):** row `garden_staff_assignments` với `status = Accepted`.
- Owner toàn quyền (sửa store, address, owners, staff).
- Staff chỉ nhận đơn + ship cho store đó.

---

## 8. External integrations

| Provider | Dùng cho | Config section | Ghi chú |
|---|---|---|---|
| **PayOS** | Cổng thanh toán VN | `PayOSSettings` | + COD offline |
| **Supabase (Postgres + Storage)** | DB + object storage | `ConnectionStrings.DefaultConnection`, `SupabaseStorage` | Service role key chỉ ở Development |
| **SMTP (MailKit)** | Gửi OTP | `MailSettings` | Gmail app password |
| **Ollama** | Chat LLM (tool-calling) | `AiChat` | Model như `gemma3:4b`, `qwen…` |
| **Meshy** | Image → 3D model | `Model3DSettings` | Hiện **mock** |
| **GHN + Ahamove** | Vận chuyển | `Shipping.Ghn`, `Shipping.Ahamove` | Fee API + webhook |
| **AI recommendation** | Rerank + explain | `AiRecommendationSettings` | **Service Python chưa tồn tại** |

**Không commit secrets.** `appsettings*.json` đã `.gitignore`; dev tự tạo local hoặc dùng `Section__Key` env.

---

## 9. Deployment

- Backend: Docker (`Dockerfile`, `docker-compose.yml`) — API cổng 8080, `migrate` service chạy 1 lần seed + migrate.
- Frontend: Vercel (`vercel.json` có sẵn), env `VITE_API_BASE_URL`.
- DB: Supabase managed Postgres.
- CI/CD: **chưa có**. Hiện dev local + push Vercel manual.

---

## 10. Where to look for what

| Câu hỏi | Nơi trả lời |
|---|---|
| Convention BE / thêm entity mới / EF migration flow | `FengDeskAI/docs/PROJECT-GUIDE.md` §10, `FengDeskAI/CLAUDE.md` |
| API contract chi tiết | `FengDeskAI/docs/api-documents/` (đánh số theo feature) |
| ERD & state diagrams | `FengDeskAI/docs/erd/SEP490_FengDeskAI.drawio` |
| Refactor plan (invitation, garden owner…) | `FengDeskAI/docs/ard/*.md` — spec trước khi code |
| Deliverables trường (Review1/2) | `Documents/1_SEP490/`, `Documents/2_SEP490/` |
| Branching + Conventional Commits | `CONTRIBUTING.md` (workspace root) |
| Rule cho AI agents | `AGENTS.md` (workspace root) |
| Code intel (call graph, symbol search) | `.codegraph/codegraph.db` (dev-only, dùng codegraph MCP) |
| FE feature layout / API layer | `FengDeskAI_FE/src/features/README.md`, `src/lib/httpClient.ts` |

---

## 11. Chưa làm (đừng giả định đã có)

- Python AI recommendation microservice thực (contract có, service chưa).
- Meshy image→3D thật (đang mock).
- SignalR hub cho notification (chỉ có ChatHub); realtime badge hiện qua polling 60s.
- Automated tests (unit/integration) + CI/CD pipeline.
- Analytics dashboard (owner/admin).
- Redis distributed cache (đang `AddDistributedMemoryCache`).
- Tích hợp thực với đơn vị vận chuyển (GHN/Ahamove) — mới có schema + webhook shell.

---

## 12. Cập nhật file này

Sync khi có thay đổi cross-cutting: thêm bounded context, thêm external integration, đổi auth model, đổi state machine invitation/order/delivery, thêm/bỏ deployment target. Đừng nhét chi tiết cấp code — chi tiết luôn ở `docs/` của repo con hoặc `docs/api-documents/`.
