# FengDeskAI — Project Guide

> Tài liệu tổng quan dành cho team + AI assistant đọc hiểu cấu trúc, conventions, và state hiện tại của project.
> Cập nhật mỗi khi thêm feature mới hoặc đổi convention.

---

## 1. Tổng quan

**FengDeskAI** là đề tài capstone **SEP490** (kỳ SU26, mã `SU26SE093`) — hệ thống **gợi ý sản phẩm trang trí bàn làm việc theo phong thủy** dùng AI.

Kiến trúc dự kiến:
- **.NET 8 monolith** (project hiện tại): business logic, auth, DB, orchestration
- **AI microservice** (Python FastAPI, sẽ làm sau): stateless scorer — nhận workspace profile + feng_shui_rules + candidate products → trả về scored recommendations
- **Frontend** (sẽ): web client

Lý do tách AI service: domain AI/NLP có ecosystem mạnh ở Python (transformers, langchain); .NET giữ vai trò business + DB.

---

## 2. Tech Stack

| Layer | Tech |
|---|---|
| Runtime | .NET 8 (LTS, `net8.0`) |
| DB | PostgreSQL 15+ via `Npgsql.EntityFrameworkCore.PostgreSQL 8.x` |
| ORM | EF Core 8 — migrations, Fluent API configurations |
| Mapping | AutoMapper 13 |
| Auth | `Microsoft.AspNetCore.Authentication.JwtBearer` + `BCrypt.Net-Next` |
| Email | MailKit 4 (SMTP) |
| Cache | `IDistributedCache` + `AddDistributedMemoryCache()` (in-memory, swap Redis sau) |
| Docs | Swashbuckle (Swagger UI) |

---

## 3. Repo Layout

```
FengDeskAI/
├── .gitignore                    # appsettings*.json bị ignore (xem mục 11)
├── FengDeskAI.slnx               # solution file (định dạng XML mới) — ở ROOT, KHÔNG trong src/
├── Documents/                    # ERD, reports, file này
└── src/
    ├── FengDeskAI.Domain/        # POCO entities, enums, value objects
    ├── FengDeskAI.Application/   # Use cases, DTOs, interfaces, services
    ├── FengDeskAI.Infrastructure/# EF Core, JWT, SMTP, cache impl
    ├── FengDeskAI.Contracts/     # DTO share với AI microservice (chưa có code)
    └── FengDeskAI.WebAPI/        # Controllers, DI bootstrap, middleware
```

---

## 4. Clean Architecture — Dependency Rule

```
Domain  ← Application  ← Infrastructure  ← WebAPI
                       ↖
                       Contracts (sẽ share cho AI microservice)
```

**Quy tắc CỨNG — không vi phạm:**

| Project | Được reference | TUYỆT ĐỐI không reference |
|---|---|---|
| `Domain` | Standard libs only | bất kỳ project nào trong solution |
| `Application` | `Domain`, `Contracts` | `Microsoft.AspNetCore.*`, `Microsoft.EntityFrameworkCore` |
| `Infrastructure` | `Application`, `Domain`, EF Core, AspNetCore | — |
| `WebAPI` | `Application`, `Infrastructure` | — |

Mục tiêu: thay được EF Core bằng Dapper/MongoDB không phải sửa Application; thay được ASP.NET Core bằng gRPC không phải sửa Application.

---

## 5. Conventions

### 5.1 Domain Layer

- Entity là **POCO thuần** — KHÔNG có `[Table]`, `[Column]`, `[Index]` từ EF Core. Mapping qua `IEntityTypeConfiguration<T>` trong Infrastructure.
- Mọi entity nghiệp vụ kế thừa `BaseEntity` (`Id`, `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`, `IsDeleted`).
- Enums đặt trong `Domain/Enums/<feature>/`.
- Entities hiện tại là anemic (chỉ data) — chấp nhận cho capstone, không phải full DDD.

#### 5.1.1 Enum vs string — quyết định khi thêm field "controlled vocabulary"

Khi gặp field thuộc tập giá trị cố định (vd `location_type`, `desk_type`, `feng_shui_element`), có 3 lựa chọn:

