# 16 — Workspace Profiles

[← Mục lục](./README.md)

Controller: **`WorkspaceController`** · Route gốc: `/api/workspace` · **Toàn bộ `[Authorize]`** — user chỉ thao tác trên profile của chính mình.

Hồ sơ không gian làm việc — mô tả bàn/phòng/hướng/phong cách... dùng làm input cho engine gợi ý phong thủy. Profile mặc định được dùng khi user không chỉ định.

Ngoài CRUD, controller này còn ôm **luồng AI intake** (mô tả tự do / ảnh / giọng nói → tự điền form) và **luồng đặt sản phẩm đã mua vào phòng**.

> **Lưu ý field engine:**
> - `fengShuiElement` (mệnh nhập tay) là **legacy**, engine **không dùng** — mệnh phòng tính bằng vector ngũ hành từ loại phòng + màu/vật liệu.
> - `inputs` (màu/vật liệu/hình khối thực tế của phòng → dựng `currentVector`) **ĐÃ lộ ra** ở cả request lẫn response. Quy ước null-vs-rỗng: `inputs = null` → **giữ nguyên**; `inputs = []` → **xoá hết**.
> - ⚠️ `entrance_direction`, `toilet_direction`, `dark_directions` (hướng bị chắn → Directional Validation) **vẫn CHƯA có ô nhập** ở request/UI — chỉ tồn tại ở tầng DB/engine, nên `DIRECTION_PENALTY` gần như không bao giờ kích hoạt. Xem `docs/ard/architecture-core/06-doc-debt.md`.

---

## 📋 Bảng endpoint — **18 endpoint**

### CRUD hồ sơ

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

### AI intake (mô tả tự do / ảnh / giọng nói)

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| POST | `/api/workspace/parse-description` | **CustomerOnly** | Bắt đầu job parse — trả `operationId` **ngay**, không chờ LLM |
| GET | `/api/workspace/parse-description/{operationId}` | **CustomerOnly** | Poll kết quả job (fallback khi lỡ mất event realtime; hết hạn → `404`) |
| GET | `/api/workspace/speech-config` | **CustomerOnly** | Cấu hình STT (tắt → FE fallback Web Speech) |
| POST | `/api/workspace/transcriptions` | **CustomerOnly** | Ghi âm → text (multipart, field `file`) |
| POST | `/api/workspace/images` | Authenticated | Upload ảnh phòng để đính kèm vào intake |
| GET | `/api/workspace/element-inputs` | Authenticated | Vocabulary màu/vật liệu/hình khối/vật trang trí |
| POST | `/api/workspace/element-inputs/classify` | **CustomerOnly** | Tag tự đặt tên → hành + weight (AI, có chuẩn hoá deterministic) |

> Job chạy nền qua `WorkspaceIntakeQueue` → `WorkspaceIntakeWorker`; tiến độ phát realtime qua SignalR group `ai-op-{operationId}`.
> Rate-limit policy `workspace-intake` áp cho `parse-description`, `transcriptions`, `element-inputs/classify`.

### Đặt sản phẩm đã mua vào phòng

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/workspace/placements/purchasable` | Authenticated | Sản phẩm đã mua đủ điều kiện đặt phòng |
| PUT | `/api/workspace/{id}/placements` | Authenticated | Đặt / chuyển sản phẩm sang phòng này (idempotent) |
| DELETE | `/api/workspace/{id}/placements/{orderItemId}` | Authenticated | Gỡ sản phẩm khỏi phòng |

> `element-analysis` tính **lúc đọc**, không lưu DB: `current`/`gap` chỉ tính hàng **đã giao**; `previewCurrent`/`previewGap` tính cả hàng **đang giao**.

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
  "isDefault": true,
  "completenessPercent": 75,
  "missingFieldHints": ["Thêm diện tích mặt bàn để lọc vật phẩm vừa kích thước"],
  "inputs": [{ "inputKind": "Material", "inputCode": "Wood" }],
  "createdAt": "...", "updatedAt": "..."
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
