# 02 — Products

[← Mục lục](./README.md)

Controller: `ProductsController` · Route gốc: `/api/products` · Mặc định `[Authorize]`; **đọc (list/detail/model-3d) là Public**.

Ghi (product, SKU, ảnh, danh mục, phong thủy) yêu cầu **owner/staff của store sở hữu sản phẩm** (hoặc Admin) — kiểm ở service layer.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/products` | Public | Tìm/lọc sản phẩm (paged) |
| GET | `/api/products/{id}` | Public | Chi tiết sản phẩm |
| POST | `/api/products` | Owner/Admin | Tạo sản phẩm |
| PUT | `/api/products/{id}` | Owner/Admin | Cập nhật sản phẩm |
| DELETE | `/api/products/{id}` | Owner/Admin | Xóa (mềm) sản phẩm |
| POST | `/api/products/{id}/items` | Owner/Admin | Thêm SKU |
| PUT | `/api/products/{id}/items/{itemId}` | Owner/Admin | Sửa SKU |
| DELETE | `/api/products/{id}/items/{itemId}` | Owner/Admin | Xóa SKU |
| POST | `/api/products/{id}/images` | Owner/Admin | Upload ảnh (multipart) |
| POST | `/api/products/{id}/images/link` | Owner/Admin | Gắn ảnh bằng URL |
| DELETE | `/api/products/{id}/images/{imageId}` | Owner/Admin | Xóa ảnh |
| GET | `/api/products/{id}/model-3d` | Public | Trạng thái/kết quả model 3D hiện tại |
| POST | `/api/products/{id}/model-3d/requests` | Owner/Admin | Tạo yêu cầu sinh/tạo lại model 3D (`202`) |
| GET | `/api/products/{id}/model-3d/requests` | Owner/Admin | Lịch sử yêu cầu |
| PATCH | `/api/products/{id}/model-3d/toggle` | Owner/Admin | Bật/tắt hiển thị model 3D |
| DELETE | `/api/products/{id}/model-3d` | Owner/Admin | Xóa model 3D |
| PUT | `/api/products/{id}/categories` | Owner/Admin | Gán danh mục |
| PUT | `/api/products/{id}/feng-shui` | Owner/Admin | Khai báo thuộc tính phong thủy |
| PUT | `/api/products/{id}/aspirations` | **Manager+** | Duyệt thẻ mục tiêu phong thủy |

---

## GET `/api/products`

Tìm/lọc sản phẩm. Query (`ProductQueryParams` kế thừa `PageRequest`):

| Param | Kiểu | Ghi chú |
|-------|------|---------|
| `page`, `pageSize` | int | Phân trang |
| `storeId` | guid? | Lọc theo store |
| `categoryId` | guid? | Lọc theo danh mục |
| `search` | string? | Từ khóa tên |

**Response `data`** = `PagedResult<ProductListItemResponse>`:
```json
{
  "items": [{
    "id": "guid", "gardenStoreId": "guid", "name": "...",
    "isActive": true, "minPrice": 120000, "primaryImageUrl": "https://...",
    "items": [{ "id": "guid", "name": "M", "price": 120000, "stock": 8, "sku": "SKU-1",
                "weightGram": 500, "lengthCm": 10, "widthCm": 10, "heightCm": 10 }]
  }],
  "page": 1, "pageSize": 20, "totalCount": 1, "totalPages": 1
}
```

---

## GET `/api/products/{id}`

Chi tiết sản phẩm. **Response `data`** = `ProductDetailResponse`:
```json
{
  "id": "guid", "gardenStoreId": "guid", "storeName": "...", "name": "...",
  "description": "...", "isActive": true,
  "items": [ /* ProductItemResponse */ ],
  "images": [{ "id": "guid", "url": "...", "sortOrder": 0 }],
  "categories": [{ "id": "guid", "name": "..." }],
  "primaryElement": "Hoa", "secondaryElements": ["Tho"],
  "placement": "Desk", "vibes": ["Focus"], "styles": ["Minimal"],
  "model3D": { /* ProductModel3DResponse hoặc null */ },
  "createdAt": "...", "updatedAt": "..."
}
```

---

## POST `/api/products`

Tạo sản phẩm (kèm SKU, ảnh, danh mục, phong thủy tùy chọn).

**Request body** (`CreateProductRequest`)
```json
{
  "gardenStoreId": "guid",
  "name": "Cây kim tiền để bàn",
  "description": "...",
  "items": [{ "name": "Chậu nhỏ", "price": 120000, "stock": 10, "sku": "KT-S",
              "sizeClass": "Small",
              "weightGram": 500, "lengthCm": 10, "widthCm": 10, "heightCm": 10 }],
  "images": [{ "url": "https://...", "sortOrder": 0 }],
  "categoryIds": ["guid"],
  "elementInputs": [{ "inputKind": "Material", "inputCode": "Wood" }],
  "primaryElement": "Moc",
  "secondaryElements": ["Tho"],
  "placement": "Desk",
  "vibes": ["Focus"],
  "styles": ["Minimal"]
}
```
| Field phong thủy | Ghi chú |
|---|---|
| `elementInputs` | **Đường ưu tiên** — tín hiệu vật lý (vật liệu / màu / hình khối), engine tự tính vector ngũ hành |
| `primaryElement` / `secondaryElements` | Đường advanced, **chỉ dùng khi không có `elementInputs`**. Bỏ trống cả hai → sản phẩm chưa có phong thủy (set sau qua `/feng-shui`) |
| `placement` | Bỏ trống → `Desk`. Xem [bảng bên dưới](#productplacement--quyết-định-luồng-gợi-ý) |
| `items[].sizeClass` | `Small/Medium/Large` **của từng SKU** — kích thước biến thiên theo biến thể nên nằm ở đây, không ở `Product` |

> SKU mặc định `weightGram=500`, kích thước `10cm`.

---

## PUT `/api/products/{id}`

**Request body** (`UpdateProductRequest`)
```json
{ "name": "...", "description": "...", "isActive": true }
```

## DELETE `/api/products/{id}`
Xóa mềm sản phẩm.

---

## SKU (Product items)

**POST `/api/products/{id}/items`** — body `CreateProductItemRequest`:
```json
{ "name": "Chậu lớn", "price": 200000, "stock": 5, "sku": "KT-L",
  "weightGram": 800, "lengthCm": 15, "widthCm": 15, "heightCm": 20 }
