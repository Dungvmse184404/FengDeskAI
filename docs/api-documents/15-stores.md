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
| GET | `/api/stores/{id}/staff` | Owner/Admin | Danh sách nhân viên + lời mời chưa phản hồi |
| POST | `/api/stores/{id}/staff` | Owner/Admin | **Mời** nhân viên (Pending + Notification) |
| DELETE | `/api/stores/{id}/staff/{assignmentId}` | Owner/Admin | Gỡ / huỷ lời mời (→ `Revoked`) |
| GET | `/api/stores/staff/invitations/mine` | Authenticated | Lời mời Pending gửi cho tôi |
| POST | `/api/stores/staff/{assignmentId}/accept` | Người được mời | Đồng ý (→ `Accepted`) |
| POST | `/api/stores/staff/{assignmentId}/reject` | Người được mời | Từ chối (→ `Rejected`) |

---

## GET `/api/stores` · `/mine` · `/{id}`

> **`/mine`** trả store mà user là **owner HOẶC garden staff đã `Accepted`** (nguồn sự thật cho quyền vào khu người bán). Mỗi item có thêm `isOwner`: `true` = owner, `false` = chỉ là nhân viên (dùng để FE ẩn nút owner-only). `/` và `/{id}` không set `isOwner`.

`data` = `StoreResponse` (hoặc mảng):
```json
{
  "id": "guid", "name": "Vườn Xanh", "description": "...", "hotline": "1900...",
  "openingHours": "8:00-21:00", "isActive": true, "isOwner": true,
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

## Nhân viên (staff) — invitation flow

State machine assignment:
```
Pending ──staff accept──► Accepted ──owner gỡ──► Revoked
   │
   ├──staff reject──► Rejected
   └──owner huỷ──► Revoked
```
Quyền store-scoped của staff chỉ tính khi `Status == Accepted`.

**GET `/{id}/staff`** — Owner/Admin. `data` = mảng `StaffAssignmentResponse`:
```json
[{
  "id": "guid", "gardenStoreId": "guid",
  "staffId": "guid", "staffName": "Nguyễn Văn B",
  "staffEmail": "b@example.com", "staffPhone": "0901234567",
  "invitedBy": "guid", "invitedByName": "Nguyễn Văn A",
  "status": "Pending",
  "invitedAt": "...", "respondedAt": null, "unassignedAt": null
}]
```
Trả cả `Pending` và `Accepted`. Rejected/Revoked ẩn để list gọn.

**POST `/{id}/staff`** — Owner/Admin. Mời nhân viên (FE lấy `staffId` từ `GET /api/users/search`):
```json
{ "staffId": "guid" }
```
Vẫn chấp nhận `{ "staffEmail": "..." }` cho client cũ. BE tạo Pending + push Notification (`StaffInvited`, `ReferenceType.StaffInvitation`, `ReferenceId = assignment.id`).

Lỗi: `400 StaffNotFound` (user không tồn tại), `400 IdentifierRequired` (không gửi cả hai), `400 CannotInviteOwner`, `409 AlreadyInvited` (đang Pending), `409 AlreadyAssigned` (đang Accepted).

**DELETE `/{id}/staff/{assignmentId}`** — Owner/Admin. Gỡ hoặc huỷ lời mời (Pending/Accepted → `Revoked`).

### Lời mời (góc nhìn người được mời)

**GET `/api/stores/staff/invitations/mine`** — Authenticated. `data` = mảng `InvitationResponse`:
```json
[{
  "id": "guid", "gardenStoreId": "guid", "storeName": "Vườn Xanh",
  "invitedBy": "guid", "invitedByName": "Nguyễn Văn A",
  "status": "Pending", "invitedAt": "..."
}]
```

**POST `/api/stores/staff/{assignmentId}/accept`** — chỉ chủ lời mời (`StaffId == CurrentUserId`) và assignment đang `Pending`. Trả `StaffAssignmentResponse` mới (`Accepted`). Gửi Notification về cho owner.

**POST `/api/stores/staff/{assignmentId}/reject`** — tương tự. Chuyển `Rejected`. Gửi Notification về owner.

Lỗi: `404 InvitationNotFound`, `409 InvitationNotPending`.

---

[← Locations](./14-locations.md) · [Tiếp: Workspace Profiles →](./16-workspace-profiles.md)
