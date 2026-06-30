# FengDeskAI — Project Proposal

> **Đề tài:** Hệ thống thương mại điện tử gợi ý sản phẩm trang trí bàn làm việc theo phong thủy có tích hợp AI
> **Mã đề tài:** SEP490 — SU26SE093
> **Cập nhật:** 2026-06-21
> **Trạng thái:** Backend cốt lõi hoàn thiện · AI chat hoạt động · AI recommendation đang dùng mock · Frontend React (repo riêng)

---

## 1. Tóm tắt (Executive Summary)

FengDeskAI là nền tảng thương mại điện tử chuyên bán **vật phẩm trang trí bàn làm việc** (cây để bàn, đèn, vật phẩm phong thủy…) với điểm khác biệt là **engine gợi ý theo phong thủy** kết hợp **trợ lý AI hội thoại**. Người dùng khai báo hồ sơ không gian làm việc (mệnh, hướng bàn, ánh sáng, mục đích sử dụng…); hệ thống chấm điểm sản phẩm bằng luật phong thủy *tất định* (deterministic) trong .NET, sau đó AI **chỉ diễn giải thuyết phục** dựa trên các "sự thật" đã chấm — không tự bịa luật hay thêm/bớt sản phẩm.

Hệ thống được xây theo **Clean Architecture** trên .NET 8, dùng PostgreSQL (Supabase), tích hợp thanh toán PayOS, lưu trữ ảnh Supabase Storage, chat thời gian thực qua SignalR, và LLM hội thoại qua Ollama.

---

## 2. Bối cảnh & Vấn đề

- Thị trường vật phẩm phong thủy/trang trí bàn làm việc lớn nhưng **người mua thiếu kiến thức** để chọn sản phẩm "hợp mệnh, hợp không gian".
- Tư vấn phong thủy truyền thống **chủ quan, không nhất quán**, khó mở rộng quy mô.
- Các sàn TMĐT phổ thông **không cá nhân hóa theo phong thủy**, không giải thích "vì sao hợp".

**Giải pháp:** số hóa luật phong thủy thành engine chấm điểm minh bạch + dùng AI để diễn giải dễ hiểu, kèm trải nghiệm mua sắm & chăm sóc khách hàng đầy đủ.

---

## 3. Mục tiêu & Phạm vi

### Mục tiêu
1. Cho phép khách tạo **hồ sơ không gian làm việc** và nhận **gợi ý sản phẩm hợp phong thủy** kèm lý do.
2. Cung cấp **trợ lý AI hội thoại** tra cứu dữ liệu thật (hồ sơ, sản phẩm, đơn hàng) qua tool-calling.
3. Vận hành đầy đủ luồng TMĐT: giỏ hàng → đặt hàng → thanh toán → giao hàng → đánh giá.
4. Hỗ trợ **đa cửa hàng (multi-vendor)** với phân quyền nhân viên.
5. Kênh **chăm sóc khách hàng** realtime (người ↔ người và người ↔ AI).

### Ngoài phạm vi (giai đoạn này)
- App mobile native.
- Tích hợp hãng vận chuyển thật (hiện mô phỏng qua webhook).
- Marketplace mở cho người bán bên thứ ba tự đăng ký.

---

## 4. Đối tượng người dùng & Phân quyền

Thứ tự quyền tăng dần: **Customer < Staff < Manager < Admin** (policy `...OrAbove` = vai trò đó và mọi vai trò cao hơn).

| Vai trò | Năng lực chính |
|---------|----------------|
| **Customer** | Tạo hồ sơ không gian, nhận gợi ý, mua hàng, chat AI/hỗ trợ, đánh giá |
| **Staff** | Trực hàng đợi hỗ trợ, tra thông tin khách (theo consent), xử lý đơn của cửa hàng |
| **Manager** | Quản lý cửa hàng, sản phẩm, thuộc tính phong thủy, nhân viên |
| **Admin** | Quản trị toàn hệ thống, luật phong thủy, loại không gian hệ thống |

---

## 5. Kiến trúc & Công nghệ

