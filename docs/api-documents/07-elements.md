# 07 — Elements

[← Mục lục](./README.md)

Controller: `ElementsController` · Route gốc: `/api/elements` · Mặc định `[Authorize(Policy = ManagerOrAbove)]`; **đọc Public**.

Bảng tra cứu ngũ hành (`elements.code`). Ngũ hành cố định 5 hành — **KHÔNG thêm hành mới**, **KHÔNG đổi code** (engine gợi ý tham chiếu). Chỉ sửa tên hiển thị / bật-tắt / thứ tự.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/elements` | Public | Danh sách ngũ hành |
| PUT | `/api/elements/{code}` | ManagerOrAbove | Sửa tên hiển thị / bật-tắt / thứ tự |

---

## GET `/api/elements`
Query: `includeInactive` (bool, mặc định `false`).
**Response `data`** = mảng `LookupItemResponse`:
```json
[{ "code": "Kim", "name": "Kim (Metal)", "isActive": true, "sortOrder": 1 }]
```
> Các code cố định: `Kim`, `Moc`, `Thuy`, `Hoa`, `Tho`.

## PUT `/api/elements/{code}`
**Request body** (`UpdateLookupRequest`) — `code` giữ nguyên:
```json
{ "name": "Kim (Metal)", "isActive": true, "sortOrder": 1 }
```

---

[← Vibes](./06-vibes.md) · [Tiếp: Cart →](./08-cart.md)
