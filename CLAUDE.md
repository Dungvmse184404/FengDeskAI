# CLAUDE.md

Hướng dẫn cho Claude khi làm việc trong repo backend **FengDeskAI**.

## Dự án là gì

Nền tảng **e-commerce bán sản phẩm/vật phẩm phong thủy** (chủ yếu cây & đồ trang trí bàn làm việc), kèm:

- **Tư vấn bằng AI**: người dùng mô tả không gian làm việc (mệnh/ngũ hành, hướng bàn, ánh sáng, mục đích…). Một **engine chấm điểm phong thủy deterministic viết bằng .NET** xếp hạng sản phẩm; lớp AI **chỉ giải thích** kết quả — **không tự bịa luật, không thêm/bớt sản phẩm**.
  **Hai luồng** tách theo `ProductPlacement`: đồ đặt trong phòng (`Desk`/`Living`) chấm theo **gap ngũ hành của phòng**; vật mang theo người (`Carry`) chấm theo **dụng thần của người** (`POST /api/recommendations/personal`); hàng tiêu hao (`Consumable`) không vào luồng nào.
- **Trợ lý hội thoại** (chat) dùng LLM với tool-calling.
- **Hiển thị model 3D** của vật phẩm (optional) — sinh từ ảnh qua **Meshy (gọi HTTP thật)**; cờ `MeshySettings:UseMock` trong config **không được code đọc ở đâu cả**, đừng tin nó.

> SEP490 capstone, mã `SU26SE093`. Frontend nằm ở repo riêng `FengDeskAI_FE`.

## Kiến trúc — Clean Architecture (4 tầng + Contracts)

```
WebAPI  ──►  Application  ──►  Domain
   │              │
   └──►  Infrastructure ◄──────┘
                  │
            Contracts  (DTO chia sẻ với AI recommendation microservice - Python)
```

| Project | Vai trò |
|---|---|
| `FengDeskAI.Domain` | Entities, enums, business rule thuần. Mọi entity kế thừa `Common/BaseEntity` (Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted). |
| `FengDeskAI.Application` | Feature theo bounded context + feng-shui engine + interfaces. **Không phụ thuộc Infrastructure/WebAPI.** |
| `FengDeskAI.Infrastructure` | EF Core/Npgsql, migrations, repositories, UnitOfWork, tích hợp ngoài (PayOS, Supabase Storage, Ollama, Meshy, SMTP). |
| `FengDeskAI.WebAPI` | Controllers, SignalR hub, background workers, authorization, `Program.cs`. |
| `FengDeskAI.Contracts` | Request/response contract cho AI recommendation service (Python). |

Quy tắc phụ thuộc: **Domain không phụ thuộc gì; Application chỉ phụ thuộc Domain + Contracts; Infrastructure & WebAPI là tầng ngoài.** Giữ đúng chiều này khi thêm code.

## Tech stack

- .NET 8, ASP.NET Core Web API
- EF Core 8 + Npgsql, **PostgreSQL** (Supabase)
- Realtime: **SignalR** (`/hubs/chat`)
- Auth: **JWT Bearer + refresh token**, email OTP (MailKit/SMTP)
- Thanh toán: **PayOS** (+ COD)
- Lưu ảnh: **Supabase Storage**
- LLM hội thoại: **Ollama** (qwen/gemma…) với tool-calling
- Sinh 3D: **Meshy** (image-to-3D) — hiện mock
- Mapping: **AutoMapper 13**
- Deploy: Docker / Railway

## Quy ước code (quan trọng — theo đúng pattern hiện có)

**Pattern theo Feature, KHÔNG dùng MediatR/CQRS.** Mỗi feature trong `Application/Features/<Context>/` gồm:

```
DTOs/        # request/response records
Mappings/    # AutoMapper Profile
Services/    # I<Name>Service (interface) + <Name>Service (impl)  -> chứa business logic
```

Các context: `Identity, Workspace, Catalog, Vendor, Geography, Sales, Payment, Shipping, Chat, CustomerCare, Returns, Announcement`.