### 5.1 Clean Architecture (4 + 1 layer)
```
WebAPI  ──►  Application  ──►  Domain
   │              │
   └──►  Infrastructure ◄──────┘
                  │
            Contracts (DTO chia sẻ với AI microservice)
```
- **Domain** — entity, enum, quy tắc nghiệp vụ thuần.
- **Application** — feature folder theo bounded context (Identity, Workspace, Catalog, Vendor, Geography, Sales, Payment, Shipping, Chat, CustomerCare, Announcement); chứa engine phong thủy & service.
- **Infrastructure** — EF Core/Npgsql, tích hợp ngoài (PayOS, Supabase Storage, Ollama, Meshy, SMTP).
- **WebAPI** — controllers, SignalR hub, workers nền, authz policies.
- **Contracts** — hợp đồng request/response với AI recommendation service (Python).

### 5.2 Stack công nghệ
| Hạng mục | Công nghệ |
|----------|-----------|
| Backend | .NET 8, ASP.NET Core Web API |
| ORM / DB | EF Core 8 + Npgsql · PostgreSQL (Supabase) |
| Realtime | SignalR (`/hubs/chat`) |
| Auth | JWT Bearer, refresh token, OTP qua email (MailKit/SMTP) |
| Thanh toán | PayOS |
| Lưu trữ ảnh | Supabase Storage |
| LLM hội thoại | Ollama (qwen3.5, gemma3…) qua HTTP, hỗ trợ tool-calling |
| Sinh model 3D | Meshy (image-to-3D) — hiện mock |
| Frontend | React (repo riêng: `FengDeskAI_FE`) |
| Triển khai | Docker · Railway (xem README) |

### 5.3 Phân vai AI (nguyên tắc cốt lõi)
> .NET tính **luật phong thủy + chấm điểm tất định** và sinh `matchFacts`/`cautionFacts`. AI microservice **chỉ viết lời thuyết phục** dựa trên facts và được phép **hoán vị trong top-N**, **cấm** thêm/bớt sản phẩm hay bịa luật. Lưu `BaseRank` vs `FinalRank` để kiểm toán AI.

**Công thức điểm:**
```
Score = PersonalWeight × (w_element·elementMatch + w_dir·directionMatch)
                        + (w_purpose·purposeMatch + w_style·styleMatch + w_light·lightingFix + w_size·sizeFit)
```
- `PersonalWeight` chỉ nhân phần **cá nhân** (mệnh + hướng), không nhân phần chức năng.
- Phần cá nhân **chỉ tính khi giới tính là Male/Female** (cần để tính Kua); ngược lại bỏ qua, chỉ chấm theo chức năng.
- `elementMatch` tra bảng `feng_shui_rules` (25 cặp ngũ hành): Tỷ hòa +1.0, Tương sinh +0.8, Tiết khí −0.2, Tương khắc +0.2, Bị khắc −1.0.

---

## 6. Tính năng ĐÃ hoàn thành

### 6.1 Định danh & Bảo mật
- Đăng ký 3 bước (Initiate / Verify-OTP / Finalize), OTP lưu `IDistributedCache` (không lưu DB), có rate-limit & cooldown.
- Đăng nhập JWT + refresh token; phân quyền 4 cấp qua policy.

### 6.2 Hồ sơ không gian làm việc (input cho AI)
- CRUD `WorkspaceProfile` (vị trí, phong cách, ánh sáng, loại bàn, hướng bàn, hướng phòng, mục đích, mệnh, diện tích, mặc định).
- `WorkspaceType` hệ thống + tùy biến của người dùng (CRUD), có `PersonalWeight`.

### 6.3 Catalog & Thuộc tính phong thủy
- Sản phẩm, biến thể (`ProductItem`), ảnh, danh mục, tag.
- Lookup: Element (ngũ hành), Style, Vibe; bảng nối Product↔Element/Style/Vibe.
- Quản lý thuộc tính phong thủy sản phẩm: `PUT /api/products/{id}/feng-shui`.
- Tìm kiếm **không phân biệt dấu/hoa thường**, quét cả tên lẫn mô tả (PostgreSQL `unaccent` + `ILike`).

