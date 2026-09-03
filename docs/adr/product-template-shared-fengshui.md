# ARD — Bản mẫu sản phẩm (product template): nguồn phong thủy dùng chung giữa các shop

> **Status:** Proposal — chưa code. Cần chốt 4 điểm ở §8 trước khi làm.
> **Tiền đề:** engine v3 + `ProductPlacement` (`product-placement-personal-recommendation.md`). Không đụng công thức chấm điểm, chỉ đổi **nguồn dữ liệu đầu vào** của sản phẩm.

---

## 1. Vấn đề

Toàn bộ dữ liệu phong thủy nằm trên `Product`, mà `Product` là **hàng của một shop cụ thể**:

| Bảng | Nội dung |
|---|---|
| `products.element_tho/kim/thuy/moc/hoa` | cache vector ngũ hành |
| `products.is_vector_overridden`, `products.placement` | |
| `product_element_inputs` | chất liệu / màu / hình khối → nguồn auto-calc |
| `product_element` | hành chính/phụ (tầng 3 fallback) |
| `product_vibes` | vibe |

Hai shop bán cùng một món → hai `Product` độc lập → hai bộ khai báo → engine có thể chấm **cùng một vật ra hai điểm khác nhau**. Sàn càng nhiều người bán, sai lệch càng lớn, và không có chỗ nào để sửa một lần cho tất cả.

### Vì sao KHÔNG đưa element vào `categories`

Phương án đầu tiên được cân nhắc và bị loại vì ba lý do, lý do đầu là bế tắc kiến trúc:

1. **`product_categories` là quan hệ n-n.** Một sản phẩm thuộc nhiều danh mục cùng lúc → sẽ có nhiều vector mâu thuẫn và **không có luật nào định nghĩa cái nào thắng**.
2. **Sai độ mịn.** "Đá phong thủy" chứa thạch anh tím (Thổ), obsidian (Thủy), thạch anh hồng (Hỏa). Ép cả nhóm một element là phá đúng thứ tinh vi nhất của engine.
3. **`Category` do người dùng tự tạo, không có `code` bất biến** — cùng lý do đã loại nó khi chọn chỗ cho `ProductPlacement`.

## 2. Nguyên tắc tách

Thuộc tính **khách quan của vật** khác với **điều kiện bán của shop**. Hai thứ này đang bị gộp vào một bảng.

| Thuộc về VẬT (mọi shop giống nhau) → template | Thuộc về SHOP → giữ ở `products`/`product_items` |
|---|---|
| chất liệu, màu, hình khối → vector ngũ hành | giá, tồn kho |
| `placement` | ảnh chụp thật, mô tả riêng |
| vibe | biến thể SKU, `size_class`, thông số vận chuyển |
| | model 3D, danh mục, style |

**`style` cố ý KHÔNG vào template**: nó không tham gia chấm điểm (xem audit seed-data) và là cảm nhận thẩm mỹ có thể khác nhau theo ảnh chụp của từng shop.

**`size_class` cố ý KHÔNG vào template**: vừa được chuyển xuống `product_items` vì kích thước biến thiên theo biến thể — chậu mini và chậu để sàn của cùng một loại cây là hai SKU khác nhau.

## 3. Schema

### 3.1 `product_templates` (mới)

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `id` | uuid PK | |
| `code` | varchar(50) UNIQUE NOT NULL | **bất biến**, vd `PLANT-KIM-TIEN`, `BRACELET-AMETHYST-8MM` |
| `name` | varchar(255) NOT NULL | tên chuẩn của sàn, dùng cho ô tìm bản mẫu |
| `description` | text NULL | |
| `placement` | varchar(20) NOT NULL DEFAULT `'Desk'` | như `products.placement` |
| `element_tho/kim/thuy/moc/hoa` | numeric(4,3) NULL | cache vector, tính từ `product_template_element_inputs` |
| `is_vector_overridden` | boolean NOT NULL DEFAULT false | admin nhập tay vector cho vật khó suy |
| `is_active` | boolean NOT NULL DEFAULT true | |
| + cột `BaseEntity` | | audit + soft-delete |

