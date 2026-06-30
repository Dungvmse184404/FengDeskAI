# Yêu cầu sửa chữa: Flow Garden Owner & sở hữu Store

> Mục tiêu: biến hệ thống từ **shop do Admin quản lý tập trung** thành **sàn trung gian (marketplace)** — người bán (garden owner) tự mở shop và up sản phẩm.

## Quyết định thiết kế (đã chốt)

1. **Onboarding: self-service.** User tự đăng ký làm seller và tạo store ngay, không cần Admin duyệt.
2. **Sở hữu nhiều-nhiều.** Một owner có thể sở hữu **nhiều store**; một store có thể có **nhiều owner**.
3. **Role:** thêm flag `GardenOwner` vào enum `UserRole` (bit-flag). User có thể vừa `Customer` vừa `GardenOwner`.

## Vấn đề hiện tại (tóm tắt)

- `UserRole` không có `GardenOwner` — "chủ vườn" chỉ là quan hệ dữ liệu `GardenStore.OwnerUserId`.
- `POST /api/stores` khóa `AdminOnly` → seller không tự mở shop được.
- `StoreService.CreateAsync` lấy owner từ `request.OwnerUserId`, bỏ qua `actorUserId` → không tự gán owner.
- `GardenStore.OwnerUserId` chỉ chứa **1 owner** → không hỗ trợ nhiều owner/store.
- Endpoint product chỉ `[Authorize]`, không thể hiện vai trò người bán.

---

## A. Thay đổi DATABASE (bảng)

### Bảng THÊM MỚI

| Bảng | Mục đích | Cột chính |
|---|---|---|
| `garden_store_owners` | Bảng nối **nhiều-nhiều** giữa store và owner (thay cho `owner_user_id` đơn lẻ) | `id` (PK), `garden_store_id` (FK → garden_stores), `owner_user_id` (FK → users), `is_primary` (bool — owner tạo shop / owner chính), `assigned_at` + cột audit của `BaseEntity` |

> Ràng buộc: unique `(garden_store_id, owner_user_id)`. `is_primary = true` cho owner đã tạo store (đúng 1 dòng/store).

### Bảng CHỈNH SỬA

| Bảng | Thay đổi |
|---|---|
| `garden_stores` | **Bỏ cột `owner_user_id`** (chuyển quyền sở hữu sang `garden_store_owners`). Quan hệ owner giờ qua bảng nối. |
| `users` | **Không đổi schema** (cột `role` là int bit-flag sẵn). Chỉ thay đổi *logic*: giá trị role có thể chứa thêm flag `GardenOwner`. |

> Lưu ý migration dữ liệu: với store cũ đang có `owner_user_id`, cần seed 1 dòng `garden_store_owners` (`is_primary = true`) trước khi drop cột.

### Bảng KHÔNG đổi (giữ nguyên)

- `garden_staff_assignments` — vẫn là nhân viên store (khác với owner). Quyền quản lý = owner **hoặc** staff active.

---

## B. Thay đổi DOMAIN (entities & enums)

| File | Thay đổi |
|---|---|
| `Domain/Enums/UserRole.cs` | Thêm `GardenOwner = 1 << 4` // 16 |
| `Domain/Entities/Vendor/GardenStoreOwner.cs` | **Tạo mới** entity `: BaseEntity` { `GardenStoreId`, `OwnerUserId`, `IsPrimary`, `AssignedAt`, nav `Store` } |
| `Domain/Entities/Vendor/GardenStore.cs` | **Bỏ** `OwnerUserId`; **thêm** `ICollection<GardenStoreOwner> Owners` |

---

## C. Thay đổi INFRASTRUCTURE

