# Bộ test API tự động + cổng chặn CI

**Trạng thái:** tầng 0 + 1 đã xong · **Ngày:** 2026-08-13

> Kết quả: 241 endpoint được phát hiện tự động, 21 API test + 65 unit test xanh. Không phải sửa
> một dòng code production nào ngoài việc lộ `Program` cho factory.

## Bối cảnh

Trước thay đổi này, repo có:

- `tests/FengDeskAI.UnitTests` — 6 file, 624 dòng, **unit test thuần** (Moq, không dựng HTTP pipeline). Không nằm trong CI.
- `.github/workflows/deploy.yml` — push `main` → SSH vào VPS → `docker compose up -d --build`. **Không có bước test nào.**

Tức là mọi thay đổi push lên `main` đi thẳng ra production. Một lỗi thiếu đăng ký DI, một route trùng, một policy gắn sai — đều chỉ lộ ra khi app đã chạy trên VPS.

Quy mô bề mặt cần bảo vệ: **32 controller, ~241 endpoint**.

## Quyết định

Dựng project `tests/FengDeskAI.ApiTests` chạy **in-process** bằng `WebApplicationFactory<Program>`, và tách CI thành 2 workflow với `test.yml` là cổng chặn của `deploy.yml`.

### Vì sao in-process, không phải gọi HTTP vào server đã deploy

| Tiêu chí | `WebApplicationFactory` (chọn) | Gọi HTTP vào server thật |
|---|---|---|
| Chặn cuộc gọi ra ngoài | Ghi đè được DI → thay `IEmailSender`, `IPaymentGateway`… bằng fake | Không — phải để dịch vụ thật chạy, hoặc dựng mock server riêng |
| Đọc được OTP để test luồng đăng ký/quên mật khẩu | Có (fake `IEmailSender` giữ lại mã) | Không, trừ khi cắm hòm thư thật |
| Thời gian vòng lặp | `dotnet test` là xong | Phải build image + deploy trước mới test được |
| Chi phí hạ tầng CI | 1 container Postgres | Thêm cả stack |

Project test hiện có **đã reference `FengDeskAI.WebAPI`**, nên hướng này không phát sinh phụ thuộc mới về mặt kiến trúc.

### Postgres trên CI

Dùng `services:` container của GitHub Actions thay vì Testcontainers — nhẹ và nhanh hơn trên runner. Đánh đổi: chạy ở local phải tự có Postgres (dev đã có sẵn cho `appsettings.Development.json`).

## Ràng buộc phải xử lý

### 1. An toàn dữ liệu — chốt chặn bắt buộc

`appsettings.json` (base) trỏ `ConnectionStrings:DefaultConnection` vào **Supabase production**. `appsettings.Development.json` đè sang `localhost` nên **chạy local đã an toàn**, nhưng:

- Cả hai file đều **gitignore** → CI không có file nào cả → config rơi về base → **trỏ vào production**.
- Ai đó chạy `dotnet test` mà quên set biến môi trường cũng rơi vào đúng trường hợp đó.

→ `ApiTestFactory` **fail-fast**: connection string chứa `supabase.com` (hoặc không phải localhost/host được whitelist) thì ném lỗi ngay, không chạy một test nào. Đây là chốt chặn không được gỡ.

### 2. Background worker

6 worker chạy lúc khởi động (`AiBotWorker`, `WorkspaceIntakeWorker`, `OrderExpirationWorker`, `CarrierShopSyncWorker`, `ReturnSlaWorker`, `GeoAutoSyncWorker`). Chúng gọi mạng ngoài và **sửa dữ liệu giữa lúc test chạy** → gỡ toàn bộ `IHostedService` trong factory.

`GeoAutoSyncWorker` đặc biệt phiền: gọi open-api.vn lúc khởi động nếu DB < 63 tỉnh → tắt thêm bằng `Seeding:AutoGeoSync=false`.

### 3. Dịch vụ ngoài

Hiện chỉ 2/9 có công tắc mock sẵn:

| Dịch vụ | Interface | Mock sẵn |
|---|---|---|
| Shipping (GHN/Ahamove) | `IShippingProvider` | ✅ `Shipping:Provider=Mock` |
| AI Recommendation | `IAiRecommendationClient` | ✅ `AiRecommendationSettings:UseMock` |
| SMTP | `IEmailSender` | ❌ cần fake |
| PayOS | `IPaymentGateway` | ❌ cần fake |
| Supabase Storage | `IFileStorage` | ❌ cần fake |
| LLM relay | `IAiChatClient` | ❌ cần fake |
| Meshy | `IModel3DGenerator` | ❌ cần fake |
| Google | `IGoogleTokenValidator` | ❌ cần fake |
| Speech-to-text | `ISpeechToTextService` | ❌ cần fake |

