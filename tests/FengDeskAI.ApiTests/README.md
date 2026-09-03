# FengDeskAI.ApiTests

Integration test chạy API **in-process** (`WebApplicationFactory<Program>`) — dựng nguyên pipeline
HTTP thật, chỉ thay dịch vụ ngoài bằng fake. Thiết kế & lý do: [`docs/adr/api-integration-testing.md`](../../docs/adr/api-integration-testing.md).

## Chạy ở local — cấu hình một lần

Cần một Postgres **cục bộ** và một DB rỗng dành riêng cho test:

```bash
createdb -h localhost -U postgres fengdeskai_test
```

Rồi copy file mẫu và sửa mật khẩu Postgres của bạn:

```bash
cp tests/FengDeskAI.ApiTests/appsettings.Testing.json.example tests/FengDeskAI.ApiTests/appsettings.Testing.json
```

Xong. Từ giờ chạy được bằng `dotnet test`, nút Run trong Visual Studio / Rider, không cần set gì thêm:

```bash
dotnet test FengDeskAI.slnx
```

`appsettings.Testing.json` đã nằm trong `.gitignore` (mẫu `**/appsettings.*.json`) nên mật khẩu của
bạn không lên git. Mỗi người tự tạo file của mình.

### Thứ tự tìm connection string

1. Biến môi trường `ConnectionStrings__DefaultConnection` — **luôn thắng**, CI dùng đường này
2. File `appsettings.Testing.json` cạnh test assembly
3. Không có cả hai → dừng ngay kèm thông báo hướng dẫn

**Không có giá trị mặc định trong code** — không bao giờ âm thầm chạy vào một DB nào đó.

## Chốt chặn an toàn

`TestDatabaseGuard` từ chối chạy nếu connection string không trỏ vào host cục bộ — chặn cứng
`supabase.com`, `railway.app`, và mọi host lạ. Test tạo/sửa/xóa dữ liệu thật, chạy nhầm vào DB
production là mất dữ liệu. **Đừng gỡ chốt này.**

Lý do nó cần thiết: `appsettings.json` (base) trỏ vào Supabase production, `appsettings.Development.json`
mới đè sang localhost — mà cả hai đều gitignore. Trên CI không có file nào cả, config sẽ rơi về base.

## Cấu trúc

| Thư mục | Nội dung |
|---|---|
| `Infrastructure/` | Factory, fixture, chốt chặn DB, fake dịch vụ ngoài, liệt kê endpoint |
| `Endpoints/` | Test thật, nhóm theo chủ đề |

Giá trị nhạy cảm (JWT secret, mật khẩu user mẫu, webhook secret) đều **sinh ngẫu nhiên mỗi lần chạy**
qua `TestSecrets` — không có secret nào nằm trong mã nguồn.

## Đang phủ những gì

**356 test**, chia hai tầng. Tầng 1 khẳng định **"không nổ"**; tầng 2 khẳng định **nghiệp vụ đúng**.

### Tầng 0-1 — hạ tầng & bề mặt

| Test | Tầng | Nội dung |
|---|---|---|
| `AuthHelperTests` | 0 | Kiểm chính hạ tầng: token của cả 5 role gọi được `/api/Auth/me`. **Đỏ ở đây thì mọi kết luận khác vô nghĩa** |
| `TestDatabaseGuardTests` | 0 | Chính chốt chặn DB |
| `AuthorizationMatrixTests` | 1 | Toàn bộ endpoint × mỗi bộ dữ liệu: không token → 401 · sai role → 403 · endpoint public không được 401/403. **Cố ý bỏ qua route webhook** (dùng cơ chế xác thực riêng) — đúng/sai secret là việc của `ShippingFlowTests` |
| `SmokeTests` | 1 | Gọi thật mọi endpoint GET không tham số → không được 5xx; swagger sinh được |
| `RobustnessTests` | 1 | Bắn dữ liệu rác vào toàn bộ endpoint → phải từ chối bằng 4xx, không được 5xx |

### Tầng 2 — nghiệp vụ