```
**PUT `/api/products/{id}/items/{itemId}`** — body `UpdateProductItemRequest` (cùng cấu trúc).
**DELETE `/api/products/{id}/items/{itemId}`** — xóa SKU.

---

## Ảnh sản phẩm

**POST `/api/products/{id}/images`** — `multipart/form-data`: field `file` (ảnh) + `sortOrder` (int, mặc định 0). Trả `ProductImageResponse`. Thiếu file → `400`.

**POST `/api/products/{id}/images/link`** — gắn ảnh bằng URL có sẵn:
```json
{ "url": "https://...", "sortOrder": 1 }
```
**DELETE `/api/products/{id}/images/{imageId}`** — xóa ảnh.

---

## Model 3D (sinh từ ảnh qua Meshy AI — multi-image, 1–4 ảnh)

> Thiết kế đầy đủ: `docs/adr/refactor-model3d-request-flow.md`. **Request đầu tiên** (Initial, sản
> phẩm chưa có model) chạy **tự động** (worker nền gọi Meshy, tự retry nếu Meshy hết credit).
> **Request tiếp theo** (Regenerate, sản phẩm đã có model) vào hàng chờ, chỉ **staff sàn** xử lý
> thủ công — xem [26-model3d-requests.md](./26-model3d-requests.md).

**GET `/api/products/{id}/model-3d`** (Public) — trạng thái/kết quả model hiện tại. `data` = `ProductModel3DResponse`:
```json
{ "id": "guid", "productId": "guid", "status": "Succeeded", "progress": 100,
  "sourceImageUrl": "...", "modelUrl": "https://....glb",
  "thumbnailUrl": "...", "errorMessage": null, "isEnabled": true, "updatedAt": "..." }
```
Status: `Pending | Processing | Succeeded | Failed`. `isEnabled` = toggle hiển thị của owner/garden
staff — `false` thì FE ẩn hẳn phần 3D (dữ liệu model vẫn còn, chỉ ẩn hiển thị).

**POST `/api/products/{id}/model-3d/requests`** — tạo yêu cầu. `multipart/form-data`:

| Field | Kiểu | Ghi chú |
|---|---|---|
| `sourceImageIds` | `guid[]` | Ảnh sản phẩm có sẵn được tick (0 hoặc nhiều) |
| `newImages` | `file[]` | Ảnh mới upload — tự tạo `ProductImage` bình thường (vào gallery sản phẩm) |

Tổng `sourceImageIds` + `newImages` phải 1–4 ảnh (giới hạn Meshy). **Chỉ áp dụng khi tạo request
Initial** (sản phẩm chưa có model) — bỏ trống cả 2 field → dùng ảnh primary. Nếu sản phẩm **đã có**
model, đây là request Regenerate: không cần ảnh ở bước này (staff sàn tự chọn ảnh khi xử lý) — 2
field trên bị bỏ qua nếu có gửi.

Trả `202` + `data` = `Model3DRequestResponse`:
```json
{ "id": "guid", "productId": "guid", "requestType": "Initial", "status": "Queued",
  "createdAt": "...", "updatedAt": "..." }
