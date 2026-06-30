# 05 — Styles

[← Mục lục](./README.md)

Controller: `StylesController` · Route gốc: `/api/styles` · Mặc định `[Authorize(Policy = ManagerOrAbove)]`; **đọc Public**.

Bảng tra cứu phong cách (`styles.code`) — thêm/sửa không cần deploy. Dùng để đổ dropdown chọn `styleCode` khi tạo workspace/sản phẩm.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/styles` | Public | Danh sách phong cách |
| POST | `/api/styles` | ManagerOrAbove | Thêm phong cách |
| PUT | `/api/styles/{code}` | ManagerOrAbove | Sửa (đổi tên/bật-tắt/thứ tự) |

---

## GET `/api/styles`
Query: `includeInactive` (bool, mặc định `false`).
**Response `data`** = mảng `LookupItemResponse`:
```json
[{ "code": "Minimal", "name": "Tối giản", "isActive": true, "sortOrder": 1 }]
```

## POST `/api/styles`
**Request body** (`CreateLookupRequest`) — `code` bất biến, không trùng:
```json
{ "code": "Industrial", "name": "Công nghiệp", "sortOrder": 5 }
```

## PUT `/api/styles/{code}`
**Request body** (`UpdateLookupRequest`) — `code` không đổi:
```json
{ "name": "Công nghiệp", "isActive": true, "sortOrder": 5 }
```

---

[← Tags](./04-tags.md) · [Tiếp: Vibes →](./06-vibes.md)
