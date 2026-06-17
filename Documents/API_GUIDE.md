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
| POST | `/api/products/{id}/images` | 🏪 | **multipart/form-data**: field `file` (+ `sortOrder`). Lưu Supabase Storage `Product_images/{id}/`, trả URL |
| DELETE | `/api/products/{id}/images/{imageId}` | 🏪 | xoá bản ghi DB **và** file trên storage |
| PUT | `/api/products/{id}/categories` | 🏪 | `{ "categoryIds":["...","..."] }` (thay toàn bộ) |
| PUT | `/api/products/{id}/tags` | 🏪 | `{ "tagIds":["...","..."] }` (thay toàn bộ) |
| PUT | `/api/products/{id}/feng-shui` | 🏪 | thuộc tính phong thủy (làm ứng viên gợi ý) — xem body dưới |

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
> Ảnh trong body tạo product vẫn nhận URL có sẵn; để **upload tệp** dùng endpoint `POST /api/products/{id}/images` (multipart).

**Body thuộc tính phong thủy** (lưu vào `product_element` nhiều-nhiều + `products.size_class`):
```jsonc
PUT /api/products/{id}/feng-shui
{
  "primaryElement": "Moc",          // hành chính — bắt buộc (Kim|Moc|Thuy|Hoa|Tho)
  "secondaryElements": ["Thuy"],    // hành phụ 0..n (trùng hành chính sẽ bị bỏ)
  "sizeClass": "Small",             // Small | Medium | Large
  "vibes": ["Focus"],
  "styles": ["Scandinavian"]
}
```

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

### Đặt đơn (checkout)
```jsonc
POST /api/orders
{
  // optional — bỏ trống/Guid.Empty thì dùng địa chỉ mặc định của user.
  // Có gửi mà id không thuộc user → lỗi "Địa chỉ giao hàng không hợp lệ".
  "shippingAddressId": "<addr-id>",
  "note": "giao giờ hành chính",

  // optional — đặt theo danh sách product item (mua ngay), KHÔNG cần có trong giỏ.
  // Bỏ trống → đặt TOÀN BỘ giỏ hàng.
  "items": [ { "productItemId": "<id>", "quantity": 2 } ],

  // optional — 0 = PayOS (mặc định, thanh toán online), 1 = COD (trả khi nhận hàng)
  "paymentMethod": 0
}
```
- **`items` có giá trị** → đặt đúng danh sách đó (kể cả sản phẩm chưa thêm vào giỏ). Sau khi đặt, **dòng giỏ trùng `productItemId` bị xóa khỏi giỏ**; món không có trong giỏ thì thôi. *(Muốn mua một phần giỏ: gửi đúng các món đó trong `items`.)*
- **`items` trống** → đặt **cả giỏ**.
- Server: validate tồn kho → snapshot giá vào `order_items` → trừ kho → dọn giỏ → trả `OrderDetailResponse` (status `Pending`).
- **Delivery (gom theo store, mỗi store 1 delivery)**:
  - **COD** → tạo delivery **ngay khi đặt** (vendor thấy và xác nhận được luôn).
  - **PayOS** → delivery **chỉ tạo khi webhook báo đã thanh toán** (trước đó `order_items.deliveryId = null`).
- **Hết hạn thanh toán**: đơn PayOS quá **15 phút** chưa trả tiền sẽ bị worker chuyển `Expired` (transaction → `Expired`, hủy link PayOS, hoàn kho, ghi statusLog). COD không bị hết hạn. Cấu hình ở `OrderExpiration` trong `appsettings.json`.

| Method | Path | Quyền | Việc |
|---|---|---|---|
| POST | `/api/orders` | 🔑 customer | checkout (trên) |
| GET | `/api/orders?page=&pageSize=` | 🔑 customer | đơn của tôi (paged) |
| GET | `/api/orders/{id}` | 🔑 customer | chi tiết: items + deliveries + statusLogs |
| POST | `/api/orders/{id}/cancel` | 🔑 customer | hủy đơn (chỉ khi `Pending`, chưa thanh toán) → hủy cả link PayOS/transaction còn treo + hoàn kho |
| GET | `/api/orders/stores/{storeId}/deliveries?page=&pageSize=` | 🏪 vendor | delivery của store mình |
| PATCH | `/api/orders/deliveries/{deliveryId}/status` | 🏪 vendor | `{ "status":"Shipped", "trackingCode":"...", "shippingProvider":"...", "note":"..." }` |

Chuyển trạng thái delivery hợp lệ: `Pending→Confirmed→Preparing→Shipped→Delivered` (hoặc `→Cancelled`, `Shipped/Delivered→Returned`). Khi **tất cả** delivery `Delivered` → order tự `Completed`.

---

## 7. Payments (PayOS) — `api/payments`

| Method | Path | Quyền | Việc |
|---|---|---|---|
| POST | `/api/payments/{orderId}` | 🔑 customer | tạo link PayOS → `{ checkoutUrl, qrCode, orderCode, amount }` (order phải `Pending`, không phải COD; link Pending cũ bị vô hiệu) |
| GET | `/api/payments/{orderId}` | 🔑 customer | trạng thái thanh toán |
| POST | `/api/payments/{orderId}/cancel` | 🔑 customer | `{ "reason":"đổi ý" }` → hủy **mọi** link/transaction còn treo + order/deliveries → `Cancelled` + hoàn kho |
| POST | `/api/payments/payos/webhook` | 🟢 PayOS gọi | verify chữ ký → order `Paid` + **tạo delivery theo store** + **tự tạo shipment** mỗi delivery |

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