| Cách | Pros | Cons | Khi nào dùng |
|---|---|---|---|
| **String field (free-form)** | Linh hoạt nhất, không cần code change để thêm value | Không validation, user gửi gì cũng nhận, AI prompt khó list closed set, refactor đau | Tag/keyword tự do (vd `tags.name`) |
| **C# enum + lưu DB int** | Type-safe, IntelliSense, compile-time check | Query DB raw rất khó đọc (`gender = 1` là gì?). Renumber enum order → break DB | Khi DB hiếm khi query bằng tay (vd `Gender`) |
| **C# enum + lưu DB string** ⭐ | Type-safe **và** DB human-readable. Đổi enum order không sao. Thêm value chỉ cần thêm 1 dòng enum + có thể cần migration mở rộng `VARCHAR` | Đổi tên enum → cần migration `UPDATE` data | **Mặc định cho FengDeskAI** — mọi controlled vocab |

**Pattern áp dụng** (xem `WorkspaceProfileConfiguration.cs:21-28`):

```csharp
// Domain/Enums/Workspace/DeskType.cs
public enum DeskType { Sitting, Standing, StandingSitting, LShape, Corner, Other }

// Infrastructure/Persistence/Configurations/WorkspaceProfileConfiguration.cs
builder.Property(w => w.DeskType)
    .HasColumnName("desk_type")
    .HasConversion<string>()      // ← lưu "Sitting", "Standing" thay vì 0, 1
    .HasMaxLength(30);
```

DB sẽ thấy: `desk_type = 'Standing'` thay vì `desk_type = 1`. AI service đọc DB cũng dễ hiểu hơn.

**Khi nào KHÔNG dùng enum (dùng string free-form):**
- Giá trị do admin tự thêm runtime (vd `tags`, `categories.name`)
- Set quá lớn hoặc thay đổi liên tục (vd `country_code` — dùng ISO list trong code/seed thay vì enum)
- Cần i18n cho từng giá trị → lưu key + bảng dịch riêng

**Lợi cho AI recommendation:**
- Prompt cho AI có thể list closed set: `"feng_shui_element must be one of: Kim, Moc, Thuy, Hoa, Tho"`
- AI service deserialize JSON dễ (chuỗi cố định, không cần lookup table)
- Frontend dropdown sinh từ `Enum.GetNames<T>()` thay vì hard-code

**Gotcha cần lưu ý:**
- `HasMaxLength(N)` phải đủ chứa tên dài nhất + buffer cho enum value mới. Khi cần thêm value vượt N → migration `AlterColumn`.
- Đổi tên enum existing (vd `Staff` → `Employee`) → data cũ trong DB vẫn là `"Staff"` → cần migration `UPDATE table SET col = 'Employee' WHERE col = 'Staff'`.
- KHÔNG đổi enum value order (`int` underlying) khi DB đã lưu int — luôn dùng `HasConversion<string>()` để tránh rủi ro.

#### 5.1.2 Existing Enums — controlled vocabularies đã định nghĩa

Tổng hợp các enum đã có trong codebase. Dùng để:
- Frontend sinh dropdown (`Enum.GetNames<T>()`)
- AI prompt liệt kê closed-set values (vd: `"feng_shui_element must be one of: Kim, Moc, Thuy, Hoa, Tho"`)
- Validation request DTO
- DB human-readable khi query bằng tay (các enum string)

##### Identity (`Domain/Enums/`)

| Enum | Values | DB type | Note |
|---|---|---|---|
| `Gender` | `Unspecified`, `Male`, `Female`, `Other` | `int` | Set ổn định, hiếm query bằng tay |
| `UserRole` `[Flags]` | `None=0`, `Customer=1`, `Manager=2`, `Staff=4`, `Admin=8` | `int` (bit-mask) | 1 user có nhiều role: `Manager &#124; Staff = 6`. Chi tiết ở mục 6.3 |

##### Workspace (`Domain/Enums/Workspace/`)