| File                                                          | Thay đổi                                                                                                                                                                       |
| ------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Persistence/Configurations/GardenStoreOwnerConfiguration.cs` | **Tạo mới**: map bảng `garden_store_owners`, unique index `(garden_store_id, owner_user_id)`                                                                                   |
| `Persistence/Configurations/GardenStoreConfiguration.cs`      | Bỏ mapping `OwnerUserId`; cấu hình quan hệ 1-N tới `GardenStoreOwner`                                                                                                          |
| `Persistence/Repositories/StoreRepository.cs`                 | `CanManageAsync`: kiểm tra qua `garden_store_owners` (thay vì `OwnerUserId`) **hoặc** staff active. Thêm `AddOwnerAsync`, `RemoveOwnerAsync`, `GetOwnersAsync`, `IsOwnerAsync` |
| `Persistence/Migrations/`                                     | **Migration mới**: tạo `garden_store_owners`, backfill từ `owner_user_id`, drop `owner_user_id`                                                                                |

---

## D. Thay đổi APPLICATION (services, DTOs)

| File | Thay đổi |
|---|---|
| `Features/Vendor/Services/StoreService.cs` | `CreateAsync(actorUserId, …)`: tạo store → thêm `GardenStoreOwner{ OwnerUserId = actorUserId, IsPrimary = true }` → **cấp role `GardenOwner`** cho user nếu chưa có. `IsOwnerOrAdmin` → kiểm tra qua bảng nối. Thêm `AddOwnerAsync` / `RemoveOwnerAsync` (chỉ owner hiện tại hoặc Admin; không cho gỡ owner primary cuối cùng) |
| `Features/Vendor/DTOs/StoreDtos.cs` | `CreateStoreRequest`: **bỏ `OwnerUserId`** (owner = người gọi). `StoreResponse`: thêm danh sách `Owners`. Thêm `AddOwnerRequest { OwnerUserId }` |
| `Features/Identity/...` (User service) | Hàm cấp/thu hồi flag role: `AddRole(GardenOwner)` (gọi khi tạo store đầu tiên) |
| `Features/Catalog/Services/ProductService.cs` | `CanManageStoreAsync` đã gọi `Stores.CanManageAsync` — chỉ cần repo sửa là tự đúng (không đổi logic ở đây) |

---

## E. Thay đổi WEBAPI (controllers, authorization)

| File | Thay đổi |
|---|---|
| `Authorization/AuthorizationPolicies.cs` | Thêm `Roles.GardenOwner` + policy `GardenOwnerOrAbove` |
| `Program.cs` | Đăng ký policy `GardenOwnerOrAbove` = RequireRole(GardenOwner, Admin) |
| `Controllers/StoresController.cs` | `POST /api/stores`: **bỏ `AdminOnly`**, chuyển sang `[Authorize]` (self-service), truyền `CurrentUserId`. Thêm `POST /api/stores/{id}/owners`, `DELETE /api/stores/{id}/owners/{userId}` (chỉ owner/Admin) |
| `Controllers/ProductsController.cs` | (Tùy chọn) gắn `[Authorize(Policy = GardenOwnerOrAbove)]` lên các endpoint ghi để API thể hiện rõ vai trò người bán. Quyền sở hữu vẫn check ở service |

---

## F. Flow mới (end-to-end)

1. User (Customer) gọi `POST /api/stores` với thông tin shop.
2. `StoreService.CreateAsync`:
   - Tạo `GardenStore`.
   - Thêm `garden_store_owners` (owner = chính họ, `is_primary = true`).
   - Cấp flag `GardenOwner` cho user (nếu chưa có).
3. User giờ là garden owner → `POST /api/products` (+ `/items`, `/images`, `/feng-shui`) cho store của mình; `CanManageStoreAsync` cho phép vì họ có dòng trong `garden_store_owners`.
4. (Tùy chọn) Owner thêm đồng sở hữu: `POST /api/stores/{id}/owners` → thêm user khác vào `garden_store_owners`.

---

## G. Cập nhật tài liệu

- **ERD** `Documents/ERD/SEP490_FengDeskAI.drawio` (trang Logical): thêm bảng `garden_store_owners`, bỏ FK `owner_user_id` của `garden_stores`, vẽ lại quan hệ.
- Cập nhật `README.md` / `CLAUDE.md` phần role nếu cần.

## Tổng kết bảng bị tác động

| Loại | Bảng |
|---|---|
| **Thêm mới** | `garden_store_owners` |
| **Sửa** | `garden_stores` (bỏ `owner_user_id`), `users` (logic role, không đổi schema) |
| **Không đổi** | `garden_staff_assignments`, `products`, `product_items`, ... |
