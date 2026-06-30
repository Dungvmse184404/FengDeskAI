# 04 — Tags

[← Mục lục](./README.md)

Controller: `TagsController` · Route gốc: `/api/tags` · **Đọc Public**; ghi cho Manager trở lên ( kiểm ở service).

Tag sản phẩm (gồm cả thuộc tính phong thủy).

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/tags` | Public | Danh sách tag |
| POST | `/api/tags` | Manager+ | Tạo tag |
| PUT | `/api/tags/{id}` | Manager+ | Cập nhật tag |
| DELETE | `/api/tags/{id}` | Manager+ | Xóa tag |

---

## GET `/api/tags`
**Response `data`** = mảng `TagResponse`:
```json
[{ "id": "guid", "name": "Hợp mệnh Hỏa", "description": "..." }]
```

## POST `/api/tags`
**Request body** (`CreateTagRequest`)
```json
{ "name": "Hợp mệnh Hỏa", "description": "..." }
```

## PUT `/api/tags/{id}`
**Request body** (`UpdateTagRequest`)
```json
{ "name": "...", "description": "..." }
```

## DELETE `/api/tags/{id}`
Xóa tag.

---

[← Categories](./03-categories.md) · [Tiếp: Styles →](./05-styles.md)