| Enum | Values | DB type | Mô tả |
|---|---|---|---|
| `LocationType` | `Home`, `Office`, `Cafe`, `Studio`, `Other` | `varchar(30)` | Loại không gian làm việc |
| `WorkspaceStyle` | `Modern`, `Classic`, `Minimal`, `Industrial`, `Scandinavian`, `Bohemian`, `Other` | `varchar(30)` | Phong cách thiết kế |
| `LightingType` | `Natural`, `Artificial`, `Mixed`, `Dim` | `varchar(30)` | Loại ánh sáng chủ đạo |
| `DeskType` | `Sitting`, `Standing`, `StandingSitting`, `LShape`, `Corner`, `Other` | `varchar(30)` | Loại bàn |
| `CompassDirection` | `North`, `Northeast`, `East`, `Southeast`, `South`, `Southwest`, `West`, `Northwest` | `varchar(15)` | 8 hướng la bàn — dùng cho cả `desk_orientation` và `room_facing_direction` |
| `WorkPurpose` | `Office`, `Study`, `Creative`, `Reading`, `Gaming`, `Mixed`, `Other` | `varchar(30)` | Mục đích sử dụng workspace |
| `FengShuiElement` | `Kim`, `Moc`, `Thuy`, `Hoa`, `Tho` | `varchar(10)` | Ngũ hành phong thủy — **input lõi cho AI matching** với `feng_shui_rules` |

##### Application Common (`Application/Common/Enums/`)

| Enum | Values | Note |
|---|---|---|
| `OtpPurpose` | `Register`, `ResetPassword`, `ChangeEmail` | Phân biệt OTP theo mục đích — cache key khác nhau, tránh OTP register bị reuse cho reset password |
| `OtpVerifyResult` | `Success`, `Invalid`, `Expired`, `TooManyAttempts` | Result của `IOtpService.VerifyOtpAsync` |

##### Quy ước khi thêm value mới

1. **Thêm value vào enum**: chú ý KHÔNG đổi thứ tự các value cũ nếu enum lưu `int` (vd `Gender`, `UserRole`). Value mới luôn append cuối.
2. **Nếu enum dùng `HasConversion<string>()`**: check `HasMaxLength(N)` còn đủ chứa value mới không. Nếu không → migration `AlterColumn` tăng `N`.
3. **Nếu value mới đại diện business logic phong thủy mới** (vd thêm `FengShuiElement.SuperKim`) → có thể cần seed thêm rule ở `feng_shui_rules` table để AI biết match thế nào.
4. **Đổi tên value cũ**: cần migration `UPDATE table SET col = 'NewName' WHERE col = 'OldName'` cho các string-stored enum, hoặc data migration cho int-stored.
5. **Xoá value**: chỉ làm khi chắc chắn không còn row nào trong DB dùng value đó (check bằng `SELECT DISTINCT col FROM table`). Tốt nhất deprecate trước, xoá sau 1-2 release.

### 5.2 Application Layer

- **Feature folder**: `Application/Features/<BoundedContext>/{DTOs, Mappings, Services}/` — KHÔNG flat tất cả services trong 1 folder.
- `ServiceResult<T>` ở `Application/Common/Results/` — KHÔNG ở Domain (có `StatusCode` là khái niệm HTTP).
- DI ở `Application/DependencyInjection.cs` chỉ cho services thuần logic + AutoMapper.
- KHÔNG reference `Microsoft.AspNetCore.*`. Nếu cần `StatusCodes.Status200OK`, dùng `ApiStatusCodes.Ok` từ `Common/Constants/`.

### 5.3 Infrastructure Layer

- EF Configuration tách riêng file: `Infrastructure/Persistence/Configurations/<Entity>Configuration.cs`.
- Repository pattern:
  - `IGenericRepository<T>` giữ `GetAllQueryable()` cho prototype.
  - Repo cụ thể (`IUserRepository`, ...) phải có **specific methods** cho query lặp lại / performance-critical, không để service chain `.Include()` trực tiếp.
- `AppDbContext.SaveChangesAsync` override: tự fill audit (`CreatedAt`/`UpdatedAt`/`CreatedBy`/`UpdatedBy`) + biến `Remove()` thành soft-delete (`IsDeleted = true`).
- `IUnitOfWork` group repos + `SaveChangesAsync` + `ExecuteInTransactionAsync<TResult>`.
- External services: `Infrastructure/ExternalServices/<provider>/` (vd `Mail/`).
- Settings classes: `public const string SectionName = "..."` + đăng ký bằng `services.AddSettings<T>(configuration)` extension (xem `Infrastructure/Common/ConfigurationExtensions.cs`).

### 5.4 WebAPI Layer

