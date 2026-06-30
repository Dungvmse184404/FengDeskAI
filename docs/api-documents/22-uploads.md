# 22 — Uploads

[← Mục lục](./README.md)

Controller: `UploadsController` · Route gốc: `/api/uploads` · **`[Authorize]`**.

Upload ảnh dùng chung — trả URL công khai để FE đính vào entity sau (vd ảnh sản phẩm lúc tạo khi chưa có `productId`).

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| POST | `/api/uploads` | Authenticated | Upload 1 ảnh |

---

## POST `/api/uploads`

`multipart/form-data`, field **`file`** (ảnh). Thiếu file / file rỗng → `400`.

**Response `data`** = URL công khai (string):
```json
"https://storage.example.com/uploads/abc.jpg"
```

---

[← Notifications](./21-notifications.md) · [Tiếp: Ping →](./23-ping.md)
