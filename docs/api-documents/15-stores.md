# 15 — Stores

[← Mục lục](./README.md)

Controller: `StoresController` · Route gốc: `/api/stores` · Mặc định `[Authorize]`; **list/detail Public**.

Quản lý garden store (marketplace). User đã đăng nhập tự mở store (self-service → thành owner chính + được cấp role `GardenOwner`). Owner/Admin sửa store, địa chỉ, đồng sở hữu, phân công nhân viên. Quyền sở hữu kiểm ở service layer.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/stores` | Public | Store đang hoạt động |
| GET | `/api/stores/mine` | Authenticated | Store tôi đồng sở hữu |
| GET | `/api/stores/{id}` | Public | Chi tiết store |
| POST | `/api/stores` | Authenticated | Tự mở store |
| PUT | `/api/stores/{id}` | Owner/Admin | Cập nhật store |
| DELETE | `/api/stores/{id}` | Owner/Admin | Xóa (mềm) store |
| DELETE | `/api/stores/{id}/hard` | StaffOrAbove | Xóa vĩnh viễn store |
| POST | `/api/stores/{id}/address` | Owner/Admin | Thêm địa chỉ store |
| PUT | `/api/stores/{id}/address` | Owner/Admin | Sửa địa chỉ store |
| DELETE | `/api/stores/{id}/address` | Owner/Admin | Xóa (mềm) địa chỉ |
| DELETE | `/api/stores/{id}/address/hard` | StaffOrAbove | Xóa vĩnh viễn địa chỉ |
| GET | `/api/stores/{id}/owners` | Authenticated | Danh sách đồng sở hữu |
| POST | `/api/stores/{id}/owners` | Owner/Admin | Thêm đồng sở hữu |
| DELETE | `/api/stores/{id}/owners/{userId}` | Owner/Admin | Gỡ đồng sở hữu |
| GET | `/api/stores/{id}/staff` | Owner/Admin | Danh sách nhân viên |
| POST | `/api/stores/{id}/staff` | Owner/Admin | Phân công nhân viên |
| DELETE | `/api/stores/{id}/staff/{assignmentId}` | Owner/Admin | Gỡ nhân viên |

---

## GET `/api/stores` · `/mine` · `/{id}`
`data` = `StoreResponse` (hoặc mảng):
```json
{
  "id": "guid", "name": "Vườn Xanh", "description": "...", "hotline": "1900...",
  "openingHours": "8:00-21:00", "isActive": true,
  "address": { "id": "guid", "storeId": "guid", "wardId": "guid",
               "streetAddress": "...", "latitude": null, "longitude": null, "isActive": true },
  "owners": [{ "ownerUserId": "guid", "isPrimary": true, "assignedAt": "..." }],
  "createdAt": "...", "updatedAt": "..."
}
```

## POST `/api/stores`
Tự mở store; người tạo thành owner chính. **Request body** (`CreateStoreRequest`):
```json
{ "name": "Vườn Xanh", "description": "...", "hotline": "1900xxxx", "openingHours": "8:00-21:00" }
```

## PUT `/api/stores/{id}`
**Request body** (`UpdateStoreRequest`)
```json
{ "name": "...", "description": "...", "hotline": "...", "openingHours": "...", "isActive": true }
```

## DELETE `/api/stores/{id}` · `/{id}/hard`
Xóa mềm (owner/admin) hoặc xóa vĩnh viễn (StaffOrAbove).

---

## Địa chỉ store (1-1)

**POST `/{id}/address`** — body `CreateStoreAddressRequest`:
```json
{ "wardId": "guid", "streetAddress": "...", "latitude": null, "longitude": null }
```
**PUT `/{id}/address`** — body `UpdateStoreAddressRequest` (cùng cấu trúc).
**DELETE `/{id}/address`** / **`/{id}/address/hard`** — xóa mềm / vĩnh viễn.

---

## Đồng sở hữu (owners)

**GET `/{id}/owners`** — `data` = mảng `StoreOwnerResponse`.
**POST `/{id}/owners`** — body `AddOwnerRequest`: `{ "ownerUserId": "guid" }`. Chỉ owner hiện tại hoặc Admin.
**DELETE `/{id}/owners/{userId}`** — gỡ đồng sở hữu (không gỡ owner chính).

---

## Nhân viên (staff)

**GET `/{id}/staff`** — `data` = mảng `StaffAssignmentResponse`:
```json
[{ "id": "guid", "gardenStoreId": "guid", "staffId": "guid", "assignedBy": "guid",
   "isActive": true, "assignedAt": "...", "unassignedAt": null }]
```
**POST `/{id}/staff`** — body `AssignStaffRequest`: `{ "staffId": "guid" }`.
**DELETE `/{id}/staff/{assignmentId}`** — gỡ nhân viên.

---

[← Locations](./14-locations.md) · [Tiếp: Workspace Profiles →](./16-workspace-profiles.md)
