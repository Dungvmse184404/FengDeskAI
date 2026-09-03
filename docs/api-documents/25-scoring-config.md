# 25 — Scoring Config (Admin engine v3)

[← Mục lục](./README.md)

Controller: `ScoringConfigController` · Route gốc: `/api/admin/scoring` · **Toàn bộ `[Authorize(Policy = ManagerOrAbove)]`**.

Quản trị cấu hình engine chấm điểm gợi ý **v3**: tham số điểm, map ngũ hành (màu/vật liệu/hình khối), modifier theo mục đích, và vector lý tưởng/nội thất theo loại phòng. Sửa được dữ liệu **không cần đổi code**.

---

## 📋 Bảng endpoint

| Method | Path | Mô tả |
|--------|------|-------|
| GET | `/api/admin/scoring/params` | Danh sách tham số engine |
| PUT | `/api/admin/scoring/params/{code}` | Sửa 1 tham số |
| GET | `/api/admin/scoring/element-inputs` | Map (màu/vật liệu/hình) → hành |
| PUT | `/api/admin/scoring/element-inputs` | Thêm/sửa 1 dòng map |
| DELETE | `/api/admin/scoring/element-inputs/{id}` | Xóa 1 dòng map |
| GET | `/api/admin/scoring/purpose-modifiers` | Modifier Intent theo mục đích |
| PUT | `/api/admin/scoring/purpose-modifiers` | Thêm/sửa 1 modifier |
| DELETE | `/api/admin/scoring/purpose-modifiers/{id}` | Xóa 1 modifier |
| GET | `/api/admin/scoring/workspace-type-elements` | Vector Ideal/Interior theo loại phòng |
| PUT | `/api/admin/scoring/workspace-type-elements` | Thêm/sửa 1 dòng vector |
| DELETE | `/api/admin/scoring/workspace-type-elements/{id}` | Xóa 1 dòng vector |

---

## Tham số engine — `scoring_params`

`data` = mảng `ScoringParamDto`: `{ id, code, value, description }`.
**PUT** body (`UpsertScoringParamRequest`): `{ "value": 0.3, "description": "..." }`.

### Dựng vector

| Code | Default | Ý nghĩa |
|------|:---:|---------|
| `SELF_SHARE` | 0.60 | Tỉ trọng **bản mệnh** trong vector mệnh cá nhân |
| `SUPPORT_SHARE` | 0.30 | Tỉ trọng hành **sinh ra** bản mệnh (mẹ) |
| `CHILD_SHARE` | 0.10 | Tỉ trọng hành bản mệnh **sinh ra** (con) |
| `MATERIAL_SHARE` | 0.60 | Tỉ trọng chất liệu khi dựng vector sản phẩm |
| `COLOR_SHARE` | 0.40 | Tỉ trọng màu/hình khi dựng vector sản phẩm |
| `FALLBACK_PRIMARY` | 0.70 | Trọng số hành chính khi backfill vector từ `product_elements` |
| `FALLBACK_SECONDARY` | 0.30 | Trọng số hành phụ khi backfill |

### Vật phẩm mang theo người (`ProductPlacement.Carry`)

| Code | Default | Ý nghĩa |
|------|:---:|---------|
| `CARRY_PRIMARY_SHARE` | 0.60 | Tỉ trọng **dụng thần chính** khi dựng vector mục tiêu cá nhân |
| `CARRY_SECONDARY_SHARE` | 0.40 | Tỉ trọng dụng thần phụ |

> Chỉ áp cho luồng [`POST /api/recommendations/personal`](./18-recommendations.md). Có giờ sinh → dụng thần **Tứ Trụ**; thiếu giờ sinh → fallback **Nạp Âm** (dùng `SELF/SUPPORT/CHILD_SHARE`).

### Trừ điểm

| Code | Default | Ý nghĩa |
|------|:---:|---------|
| `USER_CONFLICT_PENALTY` | 0.30 | Phạt khi hành trội sản phẩm khắc mệnh user (không gian dùng chung) |
| `DIRECTION_PENALTY` | 0.15 | Phạt khi mọi hướng hợp vật phẩm đều bị chắn |
| `VIBE_MISMATCH_PENALTY` | 0.20 | Phạt khi sản phẩm **có** vibe nhưng không khớp mục đích phòng |
| `VIBE_UNKNOWN_PENALTY` | 0.05 | Phạt khi sản phẩm **chưa khai** vibe — thiếu dữ liệu, nhẹ hơn lệch thật |