- Controller kế thừa **`ApiControllerBase`** (`WebAPI/Common/ApiControllerBase.cs`) — đã có sẵn `[ApiController]` + helpers:
  - `CurrentUser` — `ICurrentUserService` lazy-resolved (sub-class không cần inject)
  - `CurrentUserId` (non-nullable `Guid`) — throw `UnauthorizedAccessException` nếu thiếu claim → filter trả 401
  - `ToActionResult(IServiceResult)` / `ToActionResult<T>(IServiceResult<T>)` — map status code chuẩn
- Controllers **thin** — 1 dòng cho 1 action là tốt:
  ```csharp
  [HttpGet]
  public async Task<IActionResult> GetMine(CancellationToken ct)
      => ToActionResult(await _service.GetMineAsync(CurrentUserId, ct));
  ```
- `[Authorize]` ở controller level cho feature cần login; `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]` cho role-restricted.
- KHÔNG inject `ICurrentUserService` ở constructor (đã có ở base).
- KHÔNG inject `IUnitOfWork`/repository trực tiếp vào controller.
- KHÔNG tự viết lại `TryGetUserId` hay `ToActionResult` — dùng từ base.
- Exception filter `UnauthorizedExceptionFilter` đã được register global trong `Program.cs` → bắt `UnauthorizedAccessException` và trả `ServiceResult.Failure(401, ...)` đồng dạng các endpoint khác.

### 5.5 Security

- Secrets (`appsettings.json`, `appsettings.Development.json`) đã `.gitignore`. Mỗi dev tự tạo local.
- JWT: access token 60 phút, refresh token 14 ngày, refresh-token rotation.
- OTP: max 5 lần verify sai, cooldown 60s giữa các lần resend. So sánh bằng `CryptographicOperations.FixedTimeEquals` (chống timing attack).
- Registration token (32-byte random base64url) sau verify OTP — finalize **bắt buộc** gửi token, KHÔNG chỉ check email.
- BCrypt work factor 12.

---

## 6. Authentication & Authorization

### 6.1 Registration — 3 bước

```
POST /api/auth/register/initiate   { email }
  → check email chưa tồn tại → gửi OTP qua SMTP

POST /api/auth/register/verify     { email, otp }
  → verify OTP → trả { registrationToken, expiresAt }

POST /api/auth/register/finalize   { registrationToken, password, fullName, phone, dateOfBirth, gender }
  → consume token → tạo user → issue { accessToken, refreshToken, user }
```

### 6.2 Auth endpoints khác

| Method | Path | Auth | Mô tả |
|---|---|---|---|
| POST | `/api/auth/login` | – | email + password → tokens |
| POST | `/api/auth/refresh` | – | refresh token → tokens mới (rotate) |
| POST | `/api/auth/logout` | ✓ | revoke refresh token |
| GET  | `/api/auth/me` | ✓ | info user hiện tại |

### 6.3 Roles — bit-mask

```csharp
[Flags]
public enum UserRole
{
    None     = 0,
    Customer = 1 << 0,  // 1
    Manager  = 1 << 1,  // 2
    Staff    = 1 << 2,  // 4
    Admin    = 1 << 3,  // 8
}
```

1 user có nhiều role: `Manager | Staff = 6`. JWT có 1 `role` claim per flag → `RequireRole("Manager", "Admin")` hoạt động đúng.

### 6.4 Authorization policies

Define trong `WebAPI/Authorization/AuthorizationPolicies.cs`:

| Policy | Cho phép |
|---|---|
| `AdminOnly` | Admin |
| `ManagerOrAdmin` | Manager, Admin |
| `StaffOrAbove` | Staff, Manager, Admin |
| `CustomerOnly` | Customer |

---

## 7. Implemented Features

| Status | Feature | Tables |
|---|---|---|
| ✅ | Identity (auth flow, JWT, roles) | `users`, `refresh_tokens` |
| ✅ | Workspace Profiles | `workspace_profiles` |
| ✅ | Geography | `provinces`, `districts`, `wards`, `user_address` |
| ✅ | Vendor multi-store | `garden_stores`, `garden_staff_assignments`, `stores_address` |
| ✅ | Catalog | `products`, `product_items`, `product_images`, `categories`, `tags`, `product_categories`, `product_tags` |
| ✅ | Sales (multi-store → multi-delivery) | `carts`, `cart_items`, `orders`, `order_items`, `deliveries`, `order_status_log` |
| ✅ | Shipping (webhook ingest) | `delivery_progress_logs`, `shipping_webhook` |
| ⏳ | Customer care | `reviews`, `support_tickets`, `after_sales_requests` |
| ⏳ | Notification | `notification` |
| ⏳ | Recommendation (AI) | `recommendations`, `recommendation_items`, `recommendation_logs`, `feng_shui_rules` |
| 🔒 | Payment / Balance / 3rd-party | sẽ tính sau |