- **Controllers mỏng**: chỉ nhận request, gọi service, trả về. Logic nằm trong Service.
- **Repository + UnitOfWork**: truy cập DB qua `Application/Interfaces/Repositories`, implement ở `Infrastructure/Persistence/Repositories`. Commit qua `UnitOfWork`.
- **Result pattern**: trả kết quả qua type trong `Application/Common/Results` (không ném exception cho lỗi nghiệp vụ).
- **EF config**: mỗi entity có config riêng trong `Infrastructure/Persistence/Configurations` (Fluent API), không annotate trong Domain.
- **Soft-delete**: dùng cờ `IsDeleted` của `BaseEntity`, không xóa cứng.
- Đặt tên DB: bảng snake_case số nhiều (vd `product_items`), khớp với ERD ở `Documents/ERD/`.
- **Mọi định danh viết bằng tiếng Anh** — class, method, biến, tên test, `DisplayName` của test. Tiếng Việt không dấu (kiểu `Them_san_pham_vao_gio`) gây kém chuyên nghiệp và dễ nhầm lẫn. Tên test theo dạng `Subject_Condition_Expectation`, vd `Checkout_EmptyCart_IsRejected`. **Comment và tài liệu vẫn viết tiếng Việt.**

### Feng-shui engine (cốt lõi nghiệp vụ)
`Application/Features/CustomerCare/Engine/` — deterministic. Khi sửa logic gợi ý, sửa ở đây.

- `ElementVector` + `ElementVectorBuilders` + `WorkspaceElementAnalyzer` — dựng vector phòng/sản phẩm.
- `PersonalTargetBuilder` — vector "người đang cần hành gì" (dụng thần Tứ Trụ, fallback Nạp Âm).
- **`ScoringModels`** — `ScoringParameters` (nạp từ bảng `scoring_params`, thiếu row thì dùng default trong code) + **`PlacementPolicy`: BẢNG luật theo `ProductPlacement`**. Thêm luật = thêm một dòng bảng, đừng rải `switch` vào `RecommendationScorer`.
- `RecommendationScorer` — `ScoreOne` đọc policy rồi chấm.
- `FengShuiCalculator` / `DestinyCalculator` / `BaTuCalculator` / `LunarCalendarConverter` — tính mệnh thuần.

⚠️ **Năm sinh phải là năm ÂM lịch.** Dùng `FengShuiCalculator.GetLunarYear(...)` hoặc overload nhận `DateOnly`/`DateTime`; **đừng lấy `DateTime.Year` thô** — người sinh tháng 1–2 trước Tết ra sai mệnh (bug đã từng có ở 5 call site).

⚠️ Engine chốt **tập sản phẩm**, nhưng AI hiện **vẫn hoán vị được `FinalRank`** trong topN.

### AI tools
`Application/Features/CustomerCare/Tools/`: mỗi tool LLM gọi được là 1 class — **14 tool đăng ký DI**. Tool đọc dữ liệu chính chủ tự scope theo `context.UserId`; **`ChatRoomDataConsent` chỉ gate `get_chat_partner_info`**; `prepare_order`/`confirm_order` chỉ bật ở phòng riêng (`AiChatService.PrivateRoomOnlyTools`).

> `Tools/CancelOrderTool.cs` là **file rỗng**, chưa implement và không đăng ký DI — đừng tính vào danh sách tool khả dụng.

### Background workers
`WebAPI/Workers/`: `AiBotWorker` (+ `AiBotQueue`), `Model3DPollingWorker` (poll Meshy), `OrderExpirationWorker` (hết hạn đơn online chưa thanh toán).

## Build / chạy / DB

```bash
# chạy migrations + seed reference data rồi thoát
dotnet run --project src/FengDeskAI.WebAPI -- seed

# chạy API (Swagger ở /swagger)
dotnet run --project src/FengDeskAI.WebAPI

# build toàn solution
dotnet build FengDeskAI.slnx
```

EF migrations (DbContext ở Infrastructure, startup ở WebAPI):

```bash
dotnet ef migrations add <Name> -p src/FengDeskAI.Infrastructure -s src/FengDeskAI.WebAPI
dotnet ef database update          -p src/FengDeskAI.Infrastructure -s src/FengDeskAI.WebAPI
```

Docker:

```bash
docker compose up -d --build          # API ở cổng 8080
docker compose run --rm migrate       # migrate + seed một lần
```

## Cấu hình & bảo mật