Tất cả đều đã nằm sau interface → thay được trong factory, **không phải sửa một dòng code production nào**.

Fake `IEmailSender` không chỉ để chặn gửi mail — nó **giữ lại OTP** để test chạy trọn luồng đăng ký / quên mật khẩu / đổi email, thứ trước đây không tự động hóa được.

### 4. `Program.cs` dùng top-level statements

`WebApplicationFactory<Program>` cần type `Program` public. Thêm `public partial class Program { }` ở cuối file — một dòng, không đổi hành vi.

### 5. Hai cái bẫy phát hiện lúc triển khai

**a) Ép config phải qua BIẾN MÔI TRƯỜNG, không dùng `ConfigureAppConfiguration`.**
Callback đó chỉ chạy khi host được build, tức **sau** thân `Program.cs`. Mà `Program.cs` gọi
`AddInfrastructure(builder.Configuration, …)`, trong đó `JwtSettings` được đọc **ngay tại chỗ** để
dựng `IssuerSigningKey`. Hệ quả: app verify token bằng secret trong `appsettings.json` nhưng
`IOptions<JwtSettings>` (resolve lúc chạy) ký bằng secret của test → **mọi request có token đều 401**.
Triệu chứng rất dễ đọc nhầm thành "phân quyền hỏng". `WebApplication.CreateBuilder` nạp biến môi
trường ngay lúc khởi tạo nên đặt ở đó thì cả hai phía thấy cùng giá trị.

**b) Action có tham số `IFormFile` bị ràng buộc `multipart/form-data`** do `[ApiController]` tự suy
ra — và ràng buộc này **không** xuất hiện dưới dạng `ConsumesAttribute` trong `EndpointMetadata`, phải
dò theo kiểu tham số. Gửi sai content-type thì khâu **chọn action** trả 415 ngay trong `UseRouting`,
trước cả khi `AuthorizationMiddleware` chạy → không bao giờ thấy 401/403.

## Chia tầng

Phủ happy path cho đủ 241 endpoint là không khả thi trong khung thời gian capstone, và cũng không đáng. Chia tầng để có cổng chặn sớm:

| Tầng | Nội dung | Phủ | Ước lượng |
|---|---|---|---|
| **0** | Hạ tầng: factory, fixture DB, fake external, `AuthHelper` cho 5 role | — | 2–3 ngày |
| **1** | Smoke + ma trận quyền, **tự sinh từ `/swagger/v1/swagger.json`**: không token → 401; sai role → 403; đúng role → không 500 | **100% endpoint** | 1–2 ngày |
| 2 | Happy path 8–10 kịch bản xuyên suốt theo bounded context | luồng ra tiền | 4–6 ngày |
| 3 | Nhánh lỗi & biên | tùy thời gian | — |

**Đợt này làm tầng 0 + 1.** Lý do: tầng 1 rẻ nhất mà bắt được đúng nhóm lỗi làm sập app khi deploy (thiếu DI, route trùng, policy sai) — vốn là thứ hiện không có gì chặn.

## CI

```
test.yml    ← chạy trên mọi PR và push. Postgres service container.
              dotnet build → UnitTests → ApiTests
deploy.yml  ← thêm `needs: test`, chỉ chạy khi test xanh
```

Chặn merge cần bật **branch protection** cho `main` trên giao diện GitHub (bắt buộc PR + status check `test`) — thao tác thủ công, không cấu hình được từ repo.

## Hệ quả

**Được:**
- Lỗi sập app bị chặn trước khi ra VPS, không phải sau.
- Luồng OTP (đăng ký, quên mật khẩu, đổi email) lần đầu tiên test tự động được.
- Có chốt chặn khỏi việc vô tình chạy test lên DB production.

**Mất / phải chấp nhận:**
- Chạy test ở local cần Postgres (dev đã có).
- Mỗi khi thêm dịch vụ ngoài mới, phải viết fake tương ứng, không thì test đỏ.
- Tầng 1 chỉ khẳng định "không nổ", **không** khẳng định nghiệp vụ đúng. Đừng nhầm test xanh với tính năng chạy đúng.

## Liên quan

- [`docs/ard/bounded-contexts/identity.md`](../ard/bounded-contexts/identity.md) — luồng auth mà tầng 2 sẽ phủ trước tiên
- [`docs/api-documents/01-authentication.md`](../api-documents/01-authentication.md)
