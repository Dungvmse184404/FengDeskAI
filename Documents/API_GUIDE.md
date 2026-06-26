# FengDeskAI — Hướng dẫn sử dụng API

> Tổng hợp các endpoint hiện có + luồng mua hàng end-to-end (cart → order → payment → shipping).
> Base URL dev: `https://localhost:7016` (xem `Properties/launchSettings.json`). Swagger UI: `/swagger`.

## Quy ước chung

- **Auth**: gửi header `Authorization: Bearer <accessToken>` (lấy từ login/finalize). Swagger có nút Authorize.
- **Response bọc** trong `ServiceResult`: `{ isSuccess, statusCode, message, errors, data }`.
- **Phân trang**: query `?page=1&pageSize=20`. Kết quả `PagedResult`: `{ items, page, pageSize, totalCount, totalPages }`.
- **Phân quyền**: 🟢 public · 🔑 cần đăng nhập · 👮 Manager/Admin · 🛡️ Admin · 🌱 GardenOwner (người bán) · 🏪 owner/staff của store (kiểm tra ở service).
- **Role `GardenOwner`** (`UserRole=16`): người bán sở hữu store, được cấp tự động khi tạo store đầu tiên (self-service). "Nhân viên store" KHÔNG có role riêng — chỉ là `garden_staff_assignments`; phân biệt với `Staff` = nhân viên sàn. Chi tiết: `Documents/FIX_GARDEN_OWNER_FLOW.md`.

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

> ⚠️ **Sắp thay đổi (per `FIX_GARDEN_OWNER_FLOW.md`):** `POST /api/stores` chuyển sang self-service 🌱 — người tạo tự thành owner (bỏ `ownerUserId` trong body) và được cấp role `GardenOwner`. Sở hữu chuyển sang nhiều-nhiều qua bảng `garden_store_owners`; sẽ có thêm `POST/DELETE /api/stores/{id}/owners`.

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

### z
| Method | Path                                                                | Quyền | Ghi chú                                                                                                     |
| ------ | ------------------------------------------------------------------- | ----- | ----------------------------------------------------------------------------------------------------------- |
| GET    | `/api/products?storeId=&categoryId=&tagId=&search=&page=&pageSize=` | 🟢    | list card (`minPrice`, ảnh chính, **`items[]` kèm giá+tồn kho mỗi SKU**) + paging                           |
| GET    | `/api/products/{id}`                                                | 🟢    | detail: items (SKU), images, categories, tags                                                               |
| POST   | `/api/products`                                                     | 🏪    | tạo product kèm items/images/links (xem body dưới)                                                          |
| PUT    | `/api/products/{id}`                                                | 🏪    | `{ "name":"...", "description":"...", "isActive":true }`                                                    |
| DELETE | `/api/products/{id}`                                                | 🏪    | soft-delete                                                                                                 |
| POST   | `/api/products/{id}/items`                                          | 🏪    | `{ "name":"Size L", "price":250000, "stock":30, "sku":"KT-L" }`                                             |
| PUT    | `/api/products/{id}/items/{itemId}`                                 | 🏪    | như trên                                                                                                    |
| DELETE | `/api/products/{id}/items/{itemId}`                                 | 🏪    |                                                                                                             |
| POST   | `/api/products/{id}/images`                                         | 🏪    | **multipart/form-data**: field `file` (+ `sortOrder`). Lưu Supabase Storage `Product_images/{id}/`, trả URL |
| DELETE | `/api/products/{id}/images/{imageId}`                               | 🏪    | xoá bản ghi DB **và** file trên storage                                                                     |
| PUT    | `/api/products/{id}/categories`                                     | 🏪    | `{ "categoryIds":["...","..."] }` (thay toàn bộ)                                                            |
| PUT    | `/api/products/{id}/tags`                                           | 🏪    | `{ "tagIds":["...","..."] }` (thay toàn bộ)                                                                 |
| PUT    | `/api/products/{id}/feng-shui`                                      | 🏪    | thuộc tính phong thủy (làm ứng viên gợi ý) — xem body dưới                                                  |

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

