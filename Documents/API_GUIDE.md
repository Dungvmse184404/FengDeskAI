# FengDeskAI — Hướng dẫn sử dụng API

> Tổng hợp các endpoint hiện có + luồng mua hàng end-to-end (cart → order → payment → shipping).
> Base URL dev: `https://localhost:7016` (xem `Properties/launchSettings.json`). Swagger UI: `/swagger`.

## Quy ước chung

- **Auth**: gửi header `Authorization: Bearer <accessToken>` (lấy từ login/finalize). Swagger có nút Authorize.
- **Response bọc** trong `ServiceResult`: `{ isSuccess, statusCode, message, errors, data }`.
- **Phân trang**: query `?page=1&pageSize=20`. Kết quả `PagedResult`: `{ items, page, pageSize, totalCount, totalPages }`.
- **Phân quyền**: 🟢 public · 🔑 cần đăng nhập · 👮 Manager/Admin · 🛡️ Admin · 🏪 owner/staff của store (kiểm tra ở service).

---

## 1. Auth — `api/auth`

| Method | Path | Quyền | Body |
|---|---|---|---|
| POST | `/api/auth/register/initiate` | 🟢 | `{ "email": "a@b.com" }` |
| POST | `/api/auth/register/verify` | 🟢 | `{ "email": "a@b.com", "otp": "123456" }` → trả `registrationToken` |
| POST | `/api/auth/register/finalize` | 🟢 | `{ "registrationToken": "...", "password": "...", "fullName": "...", "phone": "...", "dateOfBirth": "2000-01-01", "gender": 1 }` |
| POST | `/api/auth/login` | 🟢 | `{ "email": "...", "password": "..." }` → `{ accessToken, refreshToken, user }` |
| POST | `/api/auth/refresh` | 🟢 | `{ "refreshToken": "..." }` |
| POST | `/api/auth/logout` | 🔑 | `{ "refreshToken": "..." }` |
| GET | `/api/auth/me` | 🔑 | – |

`gender`: 0=Unspecified, 1=Male, 2=Female, 3=Other.

---

## 2. Geography

### Tra cứu hành chính — `api/locations` (🟢)
- `GET /api/locations/provinces`
- `GET /api/locations/provinces/{provinceId}/districts`
- `GET /api/locations/districts/{districtId}/wards`

> Chạy seed để có dữ liệu: `dotnet run --project src/FengDeskAI.WebAPI -- seed`.

### Sổ địa chỉ — `api/addresses` (🔑 của user)
| Method | Path | Body |
|---|---|---|
| GET | `/api/addresses` | – |
| GET | `/api/addresses/{id}` | – |
| POST | `/api/addresses` | `{ "wardId":"...", "streetAddress":"12 Lê Lợi", "recipientName":"Dung", "recipientPhone":"09xx", "isDefault":true, "label":"Nhà" }` |
| PUT | `/api/addresses/{id}` | như POST (không có isDefault) |
| PATCH | `/api/addresses/{id}/set-default` | – |
| DELETE | `/api/addresses/{id}` | – |

---

## 3. Vendor / Stores — `api/stores`

| Method | Path | Quyền | Ghi chú |
|---|---|---|---|
| GET | `/api/stores` | 🟢 | danh sách store active |
| GET | `/api/stores/{id}` | 🟢 | chi tiết + địa chỉ |
| POST | `/api/stores` | 🛡️ Admin | `{ "ownerUserId":"...", "name":"...", "hotline":"1900...", "description":"...", "openingHours":"08:00-21:00" }` |
| PUT | `/api/stores/{id}` | 🏪 owner/admin | `{ "name":"...", "hotline":"...", "description":"...", "openingHours":"...", "isActive":true }` |
| PUT | `/api/stores/{id}/address` | 🏪 owner/admin | `{ "wardId":"...", "streetAddress":"...", "latitude":null, "longitude":null }` |
| GET | `/api/stores/{id}/staff` | 🏪 owner/admin | danh sách nhân viên |
| POST | `/api/stores/{id}/staff` | 🏪 owner/admin | `{ "staffId":"<userId>" }` |
| DELETE | `/api/stores/{id}/staff/{assignmentId}` | 🏪 owner/admin | gỡ phân công |

---

## 4. Catalog

### Categories — `api/categories` (GET 🟢, CRUD 👮)
- `GET /api/categories` · `GET /api/categories/{id}`
- `POST` `{ "name":"Cây để bàn", "description":"...", "parentId":null }`
- `PUT /api/categories/{id}` · `DELETE /api/categories/{id}`

### Tags — `api/tags` (GET 🟢, CRUD 👮)
- `GET /api/tags`
- `POST` `{ "name":"Thủy", "description":"..." }`
- `PUT /api/tags/{id}` · `DELETE /api/tags/{id}`