### 3.2 `product_template_element_inputs` (mới)

Bản sao của `product_element_inputs`, đổi khóa ngoại:

| Cột | Kiểu |
|---|---|
| `id` | uuid PK |
| `template_id` | uuid FK → `product_templates` ON DELETE CASCADE |
| `input_kind` | varchar(20) — Color / Material / Shape / DecorItem |
| `input_code` | varchar(50) — khớp `element_input_map.input_code` |

### 3.3 `product_template_elements` (mới)

Tầng 3 fallback, bản sao của `product_element`:

| Cột | Kiểu |
|---|---|
| `template_id` | uuid |
| `element` | varchar(10) |
| `is_primary` | boolean |
| PK | (`template_id`, `element`) |

> Đặt tên số nhiều (`..._elements`) — bảng cũ `product_element` đang số ít, lệch quy ước snake_case số nhiều của repo. Không đổi tên bảng cũ trong phạm vi này.

### 3.4 `product_template_vibes` (mới)

| Cột | Kiểu |
|---|---|
| `template_id` | uuid |
| `vibe_code` | varchar(50) FK → `vibes.code` |
| PK | (`template_id`, `vibe_code`) |

### 3.5 `products` — thêm 1 cột

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `template_id` | uuid **NULL** FK → `product_templates` ON DELETE SET NULL | Null = shop tự khai (hành vi hiện tại) |

Toàn bộ cột phong thủy cũ trên `products` **giữ nguyên**, dùng khi `template_id IS NULL`. Không migration dữ liệu, catalog hiện có chạy y nguyên.

## 4. Quy tắc đọc của engine

`RecommendationService.ToFacts` hiện dựng `ProductFacts` từ chính `Product`. Sau thay đổi:

```
template_id có giá trị  → vector, placement, vibes lấy TỪ TEMPLATE
template_id NULL        → lấy từ product (y như hiện tại)
```

**All-or-nothing, không override từng phần.** Cho phép "dùng template nhưng đổi riêng placement" sẽ tái tạo đúng sự phân mảnh mà template sinh ra để dập, và đẻ ra câu hỏi "cái nào thắng" ở từng trường. Shop không đồng ý với template thì bỏ template và tự khai toàn bộ.

`ProductVectorProvider.Build` **không đổi** — vẫn 3 tầng (override → DecorItem → Material/Color+Shape → fallback hành chính/phụ), chỉ khác nguồn `inputs` và `productElements` được truyền vào.

`IProductRepository.GetScorableCandidatesAsync` phải `Include` template + các bảng con của nó.

## 5. Luồng vendor

1. Form tạo sản phẩm có ô **tìm bản mẫu** (autocomplete theo `name`/`code`), đặt **trước** khối phong thủy.
2. Chọn được → khối phong thủy chuyển sang **chỉ đọc**, hiển thị vector + placement + vibe của template kèm dòng *"Thông số phong thủy do sàn chuẩn hóa"*, có nút "Không dùng bản mẫu" để quay lại tự khai.
3. Không tìm thấy → tự khai như hiện tại, kèm nút **"Đề nghị tạo bản mẫu"** gửi yêu cầu cho staff.

Không có bước 3 thì vendor lười sẽ bỏ qua template và phân mảnh vẫn còn — đây là rủi ro vận hành lớn nhất của cả thiết kế.

## 6. Luồng staff/admin

- CRUD template + quản lý input chất liệu/màu/hình khối.
- Sửa input của template → **tính lại cache vector của template**; KHÔNG cần đụng `products` vì product đọc xuyên qua template. Đây là lợi ích chính: sửa một chỗ, cả sàn đổi theo.
- Xem "N sản phẩm đang dùng bản mẫu này" trước khi sửa/vô hiệu hóa.
- **Gộp template trùng**: trỏ `products.template_id` sang template đích rồi vô hiệu hóa template nguồn.
- Duyệt hàng chờ "đề nghị tạo bản mẫu" từ vendor.

