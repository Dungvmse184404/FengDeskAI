# 19 — Reviews

[← Mục lục](./README.md)

Controller: `ReviewController` · Route gốc: `/api/Review` · Mặc định `[Authorize]`; **danh sách tất cả là Public**.

Đánh giá sản phẩm (nội dung + số sao).

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/Review` | Public | Tất cả đánh giá |
| GET | `/api/Review/my` | Authenticated | Đánh giá của tôi |
| POST | `/api/Review` | Authenticated | Tạo đánh giá |
| PUT | `/api/Review/{id}` | Authenticated (chủ) | Sửa đánh giá |
| DELETE | `/api/Review/{id}` | Authenticated (chủ) | Xóa đánh giá |

---

## GET `/api/Review` · `/my`
`data` = mảng `ReviewResponse`:
```json
[{
  "id": "guid", "content": "Sản phẩm tốt", "rating": 5,
  "createdAt": "...", "updatedAt": null,
  "userId": "guid", "productId": "guid"
}]
```

## POST `/api/Review`
**Request body** (`CreateReviewRequest`)
```json
{ "content": "Sản phẩm tốt", "rating": 5, "productId": "guid" }
```
> `userId` lấy từ token (không cần gửi). `rating` thang điểm sao.

## PUT `/api/Review/{id}`
**Request body** (`UpdateReviewRequest`)
```json
{ "content": "Cập nhật nội dung", "rating": 4 }
```

## DELETE `/api/Review/{id}`
Xóa đánh giá (chỉ chủ đánh giá).

---

[← Recommendations](./18-recommendations.md) · [Tiếp: Chat →](./20-chat.md)
