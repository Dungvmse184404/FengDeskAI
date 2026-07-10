# Hotfix: thêm API gắn ảnh bằng URL vào sản phẩm đã tồn tại

> Mục tiêu: cho phép gắn ảnh vào một sản phẩm **đã tạo** bằng **URL có sẵn** (không cần upload tệp).

## Vấn đề

Hiện chỉ có 2 endpoint ảnh trên `ProductsController`:

- `POST /api/products/{id}/images` — **upload tệp** (`multipart/form-data`, field `file`).
- `DELETE /api/products/{id}/images/{imageId}` — xoá ảnh.

Gắn ảnh bằng **URL** chỉ làm được **lúc tạo product** (trong body `CreateProductRequest.Images`). Sau khi product đã tạo, **không có** endpoint nào nhận URL.

Service đã có sẵn `IProductService.AddImageAsync(productId, userId, isAdmin, CreateProductImageRequest)` (logic + guard quyền hoàn chỉnh) nhưng **chưa được map ra controller** → chỉ thiếu phần expose.

## Phạm vi

- Chỉ thêm **1 endpoint** ở `WebAPI` (controller mỏng, gọi service có sẵn).
- **Không** đổi DB, **không** đổi service/repository, **không** migration.

---

## Thay đổi CODE

### File: `src/FengDeskAI.WebAPI/Controllers/ProductsController.cs`

Trong vùng `// ----- Product images -----`, thêm endpoint dưới đây (đặt ngay sau action `UploadImage`):

```csharp
/// <summary>Gắn ảnh sản phẩm bằng URL có sẵn (không upload tệp). Lưu link vào sản phẩm.</summary>
[HttpPost("{id:guid}/images/link")]
public async Task<IActionResult> AddImageByUrl(Guid id, [FromBody] CreateProductImageRequest request, CancellationToken ct)
    => ToActionResult(await _service.AddImageAsync(id, CurrentUserId, IsAdmin, request, ct));
```

- `CreateProductImageRequest` đã tồn tại (`Application/Features/Catalog/DTOs/ProductDtos.cs`):

  ```csharp
  public class CreateProductImageRequest
  {
      public string Url { get; set; } = null!;
      public int SortOrder { get; set; }
  }
  ```

- `using FengDeskAI.Application.Features.Catalog.DTOs;` đã có sẵn ở đầu file — không cần thêm import.
- Quyền: kế thừa `[Authorize]` của controller; `AddImageAsync` tự guard owner/staff của store sở hữu sản phẩm (giống `UploadImage`).

> Không cần sửa `IProductService`/`ProductService` — `AddImageAsync` đã có sẵn và đang được dùng nội bộ.

---

## API mới

`POST /api/products/{id}/images/link` — 🏪 owner/staff của store sở hữu sản phẩm

`{id}` = **product id** (Guid, lấy từ response tạo product hoặc `GET /api/products`).

Body:

```json
{ "url": "https://res.cloudinary.com/.../cay-kim-tien.jpg", "sortOrder": 0 }
```

Response `201 Created`:

```json
{ "id": "<image-guid>", "url": "https://.../cay-kim-tien.jpg", "sortOrder": 0 }
```

Lỗi:

| HTTP                     | Khi nào                                          |
| ------------------------ | ------------------------------------------------ |
| `400` `ImageUrlRequired` | `url` rỗng / thiếu                               |
| `403`                    | không phải owner/staff của store sở hữu sản phẩm |
| `404`                    | product không tồn tại                            |

### So sánh với endpoint upload tệp

|                        | Upload tệp                           | Gắn URL (mới)                         |
| ---------------------- | ------------------------------------ | ------------------------------------- |
| Endpoint               | `POST /api/products/{id}/images`     | `POST /api/products/{id}/images/link` |
| Content-Type           | `multipart/form-data` (field `file`) | `application/json`                    |
| Lưu storage            | có (Supabase `Product_images/{id}/`) | không — chỉ lưu link                  |
| Validate định dạng ảnh | có (JPEG/PNG/BMP/GIF)                | không (chỉ kiểm `url` rỗng)           |

---

## Test nhanh

```bash
# 1) Tạo product → lấy id
PID=$(curl -s -X POST 'https://<host>/api/products' \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{ "gardenStoreId":"<store-id>", "name":"Cây Kim Tiền", "items":[{"name":"M","price":250000,"stock":10}] }' \
  | jq -r '.data.id')

# 2) Gắn ảnh bằng URL
curl -X POST "https://<host>/api/products/$PID/images/link" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{ "url":"https://example.com/kim-tien.jpg", "sortOrder":0 }'

# 3) Kiểm tra
curl "https://<host>/api/products/$PID"   # images[] có ảnh vừa gắn
```

---

## Nối lên Frontend (`FengDeskAI_FE`)

> FE ở repo riêng. Dưới đây là hợp đồng API + đoạn gọi mẫu để dán vào FE.

**Service gọi API** (axios/TS):

```ts
// services/product.ts
export async function addProductImageByUrl(productId: string, url: string, sortOrder = 0) {
  const { data } = await api.post(`/products/${productId}/images/link`, { url, sortOrder });
  return data.data; // { id, url, sortOrder }
}

// (đã có sẵn) upload tệp
export async function uploadProductImage(productId: string, file: File, sortOrder = 0) {
  const form = new FormData();
  form.append('file', file);
  form.append('sortOrder', String(sortOrder));
  const { data } = await api.post(`/products/${productId}/images`, form);
  return data.data;
}
```

**Luồng trong form tạo sản phẩm** (đúng thứ tự "tạo product trước → gắn ảnh"):

```ts
const product = await createProduct(payload);          // B1: lấy product.id
for (const [i, img] of images.entries()) {             // B2: gắn ảnh
  if (img.file) await uploadProductImage(product.id, img.file, i);      // ảnh tải lên
  else if (img.url) await addProductImageByUrl(product.id, img.url, i); // ảnh dán link
}
router.push(`/seller/products/${product.id}`);
```

- Header `Authorization: Bearer <token>` (dùng interceptor sẵn có của FE).
- Lỗi cần handle ở FE: `400` (url rỗng), `403` (không phải owner/staff store), `404` (product không tồn tại).

## Checklist

- [ ] Thêm action `AddImageByUrl` vào `ProductsController` (đoạn code trên).
- [ ] FE: thêm `addProductImageByUrl` + nối vào form tạo/sửa sản phẩm (đoạn trên).
- [ ] Build: `dotnet build FengDeskAI.slnx`.
- [ ] Test 3 bước ở trên (201 + ảnh xuất hiện trong `GET /api/products/{id}`).
- [ ] Cập nhật `Documents/API_GUIDE.md` (phần Product images) — thêm dòng endpoint mới.
