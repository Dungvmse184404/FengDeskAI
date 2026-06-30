# 06 — Vibes

[← Mục lục](./README.md)

Controller: `VibesController` · Route gốc: `/api/vibes` · Mặc định `[Authorize(Policy = ManagerOrAbove)]`; **đọc Public**.

Bảng tra cứu vibe (`vibes.code`) — thêm/sửa không cần deploy.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/vibes` | Public | Danh sách vibe |
| POST | `/api/vibes` | ManagerOrAbove | Thêm vibe |
| PUT | `/api/vibes/{code}` | ManagerOrAbove | Sửa (đổi tên/bật-tắt/thứ tự) |

---

## GET `/api/vibes`
Query: `includeInactive` (bool, mặc định `false`).
**Response `data`** = mảng `LookupItemResponse`:
```json
[{ "code": "Focus", "name": "Tập trung", "isActive": true, "sortOrder": 1 }]
```

## POST `/api/vibes`
**Request body** (`CreateLookupRequest`)
```json
{ "code": "Calm", "name": "Thư thái", "sortOrder": 3 }
```

## PUT `/api/vibes/{code}`
**Request body** (`UpdateLookupRequest`)
```json
{ "name": "Thư thái", "isActive": true, "sortOrder": 3 }
```

---

[← Styles](./05-styles.md) · [Tiếp: Elements →](./07-elements.md)
