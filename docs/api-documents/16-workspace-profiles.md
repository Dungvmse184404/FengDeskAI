# 16 — Workspace Profiles

[← Mục lục](./README.md)

Controller: `WorkspaceProfilesController` · Route gốc: `/api/workspace` · **Toàn bộ `[Authorize]`** — user chỉ thao tác trên profile của chính mình.

Hồ sơ không gian làm việc — mô tả bàn/phòng/hướng/phong cách... dùng làm input cho engine gợi ý phong thủy. Profile mặc định được dùng khi user không chỉ định.

> **Engine v3 — lưu ý field:**
> - `fengShuiElement` (mệnh nhập tay) là **legacy**, engine v3 **không dùng** (mệnh phòng nay tính bằng vector ngũ hành từ loại phòng + màu/vật liệu).
> - DB đã có thêm các cột phục vụ v3: `entrance_direction`, `toilet_direction`, `dark_directions` (hướng bị chắn → Directional Validation) và bảng phụ `workspace_profile_inputs` (màu/vật liệu/hình khối thực tế của phòng → dựng `currentVector`).
> - ⚠️ Các field/bảng mới này **hiện chưa được lộ ra** trong request/response bên dưới — mới tồn tại ở tầng DB/engine.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/workspace` | Authenticated | Danh sách profile của tôi |
| GET | `/api/workspace/default` | Authenticated | Profile mặc định |
| GET | `/api/workspace/{id}` | Authenticated | Chi tiết profile |
| GET | `/api/workspace/{id}/element-analysis` | Authenticated | Vector ngũ hành phòng (thiếu/thừa hành gì) |
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

## GET `/api/workspace/{id}/element-analysis`
Phân tích ngũ hành của một workspace **không cần chạy cả phiên recommendation** — để FE hiển thị "phòng của bạn đang thiếu/thừa hành gì". Dùng đúng công thức vector với engine chấm điểm (`Gap = adjustedIdeal − current`).

`data` = `WorkspaceElementAnalysisResponse`:
```json
{
  "workspaceProfileId": "guid",
  "dominantNeed": "Thuy",
  "elements": [
    { "element": "Thuy", "ideal": 0.20, "adjustedIdeal": 0.30, "current": 0.00, "gap":  0.30 },
    { "element": "Moc",  "ideal": 0.25, "adjustedIdeal": 0.25, "current": 0.24, "gap":  0.01 },
    { "element": "Kim",  "ideal": 0.15, "adjustedIdeal": 0.10, "current": 0.36, "gap": -0.26 }
  ]
}
```
| Field | Kiểu | Ghi chú |
|-------|------|---------|
| `workspaceProfileId` | guid | Profile được phân tích (phải thuộc user, không có → `404`) |
| `dominantNeed` | enum `FengShuiElement` | Hành có `gap` dương lớn nhất (thiếu nhiều nhất) |
| `elements[]` | array | 5 hành, sắp **giảm dần theo `gap`** (thiếu nhất → thừa nhất) |
| `elements[].ideal` | decimal | Vector lý tưởng theo loại phòng (Σ=1) |
| `elements[].adjustedIdeal` | decimal | Ideal đã bẻ theo mục đích làm việc (Σ=1) |
| `elements[].current` | decimal | Hiện trạng phòng từ màu/vật liệu (Σ=1) |
| `elements[].gap` | decimal | `adjustedIdeal − current`: **+ thiếu, − thừa** (Σ=0) |

> Workspace không gắn `workspaceTypeId` → `ideal` rỗng (toàn 0), không lỗi.

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