---

## 8. Database

### 8.1 Tables hiện có

```
users
  id (uuid PK), email (UNIQUE), password_hash, full_name, date_of_birth, gender (int),
  phone (UNIQUE WHERE NOT NULL), role (int bit-mask), balance (numeric 12,3),
  is_active, [audit + soft-delete]

refresh_tokens
  id (uuid PK), user_id (FK users CASCADE), token (UNIQUE), expires_at,
  is_revoked, revoked_at, replaced_by_token, [audit]
  INDEX (user_id, is_revoked)

workspace_profiles
  id (uuid PK), user_id (FK users CASCADE), name (varchar 100),
  location_type, style, lighting, desk_type (enum stored as VARCHAR),
  desk_orientation, room_facing_direction (8 hướng),
  work_purpose, feng_shui_element (Ngũ hành: Kim/Moc/Thuy/Hoa/Tho),
  desk_area (int, cm²), is_default, [audit]
  PARTIAL UNIQUE INDEX (user_id) WHERE is_default = TRUE AND is_deleted = FALSE
```

### 8.2 Migration commands

```bash
# Tạo migration mới (chạy ở repo root)
dotnet ef migrations add <Name> \
  --project src/FengDeskAI.Infrastructure \
  --startup-project src/FengDeskAI.WebAPI \
  -o Persistence/Migrations

# Apply lên DB
dotnet ef database update \
  --project src/FengDeskAI.Infrastructure \
  --startup-project src/FengDeskAI.WebAPI

# Revert / xoá migration cuối
dotnet ef database update <PreviousMigrationName> ...
dotnet ef migrations remove ...
```

Migrations file lưu tại `src/FengDeskAI.Infrastructure/Persistence/Migrations/`. 1 feature ≈ 1 migration, đặt tên rõ ràng (`InitialAuth`, `WorkspaceProfiles`, ...).

---

## 9. API Endpoints

### Public (anonymous)
- `POST /api/auth/register/initiate | verify | finalize`
- `POST /api/auth/login | refresh`
- `GET  /api/dev/ping/public`
- `GET  /api/locations/provinces | provinces/{id}/districts | districts/{id}/wards`
- `GET  /api/stores | /api/stores/{id}` — danh sách/chi tiết cửa hàng
- `GET  /api/categories | /api/categories/{id}` · `GET /api/tags`
- `GET  /api/products` (filter store/category/tag + search + paging) · `GET /api/products/{id}`
- `POST /api/shipping/webhook` — callback nhà vận chuyển (yêu cầu header `X-Webhook-Secret`)

### Authenticated (any role)
- `GET  /api/auth/me`
- `POST /api/auth/logout`
- `GET    /api/workspace-profiles` — list của user hiện tại
- `GET    /api/workspace-profiles/default`
- `GET    /api/workspace-profiles/{id}`
- `POST   /api/workspace-profiles`
- `PUT    /api/workspace-profiles/{id}`
- `POST   /api/workspace-profiles/{id}/set-default`
- `DELETE /api/workspace-profiles/{id}` — soft-delete
- `GET    /api/dev/ping/authenticated`
- **Addresses** (customer): `GET|POST /api/addresses`, `PUT|DELETE /api/addresses/{id}`, `PATCH /api/addresses/{id}/set-default`
- **Cart** (customer): `GET /api/cart`, `POST /api/cart/items`, `PUT|DELETE /api/cart/items/{itemId}`, `DELETE /api/cart`
- **Orders** (customer): `POST /api/orders` (checkout), `GET /api/orders` (paged), `GET /api/orders/{id}`, `POST /api/orders/{id}/cancel`
- **Orders** (vendor): `GET /api/orders/stores/{storeId}/deliveries`, `PATCH /api/orders/deliveries/{deliveryId}/status`
- **Shipping** (vendor): `GET /api/shipping/deliveries/{deliveryId}/progress`

