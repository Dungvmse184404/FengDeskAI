# Refactor: Model3D — "1 lần tự động, tạo lại thì thủ công qua staff sàn"

> **v2 — thay thế bản nháp v1 bên dưới.** Các câu hỏi mở ở mục H (v1) đã được chốt qua
> trao đổi với Vu ngày 30/07/2026. File này là bản thiết kế **cuối cùng, chờ duyệt** trước khi code.

## 1. Mục tiêu

- **Request đầu tiên** (product chưa từng có model) → **tự động** gọi Meshy như flow hiện tại,
  nhưng chạy qua **hàng chờ có retry** (không fail ngay khi Meshy hết credit).
- **Request tiếp theo** (product **đã có** model, owner/staff store không ưng ý) → **KHÔNG** tự
  động gọi Meshy nữa. Request vào hàng chờ riêng, chỉ **staff sàn** (`UserRole.Staff` trở lên)
  thấy và xử lý **thủ công**: tự chọn ảnh (được chọn nhiều ảnh) → gọi Meshy → nếu chưa ưng thì
  làm lại → khi ưng thì **accept**, ghi đè model hiện tại.
- Tại **1 thời điểm**, 1 product chỉ có **tối đa 1 request đang mở** (không cho tạo chồng).
- Garden owner có **toggle bật/tắt hiển thị** model 3D, độc lập với việc có model hay không.
- Lỗi "hết credit Meshy" phải **giấu khỏi garden owner/staff store**, chỉ **staff sàn** thấy lý do
  thật; phía owner vẫn thấy "Đang xử lý".

## 2. Actor & quyền (đã xác nhận)

| Actor | Là ai | Quyền |
|---|---|---|
| **Garden owner** | Chủ store (`GardenStoreOwner`) | Tạo request (Initial/Regenerate) cho product của store mình, bật/tắt hiển thị |
| **Garden staff** | Nhân viên riêng của store (`GardenStaffAssignment`, `InvitationStatus.Accepted`). Nhận lời mời → có quyền; **lời mời bị xóa/thu hồi → mất quyền ngay** (đã đúng theo `CanManageAsync` hiện tại, check `Status == Accepted`) | Giống hệt garden owner: tạo request, bật/tắt hiển thị |
| **Staff sàn** | `UserRole.Staff` / `Manager` / `Admin` — role **nền tảng**, KHÔNG gắn với 1 store cụ thể. Đã tồn tại sẵn trong `AuthorizationPolicies.StaffOrAbove`, **chưa từng dùng** cho model-3d | Xem hàng chờ Regenerate, xử lý thủ công (upload nhiều ảnh, gọi Meshy, accept/redo) |

→ Quyền tạo request (cả Initial lẫn Regenerate) dùng chung `CanManageStoreAsync` hiện có
(owner **hoặc** garden staff Accepted **hoặc** Admin) — **không đổi** logic này.

## 3. Nguyên tắc "1 request tại 1 thời điểm"

- Product **chưa có model thành công** → được tạo **1 request Initial**. Trong lúc request đó
  còn `Queued/Processing` → **chặn** tạo thêm (Conflict), giống check hiện tại nhưng áp dụng
  trên bảng request mới thay vì `ProductModel3D.Status`.
- Product **đã có model** (Succeeded) → owner/staff store muốn đổi model → tạo **1 request
  Regenerate**. Trong lúc request đó chưa `Succeeded/Rejected` → **chặn** tạo thêm request mới.
- Không giới hạn **số lần** tạo request Regenerate theo thời gian — chỉ giới hạn **không chồng
  request đang mở**.

## 4. Thiết kế dữ liệu

### 4.1 Vì sao tách bảng mới thay vì thêm field vào `ProductModel3D` (khác v1)

Bản v1 định thêm `RequestedBy/RequestedAt/AssignedStaffId` thẳng vào `ProductModel3D` — đủ cho
1 lần request duy nhất, nhưng **không đủ** cho yêu cầu mới: cần **lịch sử nhiều request** (để
biết đã Regenerate bao nhiêu lần, ai xử lý lần nào), cần **retry với backoff** khi hết credit
(cần `NextAttemptAt` không nên nằm trên bảng "model hiện tại"), và cần **nhiều ảnh nguồn** riêng
cho từng lần thử của staff. → Tách bảng `model3d_requests`, `ProductModel3D` chỉ giữ vai trò
**"kết quả/hiện trạng mới nhất"** (giữ nguyên 1–1 với `Product` như hiện tại).