- Secret **không** commit. `appsettings.json` chỉ có default trống; giá trị thật lấy từ env (`Section__Key`) hoặc file `.env` local.
- Biến quan trọng: `ConnectionStrings__DefaultConnection`, `JwtSettings__SecretKey`, `MailSettings__*`, `PayOSSettings__*`, `SupabaseStorage__ApiKey`, `AiChat__BaseUrl`, `AiRecommendationSettings__ApiKey`.
- **Không** in/log secret; không hard-code key vào code.

## Khi điều hướng / phân tích codebase

- Repo đã có **CodeGraph index** (`.codegraph/`). Ưu tiên dùng công cụ CodeGraph để tìm quan hệ class/method và kiến trúc trước khi grep thủ công.
- ERD & diagrams (logical, state, use case…) ở `Documents/ERD/SEP490_FengDeskAI.drawio`. Khi đổi schema, cập nhật ERD tương ứng.
- Proposal & trạng thái tính năng: `Documents/PROPOSAL.md`.

## Test & CI

```bash
# Cấu hình MỘT LẦN: tạo DB test rồi copy file mẫu, sửa mật khẩu Postgres của bạn.
createdb -h localhost -U postgres fengdeskai_test
cp tests/FengDeskAI.ApiTests/appsettings.Testing.json.example tests/FengDeskAI.ApiTests/appsettings.Testing.json

# Sau đó chạy được từ dòng lệnh lẫn nút Run của IDE, không cần set biến môi trường.
dotnet test FengDeskAI.slnx
```

Connection string tìm theo thứ tự: biến môi trường `ConnectionStrings__DefaultConnection` (CI dùng, luôn ưu tiên) → `appsettings.Testing.json` (gitignore, mỗi máy một file) → không có thì dừng kèm hướng dẫn. **Không có giá trị mặc định trong code.**

- `tests/FengDeskAI.UnitTests` — unit test thuần (xunit + Moq). **113 test.**
- `tests/FengDeskAI.ApiTests` — integration test in-process qua `WebApplicationFactory`. **356 test.** Tầng 1 phủ **toàn bộ** endpoint ở mức smoke + ma trận phân quyền, tự sinh từ routing table nên endpoint mới được phủ ngay; tầng 2 phủ nghiệp vụ theo bounded context (Identity, Sales, Returns/RMA + SLA + công nợ, Catalog + model 3D, Vendor, quản trị + tham số chấm điểm, Workspace, gợi ý, đánh giá, giao hàng, địa chỉ, bảng tra cứu). Xem [`tests/FengDeskAI.ApiTests/README.md`](tests/FengDeskAI.ApiTests/README.md) và [`docs/adr/api-integration-testing.md`](docs/adr/api-integration-testing.md).
- **Ca test có tác dụng phụ lên phiên đăng nhập** (thứ làm đổi `TokenVersion`: tạo cửa hàng, khóa user, đổi role, thu hồi phiên) phải dùng `ScenarioUsers.CreateAsync` — nhắm vào user mẫu dùng chung sẽ làm token của role đó chết và kéo mọi ca chạy sau đỏ theo.
- `TestDatabaseGuard` chặn cứng việc chạy test vào DB từ xa (Supabase/Railway). **Đừng gỡ.**
- Thêm tích hợp ngoài mới → phải thêm fake trong `ApiTestFactory`, không thì test gọi ra dịch vụ thật.
- CI: `.github/workflows/test.yml` chạy trên mọi PR/push; `deploy.yml` có `needs: test` nên deploy chỉ chạy khi test xanh.

## Chưa làm (đừng giả định đã có)

Python AI recommendation service (contract có ở `FengDeskAI.Contracts`, service chưa tồn tại — chấm điểm chạy in-process .NET), analytics dashboard, Redis distributed cache (đang `AddDistributedMemoryCache`), SignalR hub cho notification (chỉ có `ChatHub`).

**Hai điều doc cũ ghi SAI, đừng lặp lại:** Meshy **không** mock (gọi HTTP thật); GHN/Ahamove **đã** tích hợp thật (provider + webhook 2 chiều, môi trường sandbox). Danh sách được kiểm chứng và giữ cập nhật ở `docs/ard/architecture-core/05-open-items.md`.

Chưa phủ test: trợ lý hội thoại `/api/chat` (phụ thuộc LLM), `parse-description` của workspace (worker bị gỡ trong test nên job không chạy), speech-to-text (đang tắt trong test). Xem bảng "Đang CHƯA phủ" trong README của ApiTests.