### Products — `api/products`
| Method | Path | Quyền | Ghi chú |
|---|---|---|---|
| GET | `/api/products?storeId=&categoryId=&tagId=&search=&page=&pageSize=` | 🟢 | list card (có `minPrice`, ảnh chính) + paging |
| GET | `/api/products/{id}` | 🟢 | detail: items (SKU), images, categories, tags |
| POST | `/api/products` | 🏪 | tạo product kèm items/images/links (xem body dưới) |
| PUT | `/api/products/{id}` | 🏪 | `{ "name":"...", "description":"...", "isActive":true }` |
| DELETE | `/api/products/{id}` | 🏪 | soft-delete |
| POST | `/api/products/{id}/items` | 🏪 | `{ "name":"Size L", "price":250000, "stock":30, "sku":"KT-L" }` |
| PUT | `/api/products/{id}/items/{itemId}` | 🏪 | như trên |
| DELETE | `/api/products/{id}/items/{itemId}` | 🏪 | |
| POST | `/api/products/{id}/images` | 🏪 | `{ "url":"https://...", "sortOrder":0 }` |
| DELETE | `/api/products/{id}/images/{imageId}` | 🏪 | |
| PUT | `/api/products/{id}/categories` | 🏪 | `{ "categoryIds":["...","..."] }` (thay toàn bộ) |
| PUT | `/api/products/{id}/tags` | 🏪 | `{ "tagIds":["...","..."] }` (thay toàn bộ) |

**Body tạo product:**
```jsonc
POST /api/products
{
  "gardenStoreId": "<store-id>",
  "name": "Cây Kim Tiền để bàn",
  "description": "...",
  "items":  [ { "name":"Chậu trắng", "price":250000, "stock":30, "sku":"KT-W" } ],
  "images": [ { "url":"https://...", "sortOrder":0 } ],
  "categoryIds": ["<cat-id>"],
  "tagIds": ["<tag-id>"]
}
```

> **Lưu ý**: `productItemId` mà cart/order dùng = `items[].id` trong product detail (mỗi SKU mang giá + tồn kho riêng).

---

## 5. Cart — `api/cart` (🔑 của user)

| Method | Path | Body | Việc |
|---|---|---|---|
| GET | `/api/cart` | – | giỏ + `subtotal`; mỗi dòng có `id` (cartItemId) |
| POST | `/api/cart/items` | `{ "productItemId":"...", "quantity":2 }` | thêm/cộng dồn dòng |
| PUT | `/api/cart/items/{itemId}` | `{ "quantity":3 }` | đổi số lượng (0 = xóa dòng) |
| DELETE | `/api/cart/items/{itemId}` | – | xóa 1 dòng |
| DELETE | `/api/cart` | – | xóa sạch giỏ |

---

## 6. Orders — `api/orders` (🔑)

### Đặt đơn (checkout) — đọc **giỏ hàng** để tạo đơn
```jsonc
POST /api/orders
{
  "shippingAddressId": "<addr-id>",
  "note": "giao giờ hành chính",
  "cartItemIds": ["<cartItem-1>", "<cartItem-2>"]   // optional: chỉ mua các dòng này
}
```
- **`cartItemIds` bỏ trống** → đặt **cả giỏ**. **Có** → chỉ đặt các dòng đó, phần còn lại **giữ trong giỏ**.
- Server: validate tồn kho → **gom theo store, mỗi store 1 delivery** → snapshot giá vào `order_items` → trừ kho → xóa dòng đã đặt → trả `OrderDetailResponse` (status `Pending`).

| Method | Path | Quyền | Việc |
|---|---|---|---|
| POST | `/api/orders` | 🔑 customer | checkout (trên) |
| GET | `/api/orders?page=&pageSize=` | 🔑 customer | đơn của tôi (paged) |
| GET | `/api/orders/{id}` | 🔑 customer | chi tiết: items + deliveries + statusLogs |
| POST | `/api/orders/{id}/cancel` | 🔑 customer | hủy đơn (chỉ khi `Pending`) → hoàn kho |
| GET | `/api/orders/stores/{storeId}/deliveries?page=&pageSize=` | 🏪 vendor | delivery của store mình |
| PATCH | `/api/orders/deliveries/{deliveryId}/status` | 🏪 vendor | `{ "status":"Shipped", "trackingCode":"...", "shippingProvider":"...", "note":"..." }` |

Chuyển trạng thái delivery hợp lệ: `Pending→Confirmed→Preparing→Shipped→Delivered` (hoặc `→Cancelled`, `Shipped/Delivered→Returned`). Khi **tất cả** delivery `Delivered` → order tự `Completed`.

---

## 7. Payments (PayOS) — `api/payments`

