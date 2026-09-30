# ADR — Xoá sản phẩm: người bán xoá mềm, Manager xoá vĩnh viễn

**Trạng thái:** ĐÃ LÀM (29/09/2026)

## 1. Bối cảnh

Trước đây `DELETE /api/products/{id}` chỉ đánh dấu `products.is_deleted`. Đơn hàng đọc tên biến thể, ảnh, cửa
hàng **qua** `order_items → product_items → products`; bộ lọc xoá mềm ẩn sản phẩm nên **món biến mất khỏi đơn cũ**
(API test tái hiện: đơn đã giao hiện 0 món nhưng tạm tính vẫn 150 000đ). Biến thể không bị xoá theo nên vẫn nằm
trong giỏ khách. Xoá cứng thì không làm được vì `order_items`, `cart_items`, `return_items`, `recommendation_items`
khoá ngoại `Restrict`.

## 2. Quyết định (chốt với chủ dự án)

| # | Quy tắc |
|---|---|
| 1 | **Đơn hàng không phụ thuộc sản phẩm.** `order_items` chụp lúc đặt: `product_id`, `garden_store_id`, `variant_name`, `sku`, `image_url` (cùng `product_name`, `unit_price` đã có). Mọi màn đơn / tạo delivery lúc webhook / thống kê đọc cột chụp |
| 2 | **Người bán xoá = xoá mềm** (`IsDeleted`) sản phẩm **và** biến thể, gỡ khỏi mọi giỏ hàng. Không dùng `IsActive` — `IsActive = false` là "Ngừng bán" (bật lại được) |
| 3 | **Manager xoá = xoá vĩnh viễn** (`DELETE /api/products/{id}/permanent`, ManagerOrAbove, mọi cửa hàng, kể cả sản phẩm người bán đã xoá mềm) |
| 4 | Cả hai **bị chặn (409) khi còn đơn chưa đóng**: đơn `Pending/Paid/Processing/Shipping`, đã giao nhưng còn trong khoảng đổi trả (`PayoutPolicy.HoldDays`), có yêu cầu trả/đổi hàng chưa kết thúc, hoặc biến thể đang là hàng đổi của ticket chưa xong — hoàn kho, trả hàng, đổi hàng còn cần biến thể. FE đề xuất "Ngừng bán" thay thế |
| 5 | **Đánh giá được giữ** khi xoá cứng: `reviews.product_id` SET NULL; `reviews.product_name`, `reviews.garden_store_id` chụp lúc viết — điểm đánh giá cửa hàng vẫn tính |

## 3. Dữ liệu (migration `AddOrderLineSnapshots`, chỉ cộng thêm)

| Bảng | Thay đổi |
|---|---|
| `order_items` | + `product_id`, `garden_store_id` (index), `variant_name`, `sku`, `image_url`; `product_item_id` cho phép NULL, FK → **SET NULL** |
| `reviews` | + `product_name`, `garden_store_id` (index); `product_id` cho phép NULL, FK → **SET NULL** |
| `cart_items` → `product_items` | FK → **CASCADE** |
| `recommendation_items` → `products` | FK → **CASCADE** |
| `return_items.exchange_product_item_id` | FK → **SET NULL** |

Migration điền bù dữ liệu cũ (đọc bằng SQL thô nên thấy cả sản phẩm đã xoá mềm — đơn cũ bị "mất món" hiện lại đủ).
CI migrate **trước** khi đổi container ⇒ container cũ có thể tạo vài dòng chưa có cột chụp: `OrderSnapshotBackfillSeeder`
chạy lại cùng câu SQL mỗi lần deploy (idempotent), và các truy vấn đọc cột chụp đều có đường lùi qua sản phẩm.

Xoá cứng dùng một câu `DELETE` (`ExecuteDeleteAsync`, bỏ qua cơ chế xoá mềm); DB tự xoá biến thể, ảnh, thuộc tính,
dòng giỏ và SET NULL phần còn lại. Ảnh trên Supabase Storage **chưa** được dọn.

## 4. API / FE

- `DELETE /api/products/{id}` (chủ cửa hàng) — xoá mềm; `DELETE /api/products/{id}/permanent` (Manager) — xoá cứng.
  Cả hai: 409 khi còn đơn chưa đóng. Xoá biến thể (`DELETE …/items/{itemId}`) cùng quy tắc.
- `OrderItemResponse`: `productItemId`/`productId` có thể null, thêm `productAvailable`.
- FE: nút thùng rác trên thẻ sản phẩm ở trang cửa hàng (xoá mềm); trang Quản lý sản phẩm của Manager = xoá vĩnh
  viễn (`DeleteProductDialog permanent`); hộp thoại gặp 409 thì đề xuất "Ngừng bán". Đơn hàng hiện "Sản phẩm không
  còn bán", không dẫn link, không mua lại / đánh giá món đó.

## 5. Kiểm thử

- API: CAT-09b (đơn còn trong khoảng đổi trả ⇒ 409 cả xoá mềm/cứng/biến thể), CAT-09c (chưa có đơn ⇒ rời giỏ),
  CAT-09d (đơn đã đóng: xoá mềm rồi xoá cứng, món trong đơn y nguyên tên/biến thể/ảnh/giá, giỏ khách bị gỡ, đánh
  giá còn), CAT-09e (chủ cửa hàng không xoá cứng được).
- E2E `delete-product.spec.ts`: người bán xoá mềm; đơn đang mở ⇒ Ngừng bán; Manager xoá vĩnh viễn rồi khách mở lại
  đơn vẫn thấy đủ món.

## 6. Chưa làm

- Dọn ảnh trên Storage khi xoá cứng.
- Màn Manager xem/khôi phục sản phẩm người bán đã xoá mềm (hiện Manager chỉ thấy sản phẩm còn sống trong danh sách).
- ERD (`Documents/ERD/SEP490_FengDeskAI.drawio`) chưa cập nhật các cột mới.
