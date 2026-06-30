# 16 — Workspace Profiles

[← Mục lục](./README.md)

Controller: `WorkspaceProfilesController` · Route gốc: `/api/workspace` · **Toàn bộ `[Authorize]`** — user chỉ thao tác trên profile của chính mình.

Hồ sơ không gian làm việc — mô tả bàn/phòng/hướng/phong cách... dùng làm input cho engine gợi ý phong thủy. Profile mặc định được dùng khi user không chỉ định.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/workspace` | Authenticated | Danh sách profile của tôi |
| GET | `/api/workspace/default` | Authenticated | Profile mặc định |
| GET | `/api/workspace/{id}` | Authenticated | Chi tiết profile |
| POST | `/api/workspace` | Authenticated | Tạo profile |
| PUT | `/api/workspace/{id}` | Authenticated | Cập nhật profile |
| PATCH | `/api/workspace/{id}/set-default` | Authenticated | Đặt làm mặc định |
| DELETE | `/api/workspace/{id}` | Authenticated | Xóa profile |

---

## GET `/api/workspace` · `/default` · `/{id}`
`data` = `WorkspaceProfileResponse`:
```json
{
  "id": "guid", "userId": "guid", "name": "Bàn làm việc nhà",
  "workspaceTypeId": "guid", "locationType": "Home", "styleCode": "Minimal",
  "lighting": "Natural", "deskType": "Sitting",
  "deskOrientation": "East", "roomFacingDirection": "South",
  "workPurpose": "Office", "fengShuiElement": "Moc", "deskArea": 120,
  "isDefault": true, "createdAt": "...", "updatedAt": "..."
}
```

## POST `/api/workspace`
**Request body** (`CreateWorkspaceProfileRequest`)
```json
{
  "name": "Bàn làm việc nhà",
  "locationType": "Home",
  "workspaceTypeId": "guid",
  "styleCode": "Minimal",
  "lighting": "Natural",
  "deskType": "Sitting",
  "deskOrientation": "East",
  "roomFacingDirection": "South",
  "workPurpose": "Office",
  "fengShuiElement": "Moc",
  "deskArea": 120,
  "isDefault": true
}
```
| Field | Kiểu | Ghi chú |
|-------|------|---------|
| `locationType` | enum `LocationType` | `Home`/`Office`/`Cafe`/`Studio`/`Other` |
| `workspaceTypeId` | guid? | Bỏ trống → coi như riêng tư (weight 1.0) |
| `styleCode` | string | Mã từ [Styles](./05-styles.md) |
| `lighting` | enum `LightingType` | |
| `deskType` | enum `DeskType` | |
| `deskOrientation`, `roomFacingDirection` | enum `CompassDirection` | |
| `workPurpose` | enum `WorkPurpose` | |
| `fengShuiElement` | enum `FengShuiElement` | `Kim/Moc/Thuy/Hoa/Tho` |
| `deskArea` | int | cm² |

## PUT `/api/workspace/{id}`
**Request body** (`UpdateWorkspaceProfileRequest`) — giống create nhưng **không có** `isDefault`.

## PATCH `/api/workspace/{id}/set-default`
Đặt profile làm default (tự bỏ default ở profile khác cùng user).

## DELETE `/api/workspace/{id}`
Xóa profile.

> Enum chi tiết: xem [Appendix](./99-appendix-models.md).

---

[← Stores](./15-stores.md) · [Tiếp: Workspace Types →](./17-workspace-types.md)