### Role-restricted
- `GET /api/dev/ping/admin` — Admin
- `GET /api/dev/ping/manager` — Manager/Admin
- `GET /api/dev/ping/staff` — Staff/Manager/Admin
- **Categories/Tags** CRUD (`POST|PUT|DELETE`) — Manager/Admin
- **Products** CRUD + items/images/category-tag links — Vendor (owner/staff của store); ownership check ở service layer
- **Stores**: `POST /api/stores` (tạo + gán owner) — Admin; `PUT /api/stores/{id}`, `…/address`, `…/staff` — owner/admin

---

## 10. Adding a New Entity — Checklist

Thứ tự **bắt buộc** khi thêm entity mới (vd `Product`):

1. **Domain** `Entities/<Context>/<Entity>.cs` — POCO kế thừa `BaseEntity`
2. **Domain** `Enums/<Context>/...` — enums liên quan (nếu có)
3. **Infrastructure** `Persistence/Configurations/<Entity>Configuration.cs` — Fluent API mapping
4. **Application** `Interfaces/Repositories/I<Entity>Repository.cs` — kế thừa `IGenericRepository<Entity>` + specific methods
5. **Infrastructure** `Persistence/Repositories/<Entity>Repository.cs` — impl
6. **Application** `Interfaces/Repositories/IUnitOfWork.cs` — add property
7. **Infrastructure** `Persistence/UnitOfWork.cs` — wire vào constructor + property
8. **Application** `Features/<Context>/DTOs/...` — Create/Update/Response DTOs
9. **Application** `Features/<Context>/Mappings/<Entity>MappingProfile.cs` — AutoMapper
10. **Application** `Features/<Context>/Services/I<Entity>Service.cs` + impl
11. **Application** `DependencyInjection.cs` — register service + mapper profile
12. **Infrastructure** `DependencyInjection.cs` — register repository
13. **WebAPI** `Controllers/<Entity>Controller.cs` — thin, dùng `ICurrentUserService` cho ownership
14. Migration: `dotnet ef migrations add <Name>` → `database update`
15. Build verify: `dotnet build FengDeskAI.slnx`

---

## 11. Configuration

### Settings class pattern

```csharp
public class XxxSettings
{
    public const string SectionName = "Xxx";
    public string Field1 { get; set; } = null!;
}
```

DI:
```csharp
services.AddSettings<XxxSettings>(configuration);  // extension method
// Hoặc đọc trực tiếp không qua DI:
var settings = configuration.GetSettings<XxxSettings>();
```

### appsettings management

- `appsettings.json` + `appsettings.Development.json` → **đã .gitignore**, không commit.
- Mỗi dev tự tạo local. Schema tối thiểu:

```jsonc
// src/FengDeskAI.WebAPI/appsettings.json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=sep490_fengdeskai_dev;Username=postgres;Password=YOUR_PG_PASSWORD"
  },
  "JwtSettings": {
    "Issuer": "FengDeskAI",
    "Audience": "FengDeskAI.Client",
    "SecretKey": "RANDOM_STRING_AT_LEAST_32_CHARS",
    "AccessTokenMinutes": 60,
    "RefreshTokenDays": 14
  },
  "MailSettings": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "UseStartTls": true,
    "Username": "your-gmail@gmail.com",
    "Password": "16_CHAR_GMAIL_APP_PASSWORD",
    "FromEmail": "your-gmail@gmail.com",
    "FromName": "FengDeskAI"
  },
  "Otp": {
    "Length": 6,
    "TtlMinutes": 10,
    "ResendCooldownSeconds": 60,
    "MaxVerifyAttempts": 5
  }
}
```

> **KHÔNG** sửa `.gitignore` để bypass. Nếu cần thêm secret, định nghĩa thêm class `XxxSettings` với `SectionName` và section JSON tương ứng.

---

## 12. Local Setup

```bash
# 1. Cài tooling
# .NET 8 SDK, PostgreSQL 15+
dotnet tool install --global dotnet-ef

# 2. Tạo PG database
psql -U postgres -c "CREATE DATABASE sep490_fengdeskai_dev;"

# 3. Tạo appsettings.json (copy schema từ mục 11)
# Lưu vào src/FengDeskAI.WebAPI/appsettings.json

# 4. Apply migrations
dotnet ef database update \
  --project src/FengDeskAI.Infrastructure \
  --startup-project src/FengDeskAI.WebAPI

# 5. Run
dotnet run --project src/FengDeskAI.WebAPI
```

