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

## Quyết định thiết kế (đề xuất)

1. **Hai bước, hai quyền:**
   - **Garden owner**: `Tạo yêu cầu` 3D cho product của store mình → bản ghi `Status = Requested`. *Không* gọi Meshy.
   - **Staff sàn**: xem hàng đợi `Requested` → bấm `Tạo model3d` → mới gọi Meshy → `Status = Processing`.
2. **Thêm trạng thái** vào `Model3DStatus`:

   | Status | Ý nghĩa | Ai đẩy |
   |---|---|---|
   | `Requested` (mới) | Owner đã yêu cầu, chờ staff sàn nhận | nút "Tạo yêu cầu" (owner) |
   | `Processing` | Staff đã bấm tạo → đã gửi Meshy | nút "Tạo model3d" (staff) |
   | `Succeeded` / `Failed` | worker nền hoàn tất | hệ thống |
   | `Rejected` (mới, *tùy chọn*) | Staff từ chối yêu cầu | nút "Từ chối" (staff) |

   > Giữ `Pending(0)` để không phải đánh số lại enum đã lưu DB; thêm `Requested`, `Rejected` ở **cuối** (giá trị mới) tránh lệch dữ liệu cũ.
3. **Lưu vết người yêu cầu/xử lý** trên `ProductModel3D`: `RequestedBy`, `RequestedAt`, `AssignedStaffId?`, `RequestNote?`.

---

## A. DOMAIN — entity & enum

| File | Thay đổi |
|---|---|
| `Domain/Enums/Catalog/Model3DStatus.cs` | Thêm `Requested = 4`, `Rejected = 5` (thêm cuối enum). |
| `Domain/Entities/Catalog/ProductModel3D.cs` | Thêm: `Guid RequestedBy`, `DateTime RequestedAt`, `Guid? AssignedStaffId`, `string? RequestNote`. `Status` mặc định đổi sang `Requested` (thay vì `Pending`). |

---

## B. INFRASTRUCTURE — DB

| File | Thay đổi |
|---|---|
| `Persistence/Configurations/ProductModel3DConfiguration.cs` | Map 4 cột mới; `Status` lưu string (giữ convention hiện tại). |
| `Persistence/Migrations/` | **Migration mới**: thêm cột `requested_by`, `requested_at`, `assigned_staff_id` (nullable), `request_note` (nullable) vào bảng `product_model_3d`. |
| `Persistence/Repositories/ProductRepository.cs` | Thêm `GetModel3DRequestsAsync(status, skip, take, ct)` — list theo trạng thái (cho hàng đợi staff). Có thể kèm thông tin product/ảnh để staff xem. |
| `Interfaces/Repositories/IProductRepository.cs` | Khai báo method mới. |

---

## C. APPLICATION — `ProductModel3DService.cs`

| Mục | Thay đổi |
|---|---|
| **Tách** `GenerateAsync` cũ thành 2 method | |
| `RequestAsync(productId, userId, isAdmin, request, ct)` | Quyền: `CanManageStoreAsync` (owner store / admin). Chọn/validate ảnh nguồn như cũ (có thể để staff chọn sau — xem mục H). Tạo/cập nhật `ProductModel3D` với `Status = Requested`, `RequestedBy = userId`, `RequestedAt = now`, `RequestNote`. **Không** gọi Meshy. Chặn nếu đang `Requested`/`Processing` (Conflict). |
| `GenerateAsync(productId, staffId, ct)` *(bản mới, cho staff)* | Quyền: **staff sàn / admin** (kiểm tra ở controller bằng policy — xem mục E). Load model: phải đang `Requested` (else BadRequest). Chọn ảnh nguồn (đã lưu `SourceImageUrl` lúc request, hoặc chọn lại). Gọi `_generator.StartImageTo3DAsync` → set `Status = Processing`, `MeshyTaskId`, `AssignedStaffId = staffId`, reset progress/url như logic hiện có. |
| `RejectAsync(productId, staffId, reason, ct)` *(tùy chọn)* | `Requested → Rejected`, lưu `ErrorMessage = reason`. |
| `ListRequestsAsync(status, page, ct)` | Cho màn hình staff: trả danh sách yêu cầu theo trạng thái. |
| `PollPendingAsync`, `FinalizeSucceededAsync`, `GetAsync`, `DeleteAsync` | **Giữ nguyên** (worker + đọc + xoá không đổi). |

> Lưu ý: phần "chọn ảnh nguồn" hiện nằm trong `GenerateAsync`. Quyết định đặt ở bước **request** (owner chọn) hay bước **generate** (staff chọn) — xem mục H.

---

## D. APPLICATION — Interface + DTO

| File | Thay đổi |
|---|---|
| `Features/Catalog/Services/IProductModel3DService.cs` | Thêm khai báo `RequestAsync`, `GenerateAsync(staff)`, `RejectAsync?`, `ListRequestsAsync`. Đổi chữ ký `GenerateAsync` cũ. |
| `Features/Catalog/DTOs/ProductDtos.cs` | Thêm `RequestModel3DRequest { Guid? SourceImageId; string? Note }`. `ProductModel3DResponse`: thêm `RequestedBy`, `RequestedAt`, `AssignedStaffId`, trạng thái mới. DTO cho item hàng đợi staff (kèm tên product, ảnh). |
| `Features/Catalog/Mappings/CatalogMappingProfile.cs` | Map các field mới. |