### 4.2 Bảng mới: `model3d_requests`

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `id` | uuid | PK |
| `product_id` | uuid | FK → `products` |
| `request_type` | string (`Initial` \| `Regenerate`) | Snapshot lúc tạo — quyết định luồng auto hay thủ công |
| `requested_by` | uuid | User tạo request (owner hoặc garden staff) |
| `status` | string | `Queued → Processing → Succeeded/Failed` (Initial) hoặc `AwaitingStaff → StaffInProgress → Succeeded/Rejected` (Regenerate) |
| `source_image_ids` | jsonb (mảng Guid) | **Nhiều ảnh** — bắt buộc ≥ 1. Initial: mặc định ảnh primary nếu bỏ trống. Regenerate: staff chọn khi xử lý |
| `meshy_task_id` | string? | Task hiện tại đang thử (staff có thể thử nhiều lần → field này bị ghi đè mỗi lần thử) |
| `assigned_staff_id` | uuid? | Chỉ set khi staff sàn nhận xử lý (Regenerate) |
| `internal_failure_reason` | string? | **Staff-only**: `InsufficientCredits` \| `GenerationFailed` \| `InvalidImage`... KHÔNG map ra API cho owner |
| `next_attempt_at` | timestamp? | Dùng cho backoff khi `InsufficientCredits` — worker chỉ thử lại sau mốc này |
| `rejected_reason` | string? | Staff từ chối thủ công (tùy chọn, giữ như v1) |
| `created_at`, `updated_at` | timestamp | |

### 4.3 `ProductModel3D` — thêm 1 field

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `is_enabled` | bool, default `true` | Toggle của owner/staff store. **Độc lập với `Status`.** Tắt → FE ẩn hẳn `3DSection`, chỉ còn ảnh 2D (đã chốt) |

`Status/ModelUrl/ThumbnailUrl/Progress/ErrorMessage` giữ nguyên ý nghĩa: phản ánh **request gần
nhất đã Succeeded**. Khi staff accept 1 request Regenerate mới → ghi đè các field này (xóa GLB cũ
qua `IFileStorage.DeleteByUrlAsync` — logic đã có sẵn, không đổi).

## 5. Xử lý lỗi "hết credit Meshy" — đã tra doc Meshy chính thức (docs.meshy.ai/api/errors)

- **Xác nhận**: hết credit trả về **`402 Payment Required`** — xảy ra **ngay khi gọi POST tạo
  task** (`StartImageTo3DAsync`/`StartMultiImageTo3DAsync`), **không phải** lỗi xuất hiện sau
  trong lúc poll (`task_error` khi polling chỉ có các code: `image_too_complex`,
  `model_missing_uv`, `model_insufficient_uv`, `invalid_input`, `moderation_blocked`, `timeout`,
  `format_conversion_failed` — không có `insufficient_credits` ở đây). → Chỉ cần bắt `402` tại
  bước **gửi task**, không cần xử lý gì thêm ở bước poll cho case này.
- `MeshyModel3DGenerator` bắt riêng `HttpStatusCode.PaymentRequired` (402) khỏi các lỗi HTTP khác.
- Khi gặp `402`: `model3d_requests.status` **giữ nguyên** `Queued` (chưa từng vào `Processing` vì
  task chưa tạo được), set `internal_failure_reason = InsufficientCredits`,
  `next_attempt_at = now + backoff` (đề xuất 5–15 phút, cấu hình qua `MeshySettings`). Worker lần
  sau chỉ thử lại các request có `next_attempt_at <= now`.
- Lỗi khác (400 do ảnh lỗi, hoặc task poll về `FAILED` với 1 trong 7 code ở trên) → `status = Failed`
  ngay, **không** retry vô hạn.
- **API cho garden owner/garden staff**: nếu `internal_failure_reason == InsufficientCredits` →
  trả `status = "Processing"` (che giấu), KHÔNG trả field `internal_failure_reason`.
  **API cho staff sàn**: trả nguyên trạng thái thật + lý do.

## 6. Multi-image — áp dụng thống nhất cho CẢ Initial lẫn Regenerate (cập nhật sau khi tra doc Meshy)