Swagger UI: `https://localhost:7016/swagger` (xem `Properties/launchSettings.json` cho port).

---

## 13. AI Microservice Integration (planned)

Khi triển khai recommendation:

1. .NET Application định nghĩa `IRecommendationAIClient` trong `Application/Interfaces/External/`
2. Infrastructure implement bằng typed `HttpClient` + Polly retry/timeout/circuit-breaker
3. DTO request/response share giữa 2 codebase — đặt ở `FengDeskAI.Contracts/AI/`
4. AI service (Python FastAPI):
   - **Stateless** scorer — nhận `RecommendationAIRequest` (profile + matching rules + candidate products) → trả `RecommendationAIResponse` (scored items + reasoning)
   - Business rules (`feng_shui_rules`) vẫn ở .NET DB (admin CRUD); .NET bundle vào request body

Trên .NET, `RecommendationOrchestrator` thực hiện:
```
Load workspace profile
  → load matching feng_shui_rules
  → load candidate products
  → call IRecommendationAIClient
  → persist Recommendation + Items + Log (transaction)
  → return DTO
```

---

## 14. Notes for AI Assistant

Khi AI (Claude, etc.) đọc tài liệu này và làm việc trên project, lưu ý:

- **Conventions ở Section 5 là CỨNG** — không vi phạm. Đặc biệt:
  - KHÔNG add `Microsoft.AspNetCore.*` vào Application
  - KHÔNG dùng `[Table]`/`[Column]` trên Domain entities
  - `ServiceResult` ở Application, không phải Domain
- Khi thêm entity mới, follow **đầy đủ 15 bước** ở Section 10.
- Settings: pattern `const string SectionName` + `AddSettings<T>` extension. KHÔNG dùng magic string `"MailSettings"` trong `GetSection(...)`.
- Migrations: 1 feature ≈ 1 migration, tên rõ ràng (PascalCase, mô tả nội dung).
- Repository: giữ `GetAllQueryable()` cho prototype, nhưng query lặp lại/perf-critical phải có method tường minh trên repo cụ thể.
- Security: KHÔNG hardcode secrets, KHÔNG sửa `.gitignore` để bypass.
- Trao đổi/comment tiếng Việt OK; code identifier (class/method/field) giữ tiếng Anh.

---

## 15. Glossary

- **Workspace Profile**: hồ sơ không gian làm việc của user (lighting, desk_type, desk_orientation, feng_shui_element, ...). Là input cho AI recommendation.
- **Feng Shui Rules**: bảng admin-managed, định nghĩa nguyên tắc phong thủy mapping với element/lighting/desk_type/work_purpose → score_weight + required_tag. AI dùng để score sản phẩm.
- **Garden Store**: cửa hàng bán cây/sản phẩm phong thủy (multi-vendor). Owner qua `owner_id`; nhân viên qua `garden_staff_assignments` (active).
- **Product Item**: biến thể/SKU của `Product` — đơn vị mang **giá + tồn kho**. Cart & Order trỏ vào `product_item_id` (KHÔNG phải product).
- **Delivery**: một order có hàng từ nhiều store → tách thành nhiều delivery (mỗi store một delivery), **không** tách sub-order. Status fulfillment ở delivery; `orders.status` là rollup. Hủy/giao xong → tự cập nhật order.
- **Delivery Progress Log**: nhật ký mỗi lần đổi trạng thái delivery; `source_type` = Manual (vendor) / Webhook (nhà vận chuyển, kèm `raw_payload` JSONB).
- **Registration Token**: token 32-byte tạm thời sau khi verify OTP, dùng 1 lần ở bước finalize đăng ký.
- **BaseEntity**: abstract class chứa Id + audit fields + soft-delete flag.

> **Lưu ý thiết kế (lệch nhẹ so với ERD draw.io, có chủ đích):** money thống nhất `DECIMAL(12,2)`; `product_categories`/`product_tags` là junction composite-PK (không BaseEntity); status order/delivery do dev đề xuất (State Diagram trống) — chỉnh khi chốt; thuộc tính phong thủy của product đánh dấu bằng **tags** (product không có cột feng_shui_element).

---

*Last updated: 2026-06-09 — hoàn tất Geography, Vendor, Catalog, Sales (multi-delivery), Shipping (webhook). Pending: Customer care, Notification, Recommendation, Payment.*