| Method | Path | Quyền | Việc |
|---|---|---|---|
| POST | `/api/payments/{orderId}` | 🔑 customer | tạo link PayOS → `{ checkoutUrl, qrCode, orderCode, amount }` (order phải `Pending`) |
| GET | `/api/payments/{orderId}` | 🔑 customer | trạng thái thanh toán |
| POST | `/api/payments/{orderId}/cancel` | 🔑 customer | `{ "reason":"đổi ý" }` → hủy link PayOS + order/deliveries/transaction → `Cancelled` + hoàn kho |
| POST | `/api/payments/payos/webhook` | 🟢 PayOS gọi | verify chữ ký → order `Paid` + **tự tạo shipment** mỗi delivery |

**Cấu hình** (trong `appsettings.json`, đang `.gitignore`):
```jsonc
"PayOS": {
  "BaseUrl": "https://api-merchant.payos.vn",
  "ClientId": "...", "ApiKey": "...", "ChecksumKey": "...",
  "ReturnUrl": "http://localhost:5173/payment/success",
  "CancelUrl": "http://localhost:5173/payment/cancel"
}
```
> Webhook cần URL public HTTPS → dùng `ngrok http <port>` rồi đăng ký `https://<ngrok>/api/payments/payos/webhook` trong dashboard PayOS.

---

## 8. Shipping — `api/shipping`

| Method | Path | Quyền | Việc |
|---|---|---|---|
| POST | `/api/shipping/webhook` | 🟢 + header `X-Webhook-Secret` | nhà vận chuyển báo trạng thái → cập nhật delivery |
| GET | `/api/shipping/deliveries/{deliveryId}/progress` | 🏪 vendor/admin | lịch sử tiến trình giao |

Body webhook (giả lập nhà vận chuyển):
```jsonc
POST /api/shipping/webhook    (header: X-Webhook-Secret: <ShippingWebhook:Secret>)
{
  "provider": "ShopeeExpress",
  "eventType": "delivered",
  "deliveryId": "<delivery-id>",          // hoặc providerOrderId
  "newStatus": "Delivered",
  "trackingCode": "VN...SPE"
}
```
> Hiện shipping dùng **MockShopee** (tạo tracking giả khi order Paid). Thay impl `IShippingProvider` khi có API Shopee thật.

---

## 9. 🛒 Luồng mua hàng end-to-end (test nhanh)

```http
# 0. Đăng nhập (hoặc dùng vendor seed: vendor@fengdesk.local / Vendor@123)
POST /api/auth/login                          { email, password }   → accessToken

# 1. Chọn sản phẩm → lấy productItemId
GET  /api/products                            → chọn product
GET  /api/products/{id}                        → items[].id = productItemId

# 2. Thêm vào giỏ
POST /api/cart/items                          { productItemId, quantity }
GET  /api/cart                                 → xem giỏ, lấy cartItem id

# 3. Địa chỉ giao
GET  /api/locations/provinces → .../districts → .../wards    → wardId
POST /api/addresses                           { wardId, streetAddress, recipientName, recipientPhone }

# 4. Đặt đơn (cả giỏ hoặc chọn lọc bằng cartItemIds)
POST /api/orders                              { shippingAddressId, note, cartItemIds? }   → orderId

# 5. Thanh toán
POST /api/payments/{orderId}                  → checkoutUrl → khách trả tiền
   PayOS → POST /api/payments/payos/webhook   → order Paid + tự tạo shipment (delivery Confirmed)

# 6. Vendor xử lý giao hàng
GET   /api/orders/stores/{storeId}/deliveries
PATCH /api/orders/deliveries/{deliveryId}/status   { status: "Shipped" } → ... "Delivered"
   → khi mọi delivery Delivered: order tự Completed
```

---

## 10. Tài khoản & dữ liệu seed

Chạy `dotnet run --project src/FengDeskAI.WebAPI -- seed` (idempotent):
- **Geography**: tỉnh/quận/phường mẫu (Hà Nội, Đà Nẵng, HCM).
- **Vendor demo**: `vendor@fengdesk.local` / `Vendor@123` (role Manager) sở hữu store "Vườn Phong Thủy Demo".
- **Catalog**: 4 categories, 7 tags, 5 products (kèm items/ảnh/links).

---

## 11. Bảng trạng thái (enum)

| Enum | Giá trị |
|---|---|
| **OrderStatus** | `Pending` → `Paid` → `Processing` → `Completed` · `Cancelled` |
| **DeliveryStatus** | `Pending` → `Confirmed` → `Preparing` → `Shipped` → `Delivered` · `Cancelled` · `Returned` |
| **PaymentStatus** | `Pending` → `Paid` · `Cancelled` · `Failed` · `Expired` |

> Status order/delivery là đề xuất (State Diagram chưa chốt) — điều chỉnh khi cần.

---

*Cập nhật: 2026-06-09. Pending chưa làm: Customer care (reviews/support/after-sales), Notification, Recommendation (AI).*