## 7. Rủi ro & cách chặn

**Xóa template làm sản phẩm mất phong thủy.** Nếu shop dùng template mà không khai gì riêng, khi template bị xóa (`SET NULL`) thì sản phẩm rơi khỏi mọi gợi ý. Chặn bằng: chỉ cho **vô hiệu hóa** (`is_active = false`) chứ không xóa cứng; hoặc khi gỡ template thì **copy snapshot** input + vector về product trước khi set null. Nên chọn phương án snapshot.

**Template quá thô sẽ gộp nhầm.** Vòng gỗ đàn hương và vòng gỗ sưa nhìn giống nhau nhưng khác chất liệu → khác hành. Template phải mịn **tới cấp chất liệu**, không phải cấp "vòng tay gỗ". Quy ước đặt `code` nên phản ánh điều đó.

**Backfill dữ liệu cũ không tự động được.** Không có cách nào để máy biết "Cây Kim Tiền để bàn" của shop A và "Cay kim tien" của shop B là một vật. Staff phải gom tay; có thể hỗ trợ bằng màn hình gợi ý theo tên gần giống (`unaccent` + similarity đã có extension trong repo).

## 8. Cần chốt trước khi code

1. **Ai được tạo template?** Chỉ staff/admin (an toàn, chậm) hay vendor tạo được rồi staff duyệt (nhanh, rủi ro loạn như category)?
2. **All-or-nothing** như §4, hay cần cho override từng trường?
3. **Template có mang `name`/`description` chuẩn để HIỂN THỊ không**, hay chỉ mang phong thủy? Nếu có, đây là bước đệm để sau này nâng lên mô hình SPU/offer đầy đủ (một trang sản phẩm, nhiều người bán, so giá).
4. **Có chặn một shop đăng nhiều sản phẩm cùng template không?** (unique `(garden_store_id, template_id)` — chống shop tự đăng trùng chính mình).

## 9. Không làm (out of scope)

- **Tách SPU/offer đầy đủ** kiểu Shopee/Tiki: gộp cả phần hiển thị, kéo theo viết lại trang chi tiết, tìm kiếm, giỏ hàng, đánh giá và đổi khóa ngoại ở gần như mọi bounded context. Thiết kế này cố ý chỉ lấy phần giá trị lớn nhất — nhất quán dữ liệu chấm điểm — với phần nhỏ nhất của chi phí đó, và **không chặn** đường nâng cấp sau này.
- **Ép chuẩn hóa catalog hiện có**: `template_id` nullable nên dữ liệu cũ chạy nguyên, chuẩn hóa dần.
- **Đổi tên bảng `product_element` → `product_elements`** cho đúng quy ước (việc riêng, đụng migration + repo).

## 10. Phương án rẻ hơn cần cân trước

Một nửa việc chuẩn hóa **đã có sẵn**: vector không do vendor gõ tay mà suy từ `element_input_map`. Hai shop cùng khai `Material: Wood` + `Color: Green` cho ra vector **giống hệt tuyệt đối**. Phân mảnh chỉ xảy ra khi họ khai khác nhau, hoặc khi đi đường "hành chính/phụ" tự do (tầng 3).

→ Chỉ cần **ẩn/bỏ đường khai hành chính/phụ ở form vendor**, bắt buộc đi qua Chất liệu/Màu/Hình khối. **Không bảng mới, không migration**, ăn được phần lớn lợi ích. Đổi lại vẫn không có "một nguồn sự thật" để sửa một chỗ ăn cả sàn — đó chính xác là thứ template mua thêm, với giá là 4 bảng + 1 màn hình quản trị + 1 quy trình duyệt.
