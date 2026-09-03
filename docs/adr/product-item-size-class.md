# ARD — `SizeClass` chuyển từ `Product` xuống `ProductItem`

> **Status:** Implemented (2026-08-14) · Migration `20260814195923_MoveSizeClassToProductItem`.
> **Tài liệu này viết bổ sung (2026-08-26)** — XML comment của `ProductItem.SizeClass` đã trỏ tới file này từ trước nhưng file chưa tồn tại.
> **Liên quan:** [`product-placement-personal-recommendation.md`](./product-placement-personal-recommendation.md) (cùng đợt, thêm `Placement` vào chỗ `SizeClass` vừa rời đi).

---

## 1. Vấn đề

`SizeClass` (`Small` / `Medium` / `Large`) trước đây nằm trên **`Product`** — mặt hàng cha.

Nhưng giá và tồn kho nằm ở **`ProductItem`** (biến thể/SKU), và **kích thước biến thiên theo đúng biến thể đó**: cùng một "Cây kim tiền" có SKU *"Chậu nhỏ"* và *"Chậu đại"*. Đặt `SizeClass` ở cha buộc vendor phải chọn **một** giá trị cho cả dòng sản phẩm — hoặc khai sai, hoặc phải tách thành hai `Product` riêng chỉ vì khác kích thước (kéo theo trùng lặp ảnh, mô tả, thuộc tính phong thủy).

`ProductItem` vốn đã mang `WeightGram` / `LengthCm` / `WidthCm` / `HeightCm` cho vận chuyển — tức **kích thước vật lý đã thuộc về SKU**. `SizeClass` nằm ở cha là bất nhất với chính schema đang có.

## 2. Quyết định

Chuyển cột `size_class` từ bảng `products` sang `product_items`, giữ nguyên kiểu `varchar(10)` **nullable**.

```csharp
// Domain/Entities/Catalog/ProductItem.cs
/// <summary>Small/Medium/Large của CHÍNH biến thể này. Null = chưa khai báo.</summary>
public SizeClass? SizeClass { get; set; }
```

**Nullable, không có default:** khác `Placement` (luôn có mặc định an toàn `Desk`), "chưa khai kích thước" ở đây mang nghĩa riêng — **không đủ dữ liệu để chấm**, không được lẫn với "vendor đã khai là Medium".

## 3. Ảnh hưởng tới engine chấm điểm — **không có**

`SizeClass` **chưa từng tham gia chấm điểm** ở bất kỳ phiên bản engine nào: nó không xuất hiện trong `ProductFacts`, `ScoringContext`, `ScoreOne`, hay câu query candidate. XML comment cũ trên `Product.SizeClass` ghi *"so với `WorkspaceProfile.DeskArea` để loại vật quá khổ"* là **ý định thiết kế chưa bao giờ được code**.

Vì vậy migration này **không đổi thứ hạng gợi ý**. Nó chỉ sửa chỗ đặt dữ liệu cho đúng.

## 4. Ảnh hưởng API (breaking)

| Nơi | Trước | Sau |
|---|---|---|
| `CreateProductRequest` | `sizeClass` ở gốc body | `items[].sizeClass` |
| `UpdateProductItemRequest` | — | `sizeClass` |
| `ProductDetailResponse` | `sizeClass` ở gốc | `items[].sizeClass` |
| `ProductItemResponse` | — | `sizeClass` |
| `SetProductFengShuiRequest` | `sizeClass` | **bỏ** (thay bằng `placement`) |
| `IProductRepository.SetFengShuiAsync` | `..., SizeClass size, ...` | `..., ProductPlacement placement, ...` |

FE phải đổi theo — xem [`02-products.md`](../api-documents/02-products.md).

## 5. Migration

```
- products.size_class        varchar(10) null      (DropColumn)
+ product_items.size_class   varchar(10) null      (AddColumn)
```

⚠️ **Dữ liệu cũ KHÔNG được backfill.** `Up()` drop thẳng cột ở `products` mà không copy giá trị xuống các `product_items` con — mọi sản phẩm đã khai `SizeClass` trước 14/08 mất giá trị đó, các SKU nhận `null`.

Chấp nhận được vì (a) `SizeClass` không tham gia chấm điểm nên không ảnh hưởng gợi ý, (b) ánh xạ 1→n không có luật tự động đúng (chậu nhỏ và chậu đại cùng nhận `Medium` là sai với cả hai). Vendor khai lại theo từng SKU.

`Down()` đảo lại đối xứng, cũng không backfill.

## 6. Không làm (backlog)

- **Đưa kích thước vào chấm điểm.** Nếu làm, lưu ý điểm số ở mức **`Product`** còn kích thước ở mức **`ProductItem`** → phải chốt quy tắc gộp trước (lấy SKU nhỏ nhất? SKU rẻ nhất? chấm theo từng SKU rồi lấy max?). Xem [`personalized-recommendation-v3.1.md`](./personalized-recommendation-v3.1.md) §12.
- **Suy `SizeClass` từ `LengthCm × WidthCm × HeightCm`** đã có sẵn trên SKU — hấp dẫn nhưng kích thước vận chuyển là **kích thước kiện hàng**, không phải kích thước vật phẩm khi bày (chậu cây gói trong thùng lớn hơn nhiều). Cần cột riêng nếu muốn chính xác.
