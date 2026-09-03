# ARD — Sinh mã SKU ở tầng sàn

> **Status:** Implemented (2026-08-15). Không migration — cột `product_items.sku` giữ nguyên (nullable, max 20, unique index có filter).

---

## 1. Vấn đề

Backend chưa bao giờ sinh SKU: `ProductItem.Sku` luôn lấy nguyên văn từ request. Việc sinh mã nằm ở **FE, với hai công thức khác nhau**:

| Nơi | Công thức | Hỏng thế nào |
|---|---|---|
| `CreateProductPage` | 8 ký tự đầu tên + `-STD` | "Vòng tay thạch anh **tím**" và "**vàng**" → cùng ra `VONGTAYT-STD` |
| `ProductVariantsSection` | 6 ký tự đầu tên + 4 số ngẫu nhiên | 9000 tổ hợp/tiền tố; theo nghịch lý ngày sinh, ~110 biến thể cùng tên là 50% đụng |

Cả hai chạy ở trình duyệt nên **không thể** kiểm tra mã đã tồn tại. Đụng unique index `sku IS NOT NULL AND is_deleted = FALSE` → Postgres ném `DbUpdateException` → **500**, trong khi `ProductService` vốn đã kiểm FK cho vibes/styles và trả 400 tử tế.

## 2. Quyết định

### 2.1 Chuyển việc sinh mã về backend

`ISkuGenerator` / `SkuGenerator` trong `Features/Catalog/Services`. Đây là điểm cốt lõi: **chỉ tầng có DB mới biết mã đã dùng chưa**. Gọi ở `CreateAsync` (mỗi item) và `AddItemAsync` khi vendor để trống `Sku`.

### 2.2 Định dạng `FD-XXXXXXXX`

8 ký tự **Base32 Crockford** (`0123456789ABCDEFGHJKMNPQRSTVWXYZ` — bỏ `I`, `L`, `O`, `U` để không nhầm với 1/0 khi đọc mã qua điện thoại). Tổng 11 ký tự, thừa dưới giới hạn 20.

**Vì sao KHÔNG mã hóa thuộc tính nghiệp vụ** (tên, hành, placement) như phương án đầu tiên từng cân nhắc: **SKU phải bất biến**. Vendor đổi tên sản phẩm hoặc đổi hình thức sử dụng là chuyện thường; mã nhúng thuộc tính sẽ thành sai lệch, mà đổi mã thì hỏng đơn hàng cũ, hóa đơn và phiếu kho đã in. Thứ vendor/khách thực sự đọc là **tên biến thể** ("Hạt 8 ly") — SKU chỉ cần là định danh.

**Vì sao ngẫu nhiên chứ không tuần tự:** `SP-0001, SP-0002…` để lộ quy mô catalog và tốc độ thêm hàng cho đối thủ (bài toán đếm số hiệu). `32⁸ ≈ 1,1 nghìn tỷ` tổ hợp nên xác suất đụng cực thấp; vẫn thử lại tối đa 5 lần rồi mới ném.

Dùng `RandomNumberGenerator` (crypto) thay `Random` — không phải vì bảo mật mà để tránh trùng seed khi nhiều request tạo sản phẩm cùng thời điểm.

### 2.3 Vendor vẫn được nhập mã riêng

Cột giữ nullable. Bỏ trống → sàn sinh; tự nhập → `ResolveSkuAsync` kiểm trùng qua `IProductRepository.SkuExistsAsync` và trả **400** `ApiStatusMessages.Product.SkuDuplicated` thay vì để Postgres ném 500. Khi sửa biến thể, phép kiểm loại chính nó ra (`excludeItemId`).

### 2.4 Xóa cả hai generator ở FE

Một nguồn duy nhất. Ô SKU đổi từ bắt buộc thành tùy chọn, placeholder *"Để trống — hệ thống tự sinh"*.

## 3. Không làm (out of scope)

- **Đổi mã cho dữ liệu cũ.** SKU hiện có (gồm cả `KT-WHITE`, `LH-S`… từ `CatalogDemoSeeder`) giữ nguyên — đổi mã đã phát hành là điều ADR này chống lại.
- **Mã theo cửa hàng** (`{storeCode}-…`): `GardenStore` chưa có mã ngắn bất biến, và store cũng có thể đổi tên.
- **Kiểm trùng ở tầng DB bằng cách bắt exception**: giữ cách kiểm trước, đơn giản và cho thông báo tốt hơn. Đánh đổi: vẫn có khe race cực hẹp giữa lúc kiểm và lúc lưu — unique index là lưới cuối, khi đó vendor thấy 500 (hiếm, chấp nhận).

## 4. File thay đổi

**Backend**
- `Features/Catalog/Services/ISkuGenerator.cs`, `SkuGenerator.cs` — **mới**.
- `Features/Catalog/Services/ProductService.cs` — inject generator, helper `ResolveSkuAsync`, áp ở create/add/update item.
- `Interfaces/Repositories/IProductRepository.cs` + `ProductRepository.cs` — `SkuExistsAsync`.
- `Common/Constants/ApiStatusMessage.cs` — `Product.SkuDuplicated`.
- `DependencyInjection.cs`.

**Frontend**
- `manager/pages/CreateProductPage.tsx` — bỏ `useEffect` sinh `NAME8-STD`; SKU thành tùy chọn.
- `manager/components/ProductVariantsSection.tsx` — bỏ sinh `PREFIX-RAND4`; bỏ luôn prop `productName` (chỉ generator cũ dùng).

## 5. Test cases tối thiểu

1. Tạo sản phẩm không nhập SKU → item có mã dạng `FD-` + 8 ký tự, không chứa `I/L/O/U`.
2. Tạo 2 sản phẩm tên gần giống ("… tím" / "… vàng"), cả hai bỏ trống SKU → hai mã khác nhau (trước đây trùng).
3. Nhập tay SKU đã tồn tại → **400** kèm `SkuDuplicated`, không phải 500.
4. Sửa biến thể mà giữ nguyên SKU của chính nó → thành công (không tự báo trùng).
5. Thêm biến thể bỏ trống SKU → sinh mã mới, khác mã các biến thể còn lại.
6. SKU cũ trong DB (`KT-WHITE`…) không bị đổi sau khi deploy.
