# ADR — Refactor: Product Create Feng-Shui Flow (garden owner)

> **Status:** Proposal.
> **Vấn đề:** luồng tạo product hiện chỉ chở **đường thủ công** (`PrimaryElement/SecondaryElements` — thứ vendor không rành phong thủy không biết chọn), còn **đường tự động** (khai vật liệu/màu → engine tự tính vector, tầng 2) nằm ở endpoint riêng `PUT /products/{id}/element-inputs` gọi sau khi tạo. Hệ quả: (a) ưu tiên UX ngược với mục tiêu "vendor không cần biết phong thủy"; (b) hở bước — FE quên gọi bước 2 → product không có dữ liệu phong thủy, engine trả `ElementVector.Zero`, và sau này polarity (scoring v4) cũng null theo.

## Nguyên tắc

Giống workspace: **hỏi thứ vendor chắc chắn biết** (sản phẩm làm bằng gì, màu gì, dáng gì), không hỏi thứ họ không biết (hành). Đường khai hành trực tiếp giữ lại làm advanced cho người rành. Mọi con đường đều đi qua một chỗ tính vector duy nhất.

## 1. BE — Application

### 1.1 `DTOs/ProductDtos.cs` — `CreateProductRequest` thêm

```csharp
/// <summary>Tín hiệu vật lý (vật liệu/màu/hình khối) — nguồn auto-calc vector, ưu tiên hơn PrimaryElement.</summary>
public List<ProductElementInputDto> ElementInputs { get; set; } = new();
```

(`ProductElementInputDto { ElementInputKind Kind; string Code; }` — tái dùng DTO trong `ProductVectorDtos.cs` nếu đã có, không tạo trùng.)

`PrimaryElement/SecondaryElements` giữ nguyên, docstring sửa thành "đường advanced / fallback tầng 3".

### 1.2 Tách logic chung — `Services/ProductVectorApplier.cs` (mới, internal)

Rút phần thân của `ProductVectorService.SetElementInputsAsync` (validate codes với `element_input_map` → replace `product_element_inputs` → recompute + ghi cache 5 cột `Element*` → `IsVectorOverridden = false`) thành helper dùng chung:

```csharp
internal static class ProductVectorApplier
{
    /// <summary>Trả về lỗi validate (codes lạ) hoặc null. KHÔNG SaveChanges — caller commit.</summary>
    public static async Task<string?> ApplyInputsAsync(
        Product product, IReadOnlyList<(ElementInputKind Kind, string Code)> inputs,
        IUnitOfWork uow, CancellationToken ct);
}
```

- `ProductVectorService.SetElementInputsAsync` → gọi applier (hành vi giữ nguyên 100%, regression test bằng API cũ).
- `ProductService.CreateAsync` → sau `ApplyFengShui`, nếu `request.ElementInputs.Count > 0` gọi applier với product vừa add. Lỗi validate → `Failure(400)` (không tạo product — cùng 1 `SaveChangesAsync`, atomic sẵn).

### 1.3 Thứ tự ưu tiên khi create có cả hai

Không đổi engine: `ProductVectorProvider.Build` đã ưu tiên tầng 2 (inputs) trên tầng 3 (primary/secondary). Create nhận cả `ElementInputs` lẫn `PrimaryElement` → lưu cả hai, vector cache tính theo inputs; `PrimaryElement` chỉ còn vai trò fallback nếu sau này inputs bị xóa. Ghi rõ vào Swagger doc của endpoint.

## 2. BE — WebAPI

### 2.1 Endpoint tra cứu vocabulary cho form vendor (mới)

`GET /scoring-config/element-inputs` hiện là admin-only. Form vendor cần danh sách code hợp lệ để render dropdown:

```
GET /api/catalog/element-input-codes        [Authorize]  (mọi user đăng nhập)
→ [ { kind: "Material", codes: ["Wood","Metal","Ceramic",…] }, … ]
```

Impl: query `element_input_map` distinct `(kind, code)`, cache 10' (`IMemoryCache`). Đặt ở `ElementsController` (đã là chỗ tra cứu catalog lookup). **Không** trả weight/element — vendor không cần biết mapping, tránh gợi ý "chọn code để ăn hành đẹp".

## 3. FE — form tạo sản phẩm (kênh người bán, `features/shop` + form dùng chung `ProductFengShuiForm`)

Sắp lại thứ tự section trong form create:

1. **"Đặc điểm sản phẩm"** (mặc định, luôn hiện): 3 nhóm chọn — Vật liệu, Màu chủ đạo, Hình khối — options từ `element-input-codes` (TanStack Query, `staleTime` 10'). Multi-select chip. Không nhắc chữ "ngũ hành" ở đây — với vendor đây chỉ là mô tả sản phẩm.
2. Vibes / Styles / SizeClass — giữ như hiện tại.
3. **"Phong thủy nâng cao"** (collapse, mặc định đóng): chọn hành chính/phụ trực tiếp + link tới màn override vector (đã có). Tooltip: "Chỉ dùng nếu bạn đã biết chính xác — thường không cần, hệ thống tự tính từ Đặc điểm sản phẩm."
4. Sau khi tạo thành công, nếu cả ElementInputs lẫn PrimaryElement đều trống → toast nhắc "Sản phẩm chưa có dữ liệu phong thủy nên sẽ không xuất hiện trong gợi ý — bổ sung Đặc điểm sản phẩm nhé" (không chặn).

Types/api: `product.d.ts` thêm `elementInputs` vào create payload; `*.api.ts` tương ứng.

## 4. Test

1. Create chỉ với `ElementInputs` (Wood + Brown) → 201, cache 5 cột `Element*` non-null, `IsVectorOverridden = false`, recommendation nhận diện được ngay (tầng 2).
2. Create với code lạ (`Material:Plastic99`) → 400, product KHÔNG được tạo (atomic).
3. Create chỉ với `PrimaryElement` (đường cũ) → hành vi y như trước (tầng 3) — regression.
4. Create cả hai → vector theo inputs (tầng 2 thắng), `product_elements` vẫn lưu primary.
5. `PUT /element-inputs` sau create → kết quả byte-identical với trước refactor (applier không đổi hành vi).
6. `GET /catalog/element-input-codes` với token customer → 200; anonymous → 401.

## Quan hệ với các ADR khác

- Chuẩn bị nền cho `recommendation-scoring-v4-polarity.md`: polarity của product suy từ chính `ElementInputs` này — luồng create sửa xong thì v4 không cần vendor làm gì thêm.
- Cùng tinh thần với `refactor-workspace-input-relaxation.md` (không hỏi thứ user không biết) — có thể review cùng đợt.