### 6.4 Engine gợi ý phong thủy
- `FengShuiCalculator` (Nạp Âm Can-Chi, Kua, hướng tốt, quan hệ ngũ hành).
- `RecommendationScorer` (chấm điểm tất định + sinh facts/cautions, gate theo giới tính).
- Seeder: 7 loại không gian + 25 luật ngũ hành.
- `POST/GET /api/recommendations` — orchestrate trong transaction, lưu `Recommendation`/`Items`/`Logs` (audit request/response/ms).
- Hợp đồng AI (`Contracts/Recommendation/`) + `MockAiRecommendationClient`; toggle `UseMock` để nối service thật.

### 6.5 Trợ lý AI hội thoại (tool-calling)
- Chat đồng bộ với AI: `POST /api/chat/ai/messages` (kèm `productId` context, ảnh).
- **8 tool** model gọi để lấy dữ liệu thật: `get_my_profile`, `list_my_workspaces`, `get_product`, `search_products`, `recommend_products`, `list_my_orders`, `get_payment_status`, `get_chat_partner_info`.
- Vòng lặp tool-calling, chống mất content, chống tràn context (`num_ctx`).
- Hỗ trợ **ảnh "dính" hội thoại** kiểu Messenger (vision, ngân sách N ảnh gần nhất).
- Prompt kỹ thuật bằng tiếng Anh, AI **trả lời khách bằng tiếng Việt**.

### 6.6 Chat thời gian thực & Chăm sóc khách hàng
- SignalR hub: join/leave/gửi tin/đánh dấu đã đọc; event `messageReceived`, `chatboxRead`, `aiStatus`.
- Mô hình phòng suy từ participants: phòng AI riêng, phòng 1-1, phòng nhóm; AI trả lời khi được `@AI`.
- **Phòng hỗ trợ**: khách mở phòng, nhân viên trực hàng đợi (`GET /api/chat/support/open`), self-join.
- **Trạng thái AI realtime** (thinking / calling_tool / writing / done).
- **Consent chia sẻ dữ liệu** (profile/workspaces/orders) enforced ở code, không chỉ ở prompt.

### 6.7 Bán hàng, Thanh toán, Giao hàng
- Giỏ hàng, đặt hàng, log trạng thái đơn.
- Thanh toán PayOS + COD; worker hết hạn đơn online chưa thanh toán → Expired.
- Giao hàng + webhook tiến trình (`ShippingWebhook`).

### 6.8 Đa cửa hàng, Địa lý, Thông báo, Đánh giá
- `GardenStore`, phân công nhân viên, địa chỉ cửa hàng.
- Tỉnh/Huyện/Xã + địa chỉ người dùng.
- Thông báo (`Notification`) + đánh giá sản phẩm (`Review`).

### 6.9 Hạ tầng triển khai
- **Dockerfile** multi-stage + **docker-compose** + **.env** tách secret; sẵn sàng push **Railway** (bind `$PORT` động). Chi tiết ở README.

---

## 7. Tính năng CHƯA thực hiện / đang dở

> Đây là phần trọng tâm để lập kế hoạch giai đoạn tiếp theo.

### 7.1 AI Recommendation microservice (Python) — **chưa build**
- Hiện dùng `MockAiRecommendationClient` (giữ nguyên thứ tự engine, diễn giải đơn giản).
- Cần: dựng service FastAPI nhận `AiRecommendationRequest`, gọi LLM viết lời thuyết phục từ `matchFacts`, hoán vị top-N, trả `AiRecommendationResponse`; đổi `UseMock=false` để nối.
- **Lưu ý:** các "facts" hiện sinh bằng tiếng Việt trong `RecommendationScorer` — cần quyết định ngôn ngữ thống nhất với prompt service.

### 7.2 Sinh mô hình 3D sản phẩm (Meshy) — **mock**
- Entity `ProductModel3D` + `Model3DPollingWorker` + `MeshySettings` đã có, nhưng `UseMock=true`, `ApiKey` rỗng.
- Cần: API key thật, kiểm thử luồng image-to-3D end-to-end, hiển thị model-viewer ở FE.

### 7.3 After-sales / Đổi-trả-bảo hành — **chưa có**
- Trong scope ban đầu (`after_sales_requests`) nhưng chưa có entity/luồng. Cần thiết kế quy trình yêu cầu đổi/trả/hoàn tiền.