## 9. Chat & Trợ lý AI — `api/chat` (🔑)

Một mô hình **`chatboxes` 1-n `chat_messages`** dùng chung cho cả chat **người ↔ người** (customer ↔ garden owner / staff / manager — không giới hạn cặp role) lẫn **người ↔ AI**. Mỗi message có `senderRole`, `senderName` (prefix email, để AI phân biệt 2 người cùng role), `content` (nullable), `images` (chỉ lưu **link**), `isFromAi`.

### Người ↔ người
| Method | Path | Ghi chú |
|---|---|---|
| POST | `/api/chat/chatbox/with/{otherUserId}` | lấy/tạo chatbox Direct với user khác |
| GET | `/api/chat/chatboxes?page=&pageSize=` | danh sách hội thoại của tôi |
| GET | `/api/chat/chatbox/{chatboxId}/messages?page=&pageSize=` | tin nhắn (mới nhất trước) |
| POST | `/api/chat/chatbox/{chatboxId}/messages` | gửi tin: `{ "content":"...", "imageUrls":["..."] }` — cần **content HOẶC ảnh** |
| POST | `/api/chat/chatbox/{chatboxId}/images` | **multipart** field `file` → trả link ảnh (`Chat_images/{chatboxId}/`) để gắn vào tin |
| PATCH | `/api/chat/message/{messageId}/read` | đánh dấu 1 tin đã đọc |
| PATCH | `/api/chat/chatbox/{chatboxId}/read-all` | đánh dấu cả hội thoại đã đọc |

### Người ↔ AI
| Method | Path | Ghi chú |
|---|---|---|
| POST | `/api/chat/ai/messages` | chat với trợ lý AI (lưu cùng `chatboxes`/`chat_messages`, Type=Assistant) |

```jsonc
POST /api/chat/ai/messages
{
  "chatboxId": null,            // lượt đầu để null → server tạo hội thoại AI & trả lại chatboxId
  "message": "Cây này hợp mệnh Mộc không?",   // có thể null nếu chỉ gửi ảnh
  "model": null,                // null → AiChat:DefaultModel; phải nằm trong AiChat:AllowedModels
  "productId": "<id>",          // hỏi về 1 sản phẩm → AI nạp thông tin sản phẩm làm ngữ cảnh
  "imageUrls": ["https://..."]  // ảnh: lưu link; AI nhận bản base64 tải từ link
}
```
- **Nhớ N lượt gần nhất** đọc từ DB (cấu hình `AiChat:MaxHistoryTurns`, mặc định 5).
- **Đổi model mỗi lượt** qua `model`. Danh tính người dùng (tên từ JWT) được đưa vào ngữ cảnh để AI xưng hô đúng.
- Cấu hình ở section **`AiChat`** (BaseUrl/ChatPath/DefaultModel/AllowedModels/...) và storage ở section **`SupabaseStorage`** trong `appsettings`.

### Realtime — SignalR hub `/hubs/chat` (cần Bearer)
- Gọi: `JoinChatbox(chatboxId)`, `SendMessage(chatboxId, content)`, `MarkAsRead(messageId)`, `LeaveChatbox(chatboxId)`.
- Sự kiện nhận: `messageReceived`, `messageMarkedAsRead`, `userJoined`, `userLeft`, `error`.

---

## 10. 🛒 Luồng mua hàng end-to-end (test nhanh)

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

# 4. Đặt đơn (cả giỏ hoặc chọn lọc bằng items; paymentMethod: 0=PayOS, 1=COD)
POST /api/orders                              { shippingAddressId, note, items?, paymentMethod? }   → orderId

# 5. Thanh toán (đơn PayOS; COD bỏ qua bước này — delivery đã tạo sẵn từ lúc đặt)
POST /api/payments/{orderId}                  → checkoutUrl → khách trả tiền (trong 15 phút, quá hạn đơn → Expired)
   PayOS → POST /api/payments/payos/webhook   → order Paid + tạo delivery theo store + tự tạo shipment (delivery Confirmed)

# 6. Vendor xử lý giao hàng
GET   /api/orders/stores/{storeId}/deliveries
PATCH /api/orders/deliveries/{deliveryId}/status   { status: "Shipped" } → ... "Delivered"
   → khi mọi delivery Delivered: order tự Completed
```

---

## 11. Tài khoản & dữ liệu seed

Chạy `dotnet run --project src/FengDeskAI.WebAPI -- seed` (idempotent):
- **Geography**: tỉnh/quận/phường mẫu (Hà Nội, Đà Nẵng, HCM).
- **Vendor demo**: `vendor@fengdesk.local` / `Vendor@123` (role Manager) sở hữu store "Vườn Phong Thủy Demo".
- **Catalog**: 4 categories, 7 tags, 5 products (kèm items/ảnh/links).

---

## 12. Bảng trạng thái (enum)

| Enum | Giá trị |
|---|---|
| **OrderStatus** | `Pending` → `Paid` → `Processing` → `Completed` · `Cancelled` · `Expired` (PayOS quá 15' chưa trả) |
| **DeliveryStatus** | `Pending` → `Confirmed` → `Preparing` → `Shipped` → `Delivered` · `Cancelled` · `Returned` |
| **PaymentStatus** | `Pending` → `Paid` · `Cancelled` · `Failed` · `Expired` |
| **PaymentMethod** | `PayOS` (0) · `COD` (1) |

> Status order/delivery là đề xuất (State Diagram chưa chốt) — điều chỉnh khi cần.

---

*Cập nhật: 2026-06-10. Pending chưa làm: Customer care (reviews/support/after-sales), Notification, Recommendation (AI).*
