# 19 — Reviews

[← Mục lục](./README.md)

Controller: `ReviewController` · Route gốc: `/api/Review` · Mặc định `[Authorize]`; **danh sách + tóm tắt điểm là Public**.

Đánh giá gắn với **dòng đơn hàng** (`reviews.order_item_id`), không gắn với "sản phẩm nói chung":

- Chỉ đánh giá được khi phần hàng của cửa hàng đó **đã giao** (`deliveries.status = Delivered`).
- Dòng đơn **đã hoàn hàng** (lệnh hoàn tiền của dòng đó `Completed`, hoặc delivery `Returned`) → không đánh giá; FE ghi chú "Đã hoàn hàng".
- **Mỗi dòng đơn một đánh giá** (unique index `UX_reviews_order_item`, lọc `is_deleted = FALSE`) → mua lại sản phẩm thì đánh giá lần nữa.
- Điểm cửa hàng = trung bình mọi đánh giá có `garden_store_id` của cửa hàng (cột chụp — đánh giá của sản phẩm đã xoá vẫn tính).

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/Review` | Public | Danh sách đánh giá, phân trang, lọc `productId` / `storeId` |
| GET | `/api/Review/summary` | Public | Trung bình + phân bố sao của **một** sản phẩm hoặc **một** cửa hàng |
| GET | `/api/Review/my` | Authenticated | Đánh giá của tôi |
| GET | `/api/Review/eligibility?productId=` | Authenticated | Tôi có được đánh giá sản phẩm này không |
| GET | `/api/Review/orders/{orderId}/items` | Authenticated (chủ đơn) | Trạng thái đánh giá từng dòng của một đơn |
| POST | `/api/Review` | Authenticated | Tạo đánh giá |
| PUT | `/api/Review/{id}` | Authenticated (chủ) | Sửa đánh giá |
| DELETE | `/api/Review/{id}` | Authenticated (chủ) | Xóa (mềm) đánh giá — dòng đơn đó đánh giá lại được |

---

## GET `/api/Review`
Query: `productId?`, `storeId?`, `page` (mặc định 1), `pageSize` (mặc định 20, tối đa 100). Mới nhất trước.

`data` = `PagedResult<ReviewResponse>`:
```json
{
  "items": [{
    "id": "guid", "content": "Sản phẩm tốt", "rating": 5,
    "createdAt": "...", "updatedAt": null,
    "userId": "guid", "user": { "id": "guid", "fullName": "Nguyễn Văn A" },
    "productId": "guid", "productName": "Cây kim tiền",
    "gardenStoreId": "guid", "orderItemId": "guid", "variantName": "Chậu sứ nhỏ"
  }],
  "page": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1
}
```
> `user` chỉ có `id` + `fullName` (không trả entity User). `productId` null khi sản phẩm đã bị xoá cứng.
> Đánh giá cũ tạo trước khi gắn dòng đơn có thể có `orderItemId` / `variantName` null.

## GET `/api/Review/summary`
Query: **đúng một** trong `productId` / `storeId` (thiếu hoặc cả hai → `400`).
```json
{ "average": 3.5, "count": 2, "distribution": [0, 0, 1, 1, 0] }
```
> `distribution[0]` = số lượt 1 sao … `distribution[4]` = 5 sao. `average` làm tròn 1 chữ số; `0` khi chưa có đánh giá.

## GET `/api/Review/my`
`data` = mảng `ReviewResponse` (không phân trang).

## GET `/api/Review/eligibility?productId=`
```json
{ "canReview": true, "status": "Reviewable", "orderItemId": "guid" }
```
> `status` null = chưa từng mua. Khi `canReview = false`, `status` là lý do — ưu tiên `NotDelivered` → `Reviewed` →
> `Returned` → `ProductUnavailable`.

## GET `/api/Review/orders/{orderId}/items`
Mảng các dòng của đơn (đơn của người khác → mảng rỗng):
```json
[{
  "orderItemId": "guid", "orderId": "guid", "productId": "guid",
  "productName": "Cây kim tiền", "variantName": "Chậu sứ nhỏ", "imageUrl": "https://...",
  "status": "Returned", "reviewId": null
}]
```

`status` (`ReviewEligibilityStatus`):

| Giá trị | Nghĩa |
|---|---|
| `Reviewable` | Đã giao, chưa đánh giá |
| `Reviewed` | Dòng này đã có đánh giá (`reviewId`) |
| `Returned` | Đã hoàn hàng — FE ghi chú "Đã hoàn hàng" |
| `NotDelivered` | Phần hàng chưa giao xong |
| `ProductUnavailable` | Sản phẩm đã bị xoá khỏi sàn |

## POST `/api/Review`
**Request body** (`CreateReviewRequest`) — gửi `orderItemId` **hoặc** `productId`:
```json
{ "orderItemId": "guid", "content": "Sản phẩm tốt", "rating": 5 }
{ "productId": "guid", "content": "Sản phẩm tốt", "rating": 5 }
```
> `orderItemId` (trang Đơn hàng) đánh giá đúng dòng đó. Chỉ `productId` (trang sản phẩm) thì BE chọn dòng đơn **mới nhất**
> còn đánh giá được. `userId` lấy từ token.

| Mã | Khi nào |
|---|---|
| `201` | Tạo thành công |
| `400` | Thiếu nội dung, `rating` ngoài 1–5, hoặc thiếu cả `orderItemId` lẫn `productId` |
| `403` | Chưa mua / chưa nhận hàng / đã hoàn hàng / sản phẩm ngừng bán |
| `404` | `orderItemId` không thuộc đơn của bạn, hoặc `productId` không tồn tại |
| `409` | Dòng đơn đã được đánh giá (hoặc mọi lần mua sản phẩm đều đã đánh giá) |

## PUT `/api/Review/{id}`
**Request body** (`UpdateReviewRequest`)
```json
{ "content": "Cập nhật nội dung", "rating": 4 }
```

## DELETE `/api/Review/{id}`
Xóa mềm (chỉ chủ đánh giá). Dòng đơn đó đánh giá lại được.

---

## Dữ liệu cũ
Migration `AddReviewOrderItem` bỏ unique `(user_id, product_id)`, thêm `order_item_id` và gắn đánh giá cũ vào một dòng đơn
của chính người viết (đơn mới nhất trước). `OrderSnapshotBackfillSeeder` chạy lại cùng câu SQL mỗi lần deploy để vá đánh
giá do container cũ tạo trong khoảng migrate → đổi container. Đánh giá không truy được về đơn giữ `order_item_id` null.

---

[← Recommendations](./18-recommendations.md) · [Tiếp: Chat →](./20-chat.md)