| Test | Bounded context | Nội dung |
|---|---|---|
| `FunctionalCaseTests` | nhiều | Ca một-request đọc từ `TestData/cases/*.json` |
| `AuthFlowTests` | Identity | Đăng ký 3 bước, quên mật khẩu, đổi email 4 bước, đăng nhập Google |
| `SalesFlowTests` | Sales | Giỏ hàng → tách đơn theo vườn → thanh toán → hủy đơn → giao hàng |
| `ReturnFlowTests` | Returns | RMA: tạo yêu cầu → staff duyệt/từ chối → hoàn tiền, kèm phân quyền |
| `SlaSweepTests` | Returns | Các lượt quét SLA: quá hạn bằng chứng, điều phối/timeout/retry/leo thang lệnh hoàn tiền, tự chốt công nợ |
| `VendorLiabilityFlowTests` | Returns | Công nợ nhà cung cấp: sinh ra sau hoàn tiền → vườn phản đối → quản lý phán quyết |
| `CatalogFlowTests` | Catalog | Sản phẩm (CRUD, biến thể, SKU, ảnh, danh mục, phong thủy, vector ngũ hành), danh mục, tag |
| `Model3DFlowTests` | Catalog | Hàng chờ model 3D: gửi yêu cầu → staff sinh → xem trước → chấp nhận/từ chối/retry |
| `StoreFlowTests` | Vendor | Tự đăng ký cửa hàng, địa chỉ, đồng sở hữu, vòng đời lời mời nhân viên, thống kê |
| `AdminFlowTests` | Identity | Quản trị user: tra cứu, khóa/mở, đổi vai trò, thu hồi phiên, nhật ký kiểm toán |
| `ScoringConfigFlowTests` | CustomerCare | Tham số chấm điểm, ánh xạ input → ngũ hành, hệ số mục đích, vector loại phòng |
| `WorkspaceFlowTests` | Workspace | Hồ sơ không gian: vòng đời, độ đầy đủ, phân tích ngũ hành, đặt sản phẩm đã mua |
| `CustomerCareFlowTests` | CustomerCare | Gợi ý theo không gian & vật phẩm mang theo, đánh giá sản phẩm |
| `ShippingFlowTests` | Shipping | Webhook nhà vận chuyển (secret, trạng thái hợp lệ), thông báo, tiến trình giao, sẵn sàng giao |
| `AddressAndTaxonomyFlowTests` | Sales / Catalog | Sổ địa chỉ khách; bảng tra cứu hành / phong cách / vibe |

### Đang CHƯA phủ — đừng giả định đã có

| Phần | Vì sao |
|---|---|
| Trợ lý hội thoại (`/api/chat`) | Phụ thuộc LLM; `FakeAiChatClient` không gọi tool nên vòng lặp tool-calling không chạy |
| `POST /api/workspace/parse-description` | Giao việc cho `WorkspaceIntakeWorker`, mà worker đã bị gỡ trong test → trạng thái đứng ở `pending` mãi |
| `POST /api/workspace/transcriptions` | `Speech__Enabled=false` nên luôn 503 |
| Chốt "admin cuối cùng" (409) | `AdminUserSeeder` tạo sẵn một admin, cộng admin của fixture là đã ≥ 2 → nhánh không chạm tới. Để cho unit test |
| Sinh 3D thật (Meshy) | Đang mock |
| Python AI recommendation service | Đang mock (`AiRecommendationSettings__UseMock=true`) |

## Hạ tầng dùng lại được

| Thành phần | Dùng để |
|---|---|
| `SalesScenario` | Phường/xã có mã GHN, cửa hàng đủ điều kiện giao, sản phẩm, địa chỉ khách |
| `DeliveredOrderScenario` | Một đơn đã đi hết vòng tới trạng thái đã giao (đi qua API thật) |
| `RefundedReturnScenario` | Một RMA đã hoàn tiền xong, kèm khoản công nợ sinh theo |
| `ScenarioUsers` | User dùng một lần — **bắt buộc** với ca có tác dụng phụ lên phiên đăng nhập (xem bẫy 3) |
| `ApiEnvelope` | Đọc `data` / `message` của phong bì phản hồi |
| `VendorLiabilityMaintenance` | Kéo hạn phản đối công nợ về quá khứ |

## Thêm tích hợp ngoài mới thì phải làm gì

Thêm fake tương ứng vào `Infrastructure/FakeExternals.cs` và đăng ký trong
`ApiTestFactory.ReplaceExternalServices`. Bỏ qua bước này thì test sẽ gọi ra dịch vụ thật — chậm,
không ổn định, và có thể tốn tiền.

## Bốn cái bẫy đã gặp, đừng dẫm lại

1. **Ép config phải qua biến môi trường, không dùng `ConfigureAppConfiguration`.** Callback đó chạy
   sau thân `Program.cs`, mà `Program.cs` đọc `JwtSettings` ngay lúc đó để dựng `IssuerSigningKey`
   → app ký token bằng secret này nhưng verify bằng secret khác → mọi request có token đều 401.
2. **Action có tham số `IFormFile` bị ràng buộc `multipart/form-data`** do `[ApiController]` tự suy
   ra. Gửi sai content-type thì khâu chọn action trả 415 trước cả phân quyền — không bao giờ thấy
   401/403.

3. **Thao tác đổi `TokenVersion` phải nhắm vào user dùng một lần.** `POST /api/stores`,
   `PATCH /api/admin/users/{id}/status`, `PUT .../roles` và `POST .../revoke-sessions` đều tăng
   `TokenVersion` và thu hồi refresh token của người bị tác động. Nhắm vào user mẫu dùng chung của
   `ApiTestFixture` sẽ làm token của role đó chết, và MỌI ca test chạy sau đó 401 theo — lỗi phụ
   thuộc thứ tự chạy nên cực khó lần. Dùng `ScenarioUsers.CreateAsync` thay vì `ClientFor(role)`.

4. **`new MultipartFormDataContent()` rỗng không phải request hợp lệ.** Thân request không có
   section nào, ASP.NET Core từ chối ngay ở khâu đọc form với 400 *"Form section has invalid
   Content-Disposition value"* — chưa vào tới action, nên test tưởng nghiệp vụ sai trong khi lỗi
   nằm ở chính request. Phải có ít nhất một section; xem `Model3DFlowTests.EmptyForm()`.
