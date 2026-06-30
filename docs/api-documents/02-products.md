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
| GET | `/api/products/{id}/model-3d` | Public | Trạng thái/kết quả model 3D |
| POST | `/api/products/{id}/model-3d` | Owner/Admin | Sinh model 3D (xử lý nền, `202`) |
| DELETE | `/api/products/{id}/model-3d` | Owner/Admin | Xóa model 3D |
| PUT | `/api/products/{id}/categories` | Owner/Admin | Gán danh mục |
| PUT | `/api/products/{id}/feng-shui` | Owner/Admin | Khai báo thuộc tính phong thủy |

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
  "sizeClass": "Medium", "vibes": ["Focus"], "styles": ["Minimal"],
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
              "weightGram": 500, "lengthCm": 10, "widthCm": 10, "heightCm": 10 }],
  "images": [{ "url": "https://...", "sortOrder": 0 }],
  "categoryIds": ["guid"],
  "primaryElement": "Moc",
  "secondaryElements": ["Tho"],
  "sizeClass": "Small",
  "vibes": ["Focus"],
  "styles": ["Minimal"]
}
```
> Bỏ trống `primaryElement` → sản phẩm chưa có phong thủy (set sau qua `/feng-shui`). SKU mặc định `weightGram=500`, kích thước `10cm`.

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

## Model 3D (sinh từ ảnh qua Meshy AI)

**GET `/api/products/{id}/model-3d`** (Public) — trạng thái/kết quả. `data` = `ProductModel3DResponse`:
```json
{ "id": "guid", "productId": "guid", "status": "Succeeded", "progress": 100,
  "sourceImageUrl": "...", "modelUrl": "https://....glb",
  "thumbnailUrl": "...", "errorMessage": null, "updatedAt": "..." }
```
Status: `Pending | Processing | Succeeded | Failed`.

**POST `/api/products/{id}/model-3d`** — yêu cầu sinh model (xử lý nền, trả `202` + trạng thái `Processing`). Body (`GenerateModel3DRequest`, có thể bỏ trống → dùng ảnh primary):
```json
{ "sourceImageId": "guid" }
```
**DELETE `/api/products/{id}/model-3d`** — xóa model.

---

## Liên kết khác

**PUT `/api/products/{id}/categories`** — gán danh mục:
```json
{ "categoryIds": ["guid", "guid"] }
```
**PUT `/api/products/{id}/feng-shui`** — khai báo phong thủy (biến sản phẩm thành ứng viên gợi ý), body `SetProductFengShuiRequest`:
```json
{ "primaryElement": "Hoa", "secondaryElements": ["Tho"],
  "sizeClass": "Medium", "vibes": ["Focus"], "styles": ["Minimal"] }
```

---

[← Authentication](./01-authentication.md) · [Tiếp: Categories →](./03-categories.md)