#### Ảnh sản phẩm — chi tiết dùng API image

Nguồn: `ProductsController.UploadImage/DeleteImage` → `ProductService` → `IFileStorage` (Supabase Storage).

**Có 2 cách gắn ảnh vào sản phẩm:**

1. **Upload tệp** — `POST /api/products/{id}/images` (🏪 owner/staff store sở hữu sản phẩm)
   - Content-Type: **`multipart/form-data`**; field tệp tên **`file`**; field phụ **`sortOrder`** (int, mặc định `0`).
   - Định dạng cho phép (theo `ImageUpload.AllowedContentTypes` — chỉ loại AI đọc được): **JPEG, PNG, BMP, GIF**.
   - Lưu tại `Product_images/{productId}/{guid}{ext}` trên Supabase Storage; URL công khai được gắn vào DB.
   - Trả **`201 Created`** + `ProductImageResponse`:

   ```jsonc
   { "id": "<guid>", "url": "https://<supabase>/.../Product_images/<productId>/<guid>.jpg", "sortOrder": 0 }
   ```

   ```bash
   curl -X POST 'https://<host>/api/products/<id>/images' \
     -H 'Authorization: Bearer <token>' \
     -F 'file=@cay-kim-tien.jpg' -F 'sortOrder=0'
   ```

2. **Dùng URL có sẵn** — chỉ qua **body tạo product** (`images: [{ "url": "...", "sortOrder": 0 }]`).
   > Service có `AddImageAsync` (gắn ảnh bằng URL lẻ) nhưng **chưa được expose** qua controller — hiện chỉ có upload tệp + thêm-qua-body-create.

**Xoá ảnh** — `DELETE /api/products/{id}/images/{imageId}` (🏪): xoá bản ghi DB **rồi** xoá file trên storage (best-effort, không chặn nghiệp vụ nếu xoá file lỗi).

**Lỗi thường gặp:**

| HTTP | Khi nào |
|---|---|
| `400` `ImageFileRequired` | không gửi `file` hoặc file rỗng |
| `422` `ImageTypeInvalid` | content-type ngoài JPEG/PNG/BMP/GIF |
| `403` | không phải owner/staff của store sở hữu sản phẩm (guard ở service) |
| `404` `ImageNotFound` | xoá ảnh không tồn tại |

**Liên quan:**
- `GET /api/products/{id}` trả `images[]` (mỗi phần tử `{ id, url, sortOrder }`); list card lấy `primaryImageUrl` = ảnh có `sortOrder` nhỏ nhất.
- Model 3D (`POST /api/products/{id}/model-3d`) sinh từ **một ảnh sản phẩm** — bỏ trống `sourceImageId` thì dùng ảnh primary.
- Upload ảnh chat (`POST /api/chat/chatbox/{id}/images`) dùng **chung** quy ước `ImageUpload` (cùng danh sách định dạng), chỉ khác thư mục `Chat_images/{chatboxId}/`.

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

### Bảng tra cứu Style / Vibe / Element (code)
`style`/`vibe` lưu ở bảng tra cứu (admin thêm/sửa không cần deploy); `element` là ngũ hành cố định.
Endpoint: `GET /api/styles`, `GET /api/vibes`, `GET /api/elements` (🟢 public) — đổ dropdown chọn **code**;
`POST`/`PUT` (👮 Manager+) để thêm/sửa style·vibe (element chỉ `GET`+`PUT` sửa tên). "Xoá" = `PUT isActive=false`.

| Nhóm | Code hợp lệ (seed sẵn) |
|---|---|
| **Style** (`styles.code`) | `Modern`, `Classic`, `Minimal`, `Industrial`, `Scandinavian`, `Bohemian`, `Other` |
| **Vibe** (`vibes.code`) | `Focus`, `Relax`, `Creative`, `Calm`, `Energize` |
| **Element** (ngũ hành, enum + `elements.code`) | `Kim`, `Moc`, `Thuy`, `Hoa`, `Tho` |