- **Đã tra doc chính thức**: Meshy có `POST /openapi/v1/multi-image-to-3d`, nhận `image_urls`
  (mảng, **1 đến 4 ảnh**, cùng 1 object từ nhiều góc), trả `{ "result": taskId }` — **cùng shape**
  với endpoint `image-to-3d` hiện tại. Poll qua `GET /openapi/v1/multi-image-to-3d/:id` (cùng
  cấu trúc `model_urls`, `thumbnail_url`, `status`, `task_error` như endpoint cũ).
- **Đề xuất đơn giản hóa**: vì `multi-image-to-3d` nhận được cả trường hợp 1 ảnh, **dùng 1
  endpoint này cho MỌI trường hợp** (Initial lẫn Regenerate) thay vì giữ 2 method riêng — khớp
  với UI bạn mô tả (cả màn hình store tạo request lẫn màn hình staff xử lý đều có tick ảnh có sẵn
  + up ảnh mới). `IModel3DGenerator.StartImageTo3DAsync(string)` **thay bằng**
  `StartImageTo3DAsync(IReadOnlyList<string> imageUrls)` (1–4 ảnh), bỏ method cũ luôn — không giữ
  2 code path song song. *Nếu bạn muốn giữ endpoint đơn ảnh riêng cho Initial, báo lại.*
- **Giới hạn cứng 1–4 ảnh** (do Meshy quy định) — validate ở tầng Application trước khi gọi Meshy,
  trả `400` rõ ràng nếu > 4 ảnh, không để Meshy trả lỗi rồi mới biết.
- **Nguồn ảnh** (đã chốt): cả bên **store tạo request** lẫn **staff xử lý Regenerate** đều có UI
  **tick ảnh có sẵn** (`product_images`) **+ ô upload ảnh mới**. Ảnh mới upload trong lúc tạo
  request → đề xuất **lưu luôn thành `ProductImage` bình thường** (tận dụng hạ tầng
  `IFileStorage`/`product_images` sẵn có, không tạo cơ chế lưu ảnh riêng cho 3D) — coi như 1 tác
  dụng phụ tốt: sản phẩm có thêm ảnh trong gallery. *Báo lại nếu bạn muốn ảnh này KHÔNG vào gallery
  công khai của sản phẩm.*
- **FE copy** (ghi chú nhỏ, không cần in đậm/nổi bật): hiển thị cạnh khu vực chọn/upload ảnh —
  *"Chụp ảnh nhiều góc độ khác nhau của sản phẩm sẽ cho ra kết quả chuẩn xác hơn."*

## 7. API mới (route riêng, theo yêu cầu)

Route riêng, tách khỏi `ProductsController` — đề xuất `Model3DRequestsController`,
route gốc `/api/model3d-requests` (không lồng trong `/api/products/{id}` vì staff sàn thao tác
qua danh sách hàng chờ chung, không theo từng product).

| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| `POST` | `/api/products/{id}/model-3d/requests` | Owner/GardenStaff/Admin (`CanManageStoreAsync`) | Tạo request (Initial nếu chưa có model, Regenerate nếu đã có). Body: `sourceImageIds[]` (tick ảnh có sẵn) + `newImages[]` (upload file mới, sẽ tạo `ProductImage`) — tổng 1–4 ảnh. Bỏ trống cả 2 → dùng ảnh primary. Chặn nếu đang có request mở (`409 Conflict`) |
| `GET` | `/api/products/{id}/model-3d/requests` | Owner/GardenStaff/Admin | Lịch sử request của 1 product |
| `PATCH` | `/api/products/{id}/model-3d/toggle` | Owner/GardenStaff/Admin | Bật/tắt `is_enabled` |
| `GET` | `/api/model3d-requests?status=AwaitingStaff` | `StaffOrAbove` | Hàng chờ Regenerate cho staff sàn (kèm tên product, store, ảnh). **Không có bước claim/khóa** — request chỉ là "thông báo product A cần làm lại", bất kỳ staff nào cũng thấy và xử lý được |
| `GET` | `/api/model3d-requests?status=Queued&reason=InsufficientCredits` | `StaffOrAbove` | **Mới** — danh sách request Initial đang kẹt do hết credit Meshy, để staff biết cần nạp thêm (đã chốt: staff cần thấy) |
| `POST` | `/api/model3d-requests/{requestId}/generate` | `StaffOrAbove` — bất kỳ staff nào, không cần claim trước | Body: `sourceImageIds[]` + `newImages[]` (1–4 ảnh). Gọi Meshy multi-image → set `meshy_task_id`, `assigned_staff_id = userId` (audit "ai làm gần nhất", không khóa độc quyền), chuyển `status = InProgress` **mang tính thông tin** (không chặn staff khác gọi lại — đã chốt theo đề xuất mục 9.5) |
| `POST` | `/api/model3d-requests/{requestId}/accept` | `StaffOrAbove` | Model hiện tại (đã Succeeded ở Meshy) đạt yêu cầu → ghi đè vào `ProductModel3D`, `request.status = Succeeded` |
| `POST` | `/api/model3d-requests/{requestId}/retry` | `StaffOrAbove` | Chưa ưng ý → chọn lại ảnh (existing + upload mới), gọi lại Meshy. **Không giới hạn số lần** (đã chốt). Xóa GLB nháp của lần thử trước trên Supabase Storage trước khi ghi `meshy_task_id` mới (đã chốt mục 9.8) |
| `POST` | `/api/model3d-requests/{requestId}/reject` | `StaffOrAbove` | Từ chối hẳn (tùy chọn, giữ như v1) |
| `GET` | `/api/products/{id}/model-3d` | Public (giữ nguyên) | Trạng thái/kết quả hiện tại — thêm field `isEnabled` |
| `DELETE` | `/api/products/{id}/model-3d` | Owner/GardenStaff/Admin (giữ nguyên) | Xóa model hiện tại |