### Trục cá nhân (v3.1)

| Code | Seed | Ý nghĩa |
|------|:---:|---------|
| `PERSONAL_WEIGHT_PRIVATE` | **0.00** | Tỉ trọng `personalScore` ở không gian `Private`. Đích **0.50** |
| `PERSONAL_WEIGHT_SHARED` | **0.00** | Ở không gian `Shared` (phòng khách, bếp, phòng họp). Đích **0.30** |
| `PERSONAL_WEIGHT_PUBLIC` | 0.00 | Ở không gian `Public` (lễ tân, khu mở) — **luôn 0** |

```
score = (1 − Wp)·gapScore + Wp·personalScore − dirPenalty − vibePenalty
```

`Wp = 0` (seed) → giữ nguyên hành vi trước v3.1: hard-filter khắc mệnh + `USER_CONFLICT_PENALTY`.
`Wp > 0` → xung khắc tính có dấu trong `personalScore` (từ `feng_shui_rules`), **bỏ** hard-filter và `USER_CONFLICT_PENALTY` để không phạt hai lần.

> `Wp` cũng bị ép về 0 khi user **chưa có ngày sinh** — không có mệnh thì không có gì để trộn.
> Chi tiết: [personalized-recommendation-v3.1.md](../adr/personalized-recommendation-v3.1.md)

### Cờ điều khiển (kill-switch)

| Code | Seed | Ý nghĩa |
|------|:---:|---------|
| `VIBE_FILTER_HARD` | **1.00** | `≥ 0.5` → lệch vibe bị **loại thẳng** (hành vi v3). `< 0.5` → chuyển sang trừ điểm mềm bằng 2 tham số trên |
| `MIN_SCORE_THRESHOLD` | **−1.00** | Cắt theo **điểm tổng** thay vì theo từng thuộc tính. `−1.0` = không cắt. Nâng lên `0.0` khi đã tắt `VIBE_FILTER_HARD` |

> `scoring_params.value` là `decimal` nên cờ được encode thành số. Bật/tắt qua endpoint này, **không cần deploy**.
> Xem [vibe-soft-scoring.md](../adr/vibe-soft-scoring.md) cho quy trình rollout.

> ⚠️ Thiếu row nào → engine dùng **default trong code**, không lỗi.

## Map ngũ hành — `element_input_map`

`data` = `ElementInputMapDto`: `{ id, inputKind, inputCode, element, weight }`.
**PUT** body (`UpsertElementInputMapRequest`):
```json
{ "inputKind": "Material", "inputCode": "Wood", "element": "Moc", "weight": 1.0 }
```
| Field | Ghi chú |
|-------|---------|
| `inputKind` | enum `ElementInputKind`: `Color` / `Material` / `Shape` / `DecorItem` |
| `inputCode` | mã bất biến, vd `Red`, `Wood`, `SaltRock`, `Sphere` |
| `element` | `Kim/Moc/Thuy/Hoa/Tho` |
| `weight` | đóng góp vào hành (mặc định 1.0). Một `(kind, code)` có thể trải nhiều hành |

> Dùng chung cho cả phòng (`workspace_profile_inputs`) và sản phẩm (`product_element_inputs`).

## Modifier Intent — `work_purpose_element_modifiers`

`data` = `WorkPurposeModifierDto`: `{ id, workPurpose, element, delta }`.
**PUT** body (`UpsertWorkPurposeModifierRequest`):
```json
{ "workPurpose": "Study", "element": "Thuy", "delta": 0.10 }
```
Bẻ vector lý tưởng theo mục đích. `delta` **có thể âm**.

## Vector loại phòng — `workspace_type_elements`

`data` = `WorkspaceTypeElementDto`: `{ id, workspaceTypeId, source, element, weight }`.
**PUT** body (`UpsertWorkspaceTypeElementRequest`):
```json
{ "workspaceTypeId": "guid", "source": "Ideal", "element": "Moc", "weight": 0.5 }
```
| Field | Ghi chú |
|-------|---------|
| `source` | `Ideal` (vector lý tưởng cần đạt) hoặc `Interior` (hiện trạng nội thất mặc định) |
| `weight` | trọng số hành trong bộ; mỗi `(type, source)` nên Σ≈1 |

---

[← Dev Tools](./24-dev-tools.md) · [Tiếp: Appendix →](./99-appendix-models.md)