> Gửi code sai (vd `"Minima"`) → **400** (đã validate), không còn lỗi 500. `code` bất biến; chỉ đổi `name`/`isActive`/`sortOrder`.
> Ảnh (product/chat) chỉ nhận **JPG, PNG, BMP, GIF** (giới hạn theo AI đọc được).

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
| GET | `/api/orders/all?page=&pageSize=` | 🛡️ Admin | tất cả đơn của mọi customer (paged, kèm `customerId`) |
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
| POST | `/api/chat/chatbox/with/{otherUserId}` | lấy/tạo chatbox Direct (1-1) với user khác |
| POST | `/api/chat/groups` | tạo phòng nhóm (mình là Owner): `{ "title":"...", "memberUserIds":["...","..."] }` |
| POST | `/api/chat/chatbox/{chatboxId}/participants` | thêm thành viên (chỉ Owner): `{ "userId":"..." }` |
| DELETE | `/api/chat/chatbox/{chatboxId}/participants/{userId}` | xoá thành viên (chỉ Owner) |
| GET | `/api/chat/chatboxes?page=&pageSize=` | danh sách hội thoại của tôi (kèm `lastMessage`) |
| GET | `/api/chat/chatbox/{chatboxId}/messages?page=&pageSize=` | tin nhắn (mới nhất trước) |
| POST | `/api/chat/chatbox/{chatboxId}/messages` | gửi tin: `{ "content":"...", "imageUrls":["..."] }` — cần **content HOẶC ảnh** |
| POST | `/api/chat/chatbox/{chatboxId}/images` | **multipart** field `file` → trả link ảnh (`Chat_images/{chatboxId}/`) để gắn vào tin |
| PATCH | `/api/chat/chatbox/{chatboxId}/read-all` | đánh dấu cả hội thoại đã đọc (cập nhật `LastReadAt`) |

> **Gọi AI trong phòng nhiều người bằng `@AI`**: bất kỳ thành viên nào gửi tin có chứa `@AI` (vd `@AI bạn thấy sản phẩm này thế nào <link>`) → worker nền cho AI trả lời, **scope tool/ngữ cảnh theo người gọi** (AI có thể gọi `get_my_profile`, `list_my_workspaces`, `get_product`... của chính người đó để tư vấn). Không cần bật cờ riêng — endpoint `ai-enabled` cũ đã bị **gỡ bỏ** (trigger nay hoàn toàn dựa vào `@AI`).
> **Ngữ cảnh gửi cho AI**: (1) toàn bộ lịch sử gần đây của phòng hiện tại (`AiChat:RoomContextMessages`, mặc định 30); (2) **chỉ các tin của chính người gọi** ở những phòng **public** khác. Nội dung **phòng private (user↔AI) không bao giờ** lọt sang phòng public.

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
- **Kết nối** → tự `AddToGroup("user-{userId}")`; muốn nhận tin trong 1 phòng phải `JoinChatbox(chatboxId)` trước (vào group `chat-{chatboxId}`).
- Gọi (client → server): `JoinChatbox(chatboxId)`, `SendMessage(chatboxId, content)`, `MarkChatboxRead(chatboxId)`, `LeaveChatbox(chatboxId)`.
- Sự kiện nhận (server → client): `messageReceived`, `chatboxRead` (`{ chatboxId, userId }`), `userJoined` (`{ userId }`), `userLeft` (`{ userId }`), `error` (string).
- `SendMessage` qua hub dùng chung `ChatService.SendMessageAsync` với REST → broadcast `messageReceived` giống nhau (qua `IChatRealtimeNotifier`); gửi tin kèm ảnh phải đi qua REST (`POST .../messages`).

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

*Cập nhật: 2026-06-18. Pending chưa làm: Customer care (reviews/support/after-sales), Notification, Recommendation (AI).*
