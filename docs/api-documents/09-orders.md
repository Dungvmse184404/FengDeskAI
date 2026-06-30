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
| POST | `/api/orders/{id}/cancel` | Authenticated | Hủy đơn |
| GET | `/api/orders/stores/{storeId}/deliveries` | Owner/Staff/Admin | Delivery của 1 store (paged) |
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
  "paymentMethod": "PayOS"
}
```
| Field | Ghi chú |
|-------|---------|
| `shippingAddressId` | Bỏ trống / `Guid.Empty` = dùng địa chỉ mặc định |
| `items` | Bỏ trống = đặt toàn bộ giỏ; món trùng giỏ bị xóa khỏi giỏ sau khi đặt |
| `paymentMethod` | `PayOS` (online, hết hạn sau 15') hoặc `COD` |

**Response `data`** = `OrderDetailResponse` (xem dưới).

---

## POST `/api/orders/shipping-fee-preview`

Xem trước phí ship trước khi đặt (không tạo đơn). Body = `CheckoutRequest`.
**Response `data`** = `ShippingFeePreviewResponse`:
```json
{
  "subtotal": 320000, "totalShippingFee": 30000, "totalAmount": 350000,
  "stores": [{ "storeId": "guid", "storeName": "...", "subtotal": 320000, "shippingFee": 30000 }]
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
  "items": [{ "id": "guid", "productItemId": "guid", "deliveryId": "guid",
              "productName": "...", "unitPrice": 120000, "quantity": 2, "lineTotal": 240000 }],
  "deliveries": [{ "id": "guid", "gardenStoreId": "guid", "storeName": "...",
                   "status": "Shipped", "shippingFee": 30000, "subtotal": 320000,
                   "trackingCode": "...", "shippingProvider": "GHN",
                   "shippedAt": "...", "deliveredAt": null, "estimatedDeliveryDate": "..." }],
  "statusLogs": [{ "fromStatus": "Pending", "toStatus": "Paid", "note": null, "changedAt": "..." }]
}
```
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
