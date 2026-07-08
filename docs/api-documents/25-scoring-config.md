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

| Code | Default | Ý nghĩa |
|------|:---:|---------|
| `SELF_SHARE` | 0.60 | Thưởng khi sản phẩm cùng hành đang thiếu |
| `SUPPORT_SHARE` | 0.30 | Thưởng hành sinh ra hành đang thiếu |
| `CHILD_SHARE` | 0.10 | Thưởng hành được hành thiếu sinh ra |
| `MATERIAL_SHARE` | 0.60 | Tỉ trọng chất liệu khi dựng vector sản phẩm |
| `COLOR_SHARE` | 0.40 | Tỉ trọng màu/hình khi dựng vector sản phẩm |
| `USER_CONFLICT_PENALTY` | 0.30 | Phạt khi sản phẩm khắc mệnh user (không gian dùng chung) |
| `DIRECTION_PENALTY` | 0.15 | Phạt khi mọi hướng hợp đều bị chắn |
| `FALLBACK_PRIMARY` | 0.70 | Trọng số hành chính khi backfill vector sản phẩm |
| `FALLBACK_SECONDARY` | 0.30 | Trọng số hành phụ khi backfill |

> Thiếu row nào → engine dùng default trong code.

## Map ngũ hành — `element_input_map`

`data` = `ElementInputMapDto`: `{ id, inputKind, inputCode, element, weight }`.
**PUT** body (`UpsertElementInputMapRequest`):
```json
{ "inputKind": "Material", "inputCode": "Wood", "element": "Moc", "weight": 1.0 }
```
| Field | Ghi chú |
|-------|---------|
| `inputKind` | enum `ElementInputKind`: `Color` / `Material` / `Shape` |
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
