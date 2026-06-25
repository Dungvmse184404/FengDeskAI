# CLAUDE.md

Hướng dẫn cho Claude khi làm việc trong repo backend **FengDeskAI**.

## Dự án là gì

Nền tảng **e-commerce bán sản phẩm/vật phẩm phong thủy** (chủ yếu cây & đồ trang trí bàn làm việc), kèm:

- **Tư vấn bằng AI**: người dùng mô tả không gian làm việc (mệnh/ngũ hành, hướng bàn, ánh sáng, mục đích…). Một **engine chấm điểm phong thủy deterministic viết bằng .NET** xếp hạng sản phẩm; lớp AI **chỉ giải thích** kết quả một cách thuyết phục — **không tự bịa luật, không thêm/bớt sản phẩm**.
- **Trợ lý hội thoại** (chat) dùng LLM với tool-calling.
- **Hiển thị model 3D** của vật phẩm (optional) — sinh từ ảnh qua Meshy (hiện đang mock).

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

### Feng-shui engine (cốt lõi nghiệp vụ)
`Application/Features/CustomerCare/Engine/`: `FengShuiCalculator`, `RecommendationScorer`, `ScoringModels`. Engine chấm điểm **deterministic** — khi sửa logic gợi ý, sửa ở đây, KHÔNG để AI quyết định thứ hạng.

### AI tools
`Application/Features/CustomerCare/Tools/`: mỗi tool LLM gọi được là 1 class (vd `RecommendProductsTool`, `SearchProductsTool`, `GetMyProfileTool`…). Tool chỉ đọc dữ liệu đã được phân quyền (xem `ChatRoomDataConsent`).

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

## Chưa làm (đừng giả định đã có)

Python AI recommendation service (đang mock), sinh 3D thật (Meshy mock), automated tests & CI/CD, analytics dashboard, tích hợp đơn vị vận chuyển thật.