### 7.4 Support ticket có cấu trúc — **chưa có**
- Hiện hỗ trợ qua phòng chat; chưa có `support_tickets` (mã ticket, trạng thái, SLA, phân loại). Phù hợp khi cần báo cáo/đo lường CSKH.

### 7.5 Analytics & Dashboard — **chưa có**
- `daily_revenue_summaries` và báo cáo doanh thu/đơn/sản phẩm bán chạy chưa triển khai. Cần cho Manager/Admin ra quyết định.

### 7.6 Tích hợp vận chuyển thật — **mô phỏng**
- Webhook dùng secret dev; chưa nối hãng vận chuyển (GHN/GHTK…) thật để lấy phí & trạng thái.

### 7.7 Kiểm thử tự động & CI/CD — **chưa có**
- **Solution không có test project**; `.github/` rỗng (không có pipeline).
- Cần: unit test cho engine phong thủy (giá trị cao, logic tất định dễ test), integration test API trọng yếu, GitHub Actions build/test/deploy.

### 7.8 Hoàn thiện luồng đa cửa hàng & vận hành
- Entity multi-store đã có nhưng các luồng quản trị (gán nhân viên, phân bổ đơn theo cửa hàng, tồn kho theo store) còn cần hoàn thiện & UI.

### 7.9 Hậu kỳ bảo mật (xem mục 9)
- Xoay (rotate) toàn bộ secret từng để plaintext; gỡ "cửa hậu" trong system prompt; rà soát phân quyền tool theo vai trò.

---

## 8. Lộ trình đề xuất (Roadmap)

| Giai đoạn | Hạng mục | Kết quả mong đợi |
|-----------|----------|------------------|
| **P1 — Hoàn thiện AI** | Dựng Python recommendation service, nối `UseMock=false`, thống nhất ngôn ngữ facts | Gợi ý có lời giải thích chất lượng, có thể kiểm toán |
| **P2 — Vận hành TMĐT** | After-sales, support ticket, tích hợp vận chuyển thật | Quy trình sau bán & giao hàng khép kín |
| **P3 — Quản trị & dữ liệu** | Analytics/dashboard, hoàn thiện multi-store | Manager/Admin có số liệu ra quyết định |
| **P4 — Chất lượng** | Test engine + API, CI/CD, rà soát bảo mật | Độ tin cậy & khả năng release tự động |
| **P5 — Trải nghiệm** | 3D viewer thật, tinh chỉnh UX FE | Khác biệt hóa sản phẩm |

---

## 9. Rủi ro & Giảm thiểu

| Rủi ro | Ảnh hưởng | Giảm thiểu |
|--------|-----------|------------|
| **Secret từng lưu plaintext** (DB, JWT, PayOS, Supabase service_role, app-password Gmail) | Cao | Đã tách `.env`/Railway Variables; **cần rotate toàn bộ key** |
| **"Cửa hậu" trong system prompt** (từ khóa khiến AI làm mọi yêu cầu, lộ lỗi) | Cao | Gỡ bỏ chỉ thị này khỏi `CoreDirective` |
| Phụ thuộc LLM ngoài (Ollama qua ngrok) | Trung bình | Cấu hình endpoint ổn định, có fallback/timeout |
| Không có test & CI | Trung bình | Bổ sung test engine + pipeline (P4) |
| AI bịa luật phong thủy | Trung bình | Đã ràng buộc "chỉ giải thích từ facts" + audit BaseRank/FinalRank |
| Quá tải context model nhỏ | Thấp | `num_ctx` mở rộng, giới hạn lịch sử/ảnh |

---

## 10. Triển khai

Hệ thống đã được container hóa và sẵn sàng deploy lên Railway:
- `Dockerfile` multi-stage (.NET 8), chạy non-root, bind `$PORT` động.
- `docker-compose.yml` (service `api` + service `migrate` chạy seeding/migration).
- Secret nạp từ env var (`Section__Key`), tách khỏi `appsettings.json`.

Xem hướng dẫn chi tiết trong [README.md](../README.md).

---

*Tài liệu này phản ánh trạng thái mã nguồn tại thời điểm cập nhật; phần "chưa thực hiện" được xác định bằng đối chiếu scope thiết kế với code/migration hiện có.*
