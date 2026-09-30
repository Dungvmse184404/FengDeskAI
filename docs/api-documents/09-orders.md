# 09 — Orders

[← Mục lục](./README.md)

Controller: `OrdersController` · Route gốc: `/api/orders` · Mặc định `[Authorize]`.

- **Customer:** checkout từ giỏ, xem đơn của mình, hủy đơn.
- **Vendor (owner/staff store):** xem & cập nhật trạng thái delivery của store mình.
- **Admin:** xem tất cả đơn.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| POST | `/api/orders` | Authenticated | Checkout tạo đơn |
| POST | `/api/orders/shipping-fee-preview` | Authenticated | Xem trước phí ship (không tạo đơn) |
| GET | `/api/orders` | Authenticated | Đơn của tôi (paged) |
| GET | `/api/orders/all` | AdminOnly | Tất cả đơn (paged) |
| GET | `/api/orders/{id}` | Authenticated* | Chi tiết đơn |
| POST | `/api/orders/{id}/cancel` | Authenticated | Hủy đơn (chỉ đơn `Pending`) |
| POST | `/api/orders/{id}/confirm-received` | Chủ đơn | Khách xác nhận đã nhận các kiện đang giao |
| GET | `/api/orders/stores/{storeId}/deliveries` | Owner/Staff/Admin | Delivery của 1 store (paged) |
| GET | `/api/orders/deliveries/{deliveryId}/detail` | Customer sở hữu order / Owner/assigned Staff/Admin | Chi tiết delivery, gồm delivery đổi hàng |
| PATCH | `/api/orders/deliveries/{deliveryId}/status` | Owner/Staff/Admin | Cập nhật trạng thái delivery |

> *`GET /{id}`: customer chỉ xem đơn của mình; Staff trở lên xem được mọi đơn.

---

## POST `/api/orders`