> `POST /api/products/{id}/model-3d` **cũ** (gọi Meshy trực tiếp) sẽ bị **loại bỏ** — thay bằng
> `POST .../model-3d/requests` (Initial tự động, Regenerate vào hàng chờ thủ công).

## 8. Việc cần làm khi code (checklist)

- [x] Domain: entity `Model3DRequest` (enum `Model3DRequestType`, `Model3DRequestStatus`, `Model3DFailureReason`), thêm `IsEnabled` vào `ProductModel3D`
- [x] Infra: `Model3DRequestConfiguration`, gộp repo method vào `IProductRepository`/`ProductRepository` (không tách `IModel3DRequestRepository` riêng — nhất quán với cách `ProductModel3D` đã làm)
- [x] Infra: `MeshyModel3DGenerator` — đổi hẳn sang `multi-image-to-3d` (dùng chung Initial/Regenerate), bắt riêng `402 Payment Required` → `InsufficientCreditsException`
- [x] Application: `ProductModel3DService.RequestAsync` (auto Initial / tạo hàng chờ Regenerate), `ToggleAsync`, `ProcessInitialQueueAsync` (worker); `Model3DRequestService` mới cho staff: `GetQueueAsync`, `GenerateAsync`/`RetryAsync` (cùng logic, không claim), `PreviewAsync`, `AcceptAsync`, `RejectAsync`
- [x] Application: `Model3DPollingWorker` gọi `ProcessInitialQueueAsync` — xử lý cả `Queued` (Initial, backoff khi 402) lẫn `Processing` (poll Meshy)
- [x] WebAPI: `Model3DRequestsController` (route riêng `/api/model3d-requests`, policy `StaffOrAbove`)
- [x] WebAPI: cập nhật `ProductsController` (bỏ `POST .../model-3d` cũ, thêm `POST/GET .../model-3d/requests`, `PATCH .../model-3d/toggle`)
- [x] DTO/Mapping: `ProductModel3DResponse` thêm `isEnabled`; `Model3DRequestResponse` (owner, che giấu lỗi hết credit) tách khỏi `Model3DRequestQueueItemResponse` (staff, đầy đủ)
- [x] Cập nhật `docs/api-documents/02-products.md` (mục Model 3D) + file mới `docs/api-documents/26-model3d-requests.md` + `99-appendix-models.md` (3 enum mới)
- [ ] FE: `3DSection.tsx` — gọi `GET /model-3d`, check `isEnabled` trước khi render, ẩn hoàn toàn nếu `false` (chưa làm — sẽ code tiếp theo yêu cầu riêng)
- [ ] **User tự chạy `dotnet ef migrations add AddModel3DRequestFlow`** — sandbox không có .NET SDK nên không tạo được migration/Designer/Snapshot chính xác, xem mục 11.

### Điều chỉnh so với thiết kế gốc (phát hiện lúc code)