---

## E. WEBAPI — `ProductsController.cs` (+ authorization)

| Endpoint | Quyền | Gọi |
|---|---|---|
| `POST /api/products/{id}/model-3d/request` | `[Authorize]` + check owner store trong service | `RequestAsync` (garden owner) |
| `POST /api/products/{id}/model-3d/generate` | `[Authorize(Policy = StaffOrAbove)]` | `GenerateAsync` (staff sàn) |
| `POST /api/products/{id}/model-3d/reject` *(tùy chọn)* | `[Authorize(Policy = StaffOrAbove)]` | `RejectAsync` |
| `GET /api/products/model-3d/requests?status=Requested` | `[Authorize(Policy = StaffOrAbove)]` | `ListRequestsAsync` (hàng đợi staff) |
| `GET /api/products/{id}/model-3d` | giữ nguyên | `GetAsync` |
| `DELETE /api/products/{id}/model-3d` | giữ nguyên | `DeleteAsync` |

> `POST .../model-3d` cũ: **bỏ** hoặc trỏ tạm sang `request` để FE cũ không vỡ (rồi deprecate).
> Policy `StaffOrAbove` = `RequireRole(Staff, Admin)` — đăng ký ở `Program.cs`/`AuthorizationPolicies.cs` (giống cách `GardenOwnerOrAbove` được thêm ở `FIX_GARDEN_OWNER_FLOW.md`).

---

## F. APPLICATION — `ApiStatusMessages`

| Key | Nội dung |
|---|---|
| `Model3DRequested` | "Đã gửi yêu cầu tạo model 3D. Chờ nhân viên xử lý." |
| `Model3DNotRequested` | "Chỉ tạo model khi yêu cầu đang ở trạng thái chờ xử lý." |
| `Model3DRequestRejected` | "Đã từ chối yêu cầu tạo model 3D." |
| `Model3DAlreadyRequested` | "Sản phẩm này đã có yêu cầu tạo model 3D đang chờ/đang xử lý." |

---

## G. Flow mới (end-to-end)

1. **Garden owner** mở product → bấm "Yêu cầu 3D" → `POST .../model-3d/request` → bản ghi `Requested`.
2. **Staff sàn** mở hàng đợi → `GET .../model-3d/requests?status=Requested` thấy yêu cầu.
3. Staff bấm "Tạo model3d" → `POST .../model-3d/generate` → gọi Meshy → `Processing` (+ `AssignedStaffId`).
4. **Worker nền** poll Meshy → tải GLB, re-host storage → `Succeeded` (`ModelUrl`). Lỗi → `Failed`.
5. Owner/FE xem `GET .../model-3d` thấy kết quả.
6. *(Tùy chọn)* Staff bấm "Từ chối" → `Rejected` + lý do.

---

## H. Câu hỏi nghiệp vụ cần chốt

1. **Chọn ảnh nguồn** ở bước nào: owner chọn lúc request, hay staff chọn lúc generate? (Ảnh hưởng DTO + UI.)
2. **Quan hệ 1–1 hiện tại**: giữ 1 product = 1 model3d (yêu cầu mới ghi đè bản cũ), hay cần **lịch sử nhiều yêu cầu** (đổi sang 1–n + bảng request riêng)?
3. **Staff có quyền từ chối** không? (Quyết định có thêm `Rejected` + endpoint reject.)
4. **Gán việc**: staff tự nhận bất kỳ request, hay cần cơ chế assign trước khi generate?

---

## Tổng kết file bị tác động

| Loại | File |
|---|---|
| **Sửa** | `Domain/Enums/Catalog/Model3DStatus.cs` (thêm `Requested`, `Rejected`) |
| **Sửa** | `Domain/Entities/Catalog/ProductModel3D.cs` (4 field mới, default `Requested`) |
| **Sửa** | `Application/Features/Catalog/Services/ProductModel3DService.cs` (tách `Request`/`Generate`/`Reject`/`ListRequests`) |
| **Sửa** | `Application/Features/Catalog/Services/IProductModel3DService.cs` |
| **Sửa** | `Application/Features/Catalog/DTOs/ProductDtos.cs` + `Mappings/CatalogMappingProfile.cs` |
| **Sửa** | `Application/Common/Constants/ApiStatusMessage.cs` |
| **Sửa** | `Infrastructure/Persistence/Configurations/ProductModel3DConfiguration.cs` + Repository + `IProductRepository` |
| **Thêm** | Migration cột mới cho `product_model_3d` |
| **Sửa** | `WebAPI/Controllers/ProductsController.cs` + policy `StaffOrAbove` (`Program.cs`/`AuthorizationPolicies.cs`) |
| **Không đổi** | `Model3DPollingWorker`, `MeshyModel3DGenerator`, `IModel3DGenerator`, `MeshySettings` (tầng Meshy giữ nguyên) |