Checkout. **Request body** (`CheckoutRequest`)
```json
{
  "shippingAddressId": "guid",
  "note": "Giao giờ hành chính",
  "items": [{ "productItemId": "guid", "quantity": 2 }],
  "paymentMethod": "PayOS",
  "voucherCode": "FREESHIP500"
}
```
| Field | Ghi chú |
|-------|---------|
| `shippingAddressId` | Bỏ trống / `Guid.Empty` = dùng địa chỉ mặc định |
| `items` | Bỏ trống = đặt toàn bộ giỏ; món trùng giỏ bị xóa khỏi giỏ sau khi đặt |
| `paymentMethod` | `PayOS` (online, hết hạn sau 15') hoặc `COD` |
| `voucherCode` | Tuỳ chọn. Bỏ trống ⇒ BE tự áp voucher tự động có lợi nhất (vd FREESHIP500). Có nhập mà không dùng được ⇒ `422` kèm lý do; lượt cuối vừa bị lấy ⇒ `409`. Xem [28-vouchers](./28-vouchers.md) |

`totalAmount = subtotal + totalShippingFee − shippingDiscount`. Tiền theo từng vườn (tiền hàng, phí ship, khoản
giảm) được chốt vào `order_store_charges` lúc đặt; đơn PayOS sinh delivery lúc webhook dùng lại đúng số đó.

**Response `data`** = `OrderDetailResponse` (xem dưới).

> **`deliveries[].shippingFee` = phí ship KHÁCH trả** cho phần hàng của vườn đó, cố định từ lúc đặt; Σ các delivery
> = `totalShippingFee` (28/09/2026). Trước đây trường này bị ghi đè bằng phí nhà vận chuyển thực tính khi tạo vận
> đơn, và bằng 0 với đơn PayOS — nay phí GHN thật nằm ở cột nội bộ `deliveries.carrier_shipping_fee` (không trả
> ra API), còn đơn PayOS được chia `totalShippingFee` theo tỉ trọng tiền hàng. Xem
> [`platform-fee-ledger.md`](../adr/platform-fee-ledger.md) §4.

---

## POST `/api/orders/{id}/confirm-received`

Khách xác nhận đã nhận hàng (28/09/2026). Mọi delivery **`Shipped`** của đơn chuyển `Delivered`, đi đúng đường vendor
cập nhật tay: `DeliveredAt`, progress log nguồn `Customer`, rollup trạng thái đơn, thông báo, **ghi sổ cái**.

| Lỗi | Khi nào |
|---|---|
| `404` | Không phải đơn của mình (lọc theo chủ đơn ngay ở truy vấn) |
| `409` | Không có kiện nào đang giao — hàng chưa gửi (Pending/Confirmed/Preparing) không được xác nhận, tránh khách tự mở cửa sổ hoàn tiền cho hàng chưa rời vườn |

> Thay cho việc FE gọi `/api/dev/deliveries/...` trước đây. Các endpoint dev đó **chỉ mở ở Development** (trả `404`
> ở môi trường khác); vendor đẩy trạng thái giao qua `PATCH /api/orders/deliveries/{id}/status`.

---

## POST `/api/orders/shipping-fee-preview`

Xem trước phí ship trước khi đặt (không tạo đơn). Body = `CheckoutRequest`.
**Response `data`** = `ShippingFeePreviewResponse`:
```json
{
  "subtotal": 620000, "totalShippingFee": 30000, "shippingDiscount": 30000, "totalAmount": 620000,
  "appliedVoucher": { "code": "FREESHIP500", "name": "...", "shippingDiscount": 30000 },
  "voucherMessage": null,   // lý do khi mã khách nhập không dùng được (preview vẫn trả phí)
  "stores": [{ "storeId": "guid", "storeName": "...", "subtotal": 620000, "shippingFee": 30000, "shippingDiscount": 30000 }]
}
```

---

## GET `/api/orders` · GET `/api/orders/all`

Paged. `data` = `PagedResult<OrderListItemResponse>`:
```json
{
  "items": [{
    "id": "guid", "customerId": "guid", "status": "Paid", "paymentMethod": "PayOS",
    "subtotal": 320000, "totalShippingFee": 30000, "totalAmount": 350000,
    "deliveryCount": 1, "createdAt": "..."
  }],
  "page": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1
}
```

---

## GET `/api/orders/{id}`

**Response `data`** = `OrderDetailResponse`:
```json
{
  "id": "guid", "customerId": "guid", "shippingAddressId": "guid",
  "status": "Processing", "paymentMethod": "PayOS",
  "subtotal": 320000, "totalShippingFee": 30000, "totalAmount": 350000, "note": "...",
  "createdAt": "...",
  "items": [{ "id": "guid", "productItemId": "guid", "productId": "guid", "productAvailable": true,
              "deliveryId": "guid", "productName": "...", "variantName": "Chậu sứ", "imageUrl": "...",
              "unitPrice": 120000, "quantity": 2, "lineTotal": 240000 }],
  "deliveries": [{ "id": "guid", "gardenStoreId": "guid", "storeName": "...",
                   "status": "Shipped", "shippingFee": 30000, "subtotal": 320000,
                   "trackingCode": "...", "shippingProvider": "GHN",
                   "shippedAt": "...", "deliveredAt": null, "estimatedDeliveryDate": "..." }],
  "statusLogs": [{ "fromStatus": "Pending", "toStatus": "Paid", "note": null, "changedAt": "..." }]
}
```

> **Món trong đơn là ảnh chụp lúc đặt** (29/09/2026): tên, biến thể, ảnh, SKU, cửa hàng lưu ngay trên `order_items`,
> nên đơn hiển thị đủ dù sản phẩm sau đó bị xoá. `productAvailable: false` = sản phẩm đã bị xoá (mềm hoặc vĩnh viễn) —
> FE không dẫn link / mua lại / đánh giá; xoá vĩnh viễn thì `productItemId` = `null`. Xem [ADR](../adr/product-deletion.md).
> `items[].deliveryId` = `null` khi đơn online chưa thanh toán (delivery chưa tạo).

---

## POST `/api/orders/{id}/cancel`
Hủy đơn (customer chủ đơn).

---

## Vendor — Delivery

**GET `/api/orders/stores/{storeId}/deliveries`** (paged) — yêu cầu owner/staff store đó hoặc admin. `data` = `PagedResult<StoreDeliveryResponse>`:
```json
{ "id": "guid", "orderId": "guid", "status": "Confirmed", "shippingFee": 30000,
  "subtotal": 320000, "trackingCode": null, "createdAt": "..." }
```

**PATCH `/api/orders/deliveries/{deliveryId}/status`** — body `UpdateDeliveryStatusRequest`:
```json
{ "status": "Shipped", "trackingCode": "GHN123", "shippingProvider": "GHN", "note": "..." }
```
Xem trạng thái hợp lệ ở [Appendix → DeliveryStatus](./99-appendix-models.md).

---

[← Cart](./08-cart.md) · [Tiếp: Payments →](./10-payments.md)