```
`requestType`: `Initial | Regenerate`. `status` đã che giấu lỗi hết credit Meshy — owner/garden
staff chỉ thấy `Queued | Processing | AwaitingStaff | InProgress | Succeeded | Failed | Rejected`,
không bao giờ thấy lý do lỗi nội bộ thật. Trả `409 Conflict` nếu sản phẩm đang có request khác
chưa xử lý xong (chỉ 1 request "mở" tại 1 thời điểm).

**GET `/api/products/{id}/model-3d/requests`** — lịch sử request, `data` = `Model3DRequestResponse[]` (mới nhất trước).

**PATCH `/api/products/{id}/model-3d/toggle`** — bật/tắt hiển thị:
```json
{ "isEnabled": false }
```

**DELETE `/api/products/{id}/model-3d`** — xóa model hiện tại (kèm xóa file GLB trên storage).

---

## `Aspiration` — mục tiêu phong thủy (vendor đề xuất, **admin duyệt**)

`Wealth` · `Career` · `Health` · `Relationship` · `Study` — ứng với 4 cung tốt Bát Trạch.

| Bước | Endpoint | Quyền | Tác dụng |
|---|---|---|---|
| Vendor **đề xuất** | `PUT /api/products/{id}/feng-shui` → `aspirations` | Owner/Admin | Ghi thẻ ở trạng thái **chưa duyệt** — engine BỎ QUA |
| Admin **duyệt** | `PUT /api/products/{id}/aspirations` | **Manager trở lên** | Bật `is_approved`; thẻ không có trong danh sách bị **hạ về chưa duyệt** |

```json
// PUT /api/products/{id}/aspirations
{ "approved": ["Wealth", "Career"] }
```

Response `ProductFengShuiResponse` tách rõ hai nhóm:
```json
{ "productId": "guid", "placement": "Desk",
  "approvedAspirations": ["Wealth"], "pendingAspirations": ["Health"] }
```

- `GET /api/products/{id}` chỉ trả **`aspirations`** = nhóm **đã duyệt**; thẻ chờ duyệt không lộ ra công khai.
- `GET /api/products?aspiration=Wealth` lọc theo thẻ **đã duyệt**.
- Vendor sửa lại danh sách đề xuất **không hạ được** thẻ đã duyệt của chính mình.

> Vì sao phải duyệt: thẻ "Tài lộc" là **lời hứa nghiệp vụ**. Để vendor tự bật thì ai cũng gắn hết 5 thẻ để lọt mọi bộ lọc.

---

## `ProductPlacement` — quyết định luồng gợi ý

Không phải nhãn hiển thị: **enum này quyết định engine chấm điểm sản phẩm thế nào**.

| Giá trị | Nghĩa | Vào luồng gợi ý nào | Hướng đặt |
|---|---|---|---|
| **`Desk`** *(mặc định)* | Đồ đặt trong không gian — tượng, đá, đồ trang trí bàn, **và vật trấn yểm gắn kiến trúc** | `recommend_products` | có `placementHint` theo la bàn |
| **`Living`** | Cây / vật sống | `recommend_products` | **không** — đặt theo ánh sáng |
| **`Carry`** | Mang theo người — vòng tay, mặt dây, charm ví, treo xe | [`/recommendations/personal`](./18-recommendations.md) — chấm theo **bản mệnh**, không theo phòng | **không** |
| **`Consumable`** | Hàng tiêu hao — nhang, nến, muối | ❌ **không vào luồng nào** | — |

> `Consumable` vẫn **tìm và mua bình thường** qua `GET /api/products`, chỉ không được engine gợi ý.

⚠️ **`Category` và `Placement` là hai trục khác nhau, không phản chiếu nhau.** `Category` trả lời *"vật này LÀ gì"* (taxonomy để khách duyệt); `Placement` trả lời *"dùng ở đâu → engine chấm thế nào"*. **Không** tạo category kiểu "Vật phẩm mang theo người" — vòng tay vẫn thuộc category "Trang sức phong thủy" và có `placement = Carry`.

Chi tiết: [product-placement-personal-recommendation.md](../adr/product-placement-personal-recommendation.md)

---

## Liên kết khác

**PUT `/api/products/{id}/categories`** — gán danh mục:
```json
{ "categoryIds": ["guid", "guid"] }
```
**PUT `/api/products/{id}/feng-shui`** — khai báo phong thủy (biến sản phẩm thành ứng viên gợi ý), body `SetProductFengShuiRequest`:
```json
{ "primaryElement": "Hoa", "secondaryElements": ["Tho"],
  "placement": "Desk", "vibes": ["Focus"], "styles": ["Minimal"],
  "aspirations": ["Wealth"] }
```

---

[← Authentication](./01-authentication.md) · [Tiếp: Categories →](./03-categories.md)