- **Bỏ hẳn "xóa GLB nháp" (mục 9.8 bản trước)**: thay vì re-host GLB nháp lên Supabase Storage mỗi
  lần staff `generate`/`retry` rồi phải dọn, dùng thẳng **URL tạm của Meshy** (`GET .../preview`,
  tự hết hạn theo asset retention của Meshy) để staff xem trước. Chỉ khi **accept** mới tải về
  re-host vĩnh viễn. Đơn giản hơn, không tốn storage cho các lần thử bị bỏ, không cần job dọn dẹp.
- **`AcceptAsync` tự poll lại Meshy** (không tin trạng thái cũ trong DB) để chắc chắn task đã
  `SUCCEEDED` trước khi ghi đè `ProductModel3D` — tránh accept nhầm khi task vẫn đang chạy.

## 9. Trạng thái chốt (cập nhật 30/07 — tất cả đã chốt, sẵn sàng code)

1. ~~Claim request~~ → **không cần claim**, bất kỳ staff sàn nào xử lý trực tiếp.
2. ~~Giới hạn số lần retry~~ → **không giới hạn**.
3. ~~Mã lỗi hết credit~~ → **`402 Payment Required`**, trả ngay lúc gọi POST tạo task (không
   phải lỗi poll sau đó). Nguồn: [docs.meshy.ai/api/errors](https://docs.meshy.ai/en/api/errors).
4. ~~API multi-image~~ → `POST /openapi/v1/multi-image-to-3d`, `image_urls` (1–4), cùng shape
   response/poll với `image-to-3d`. Nguồn:
   [docs.meshy.ai/en/api/multi-image-to-3d](https://docs.meshy.ai/en/api/multi-image-to-3d).
   Quyết định dùng **1 endpoint này cho cả Initial lẫn Regenerate** (xem mục 6).
5. ~~Race condition không claim~~ → **theo đề xuất**: `status → InProgress` mang tính thông tin
   khi staff đầu tiên gọi `generate`, không khóa cứng.
6. ~~Staff thấy request Initial kẹt vì hết credit~~ → **có**, thêm filter
   `status=Queued&reason=InsufficientCredits` riêng cho staff sàn (mục 7).
7. ~~Nguồn ảnh~~ → **cả tick ảnh có sẵn lẫn upload ảnh mới**, áp dụng cho cả màn hình store tạo
   request lẫn màn hình staff xử lý. Ảnh mới upload → lưu thành `ProductImage` bình thường.
8. ~~Dọn GLB nháp~~ → **theo đề xuất**: xóa GLB của lần thử trước mỗi khi `retry`, chỉ giữ file
   đang chờ accept.

Không còn điểm mở chặn code. Việc còn lại thuộc về lúc implement (đọc kỹ thêm các param optional
của `multi-image-to-3d` như `target_formats`, `enable_pbr`... nếu muốn tinh chỉnh chất lượng, nhưng
không bắt buộc cho phiên bản đầu).

## 10. Flow end-to-end (tóm tắt)

1. Owner/garden staff bấm "Tạo model 3D" (product chưa có model) → `POST .../model-3d/requests`
   → `RequestType=Initial`, `status=Queued` → worker tự động gọi Meshy, retry nếu hết credit →
   `Succeeded` → ghi vào `ProductModel3D`.
2. Owner không ưng ý → bấm "Yêu cầu tạo lại" → `POST .../model-3d/requests` →
   `RequestType=Regenerate`, `status=AwaitingStaff` (chặn nếu đang có request mở).
3. Staff sàn mở `GET /api/model3d-requests?status=AwaitingStaff` → chọn nhiều ảnh (không cần claim
   trước) → `generate` → `GET .../preview` xem kết quả (URL tạm Meshy) → chưa ưng thì `retry`
   (chọn ảnh khác, không giới hạn số lần) → ưng thì `accept` → tải GLB, re-host storage vĩnh viễn,
   ghi đè `ProductModel3D`, request `Succeeded`.
4. Owner bật/tắt hiển thị bất kỳ lúc nào qua `PATCH .../model-3d/toggle`, không ảnh hưởng dữ liệu
   model đã lưu.

## 11. Chạy migration (bạn tự làm — sandbox code không có .NET SDK)

Mọi thay đổi entity/configuration đã xong (mục 8), nhưng **chưa có migration**. Chạy trên máy có
.NET SDK, trong thư mục `FengDeskAI.WebAPI` (hoặc chỉ định `--project`/`--startup-project`):

```bash
dotnet ef migrations add AddModel3DRequestFlow --project ../FengDeskAI.Infrastructure --startup-project .
dotnet ef database update --project ../FengDeskAI.Infrastructure --startup-project .
```

Migration sẽ: tạo bảng `model3d_requests` (khớp `Model3DRequestConfiguration`), thêm cột
`is_enabled` (bool, default `true`) vào `product_model3ds`. Kiểm tra lại file migration sinh ra
trước khi `database update` — đặc biệt cột `source_image_ids` phải là `uuid[]` (Postgres array),
không phải `jsonb`/`text`.

## 12. Frontend — hoàn tất triển khai (31/07/2026)

3 mảng UI độc lập theo actor (mục 2), repo `FengDeskAI_FE`. Chi tiết flow từng actor + file map đầy
đủ xem `FengDeskAI_FE/docs/adr/model3d-request-flow-fe.md`. Tóm tắt:

- **Khách (public)** — `components/ui/3DSection.tsx` (`Product3DViewer`, r3f/drei) gắn vào
  `ProductDetailPage.tsx`: badge "3D" trên khung ảnh chính, chỉ hiện khi model `Succeeded` +
  `isEnabled`. 404 (chưa có model) coi là bình thường, không phải lỗi.
- **Garden owner/garden staff** — tab "Mô hình 3D" mới trong `EditProductModal.tsx` (dùng chung bởi
  `/seller/:storeId` lẫn `/manager/products`) → `ProductModel3DSection.tsx`: trạng thái model hiện
  tại + toggle hiển thị, tạo request (Initial: picker ảnh 1–4; Regenerate: không cần ảnh), khoá nút
  khi có request đang mở, lịch sử request.
- **Staff sàn** — trang mới `/manager/model3d-queue` (`requireStaffOrAbove`, sidebar "Sản phẩm →
  Hàng chờ Model 3D") → `Model3DQueuePage.tsx` + `Model3DQueueItemModal.tsx`: picker ảnh → generate,
  preview (poll Meshy live, render bằng `Product3DViewer` dùng chung) → accept/retry/reject. Request
  `Initial` hiển thị read-only (chỉ theo dõi, đúng rule backend không cho staff generate/retry loại
  này).

Đã xác minh qua Supabase (project `dgpvgxrrjxjnwnkljnjk`) + `Get-Migration` cục bộ: migration
`AddModel3DRequestFlow` áp dụng đúng vào Postgres local dev; bảng `model3d_requests` + cột
`product_model3ds.is_enabled` tồn tại.

---

## Phụ lục — bản nháp v1 (đã thay thế, giữ lại để tham chiếu lịch sử)

<details>
<summary>Xem bản v1 gốc</summary>

# Yêu cầu sửa chữa: Tách flow Model3D thành "Garden owner request → Staff sàn tạo"

> Mục tiêu: Garden owner **không** tự gọi Meshy nữa. Owner chỉ **gửi yêu cầu** tạo 3D cho 1 product.
> **Staff của sàn** (`UserRole.Staff`, khác garden staff) xem danh sách yêu cầu → bấm **Tạo model3d**
> (mới gọi Meshy). Worker nền vẫn poll Meshy hoàn tất như cũ.

## Bối cảnh hiện tại (đã đọc code)

Flow hiện gộp **request + generate làm một** — owner bấm là chạy Meshy ngay:

1. `POST /api/products/{id}/model-3d` → `ProductModel3DService.GenerateAsync`.
2. Check quyền `CanManageStoreAsync` (admin **hoặc** owner/staff store) → chọn ảnh nguồn → gọi thẳng `_generator.StartImageTo3DAsync` (Meshy) → lưu `ProductModel3D` với `Status = Processing`.
3. `Model3DPollingWorker` poll Meshy → `Succeeded`: tải GLB, re-host storage, ghi `ModelUrl`; hoặc `Failed`.
4. FE đọc `GET /api/products/{id}/model-3d`.

`Model3DStatus` hiện có: `Pending(0) → Processing(1) → Succeeded(2) / Failed(3)`. Quan hệ `Product` 1–1 `ProductModel3D`.

**Vấn đề:** không có khái niệm "yêu cầu chờ xử lý" và không tách quyền owner vs staff sàn.

> Tin tốt: `UserRole.Staff (= 1<<2)` **đã tồn tại** (staff sàn, khác garden staff trong `garden_staff_assignments`)
> → không cần thêm role. Worker nền + tầng Meshy generator **giữ nguyên**, không phải đụng.

(... nội dung v1 đầy đủ xem lịch sử git của file này ...)

</details>
