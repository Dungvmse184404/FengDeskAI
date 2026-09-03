# seed-data — Dữ liệu seeding tách khỏi code

Các seeder trong `src/FengDeskAI.Infrastructure/Persistence/Seeding/` đọc data từ các file JSON ở đây (qua `SeedDataLoader`) thay vì hard-code. Sửa data → chạy lại seed, **không cần build lại code**.

## Files

### Nhóm A — dữ liệu tham chiếu (chạy ở MỌI môi trường)

| File                           | Bảng                             | Có weight?                                      |
| ------------------------------ | -------------------------------- | ----------------------------------------------- |
| `styles-vibes.json`            | `styles`, `vibes`, `elements`    | Không                                           |
| `scoring-params.json`          | `scoring_params`                 | Có (`value`) các cặp *Share phải giữ tổng = 1.0 |
| `element-input-map.json`       | `element_input_map`              | Có (`weight`)                                   |
| `work-purpose-modifiers.json`  | `work_purpose_element_modifiers` | Có (`delta`, có thể âm)                         |
| `workspace-types.json`         | `workspace_types`                | Có (`personalWeight` — **legacy**, engine đọc `scope`) |
| `workspace-type-elements.json` | `workspace_type_elements`        | Có (vector 5 hành, tổng = 1.0)                  |

### Nhóm B — dữ liệu DEMO (đuôi `-demo`, bỏ ở production)

| File                             | Seeder đọc                                                    | Ghi chú |
| -------------------------------- | ------------------------------------------------------------- | --- |
| `catalog-demo.json`              | `CatalogDemo` (20) · `ProductFengShuiDemo` (21) · `ProductElementInputDemo` (22) | **3 seeder dùng chung 1 file** — xem ghi chú dưới |
| `carry-products-demo.json`       | `PlacementProductDemo` (23)                                     | Sản phẩm mẫu cho một `ProductPlacement` — placement khai trong file |
| `product-aspirations-demo.json`  | `ProductAspirationDemo` (24)                                    | Thẻ mục tiêu (Wealth/Health…). `approveOnSeed` = có duyệt luôn không |

#### Vì sao 3 seeder dùng chung `catalog-demo.json`

Trước đây mỗi seeder giữ **bảng riêng** trong code và khớp sản phẩm bằng **chuỗi con trong tên**
(`"Kim Tiền"` ⊂ `"Cây Kim Tiền để bàn"`). Đổi tên một sản phẩm là hai seeder kia **âm thầm không khớp
nữa** — không lỗi, không cảnh báo, chỉ là sản phẩm thiếu thuộc tính và engine bỏ qua nó.

Nay tên sản phẩm khai **đúng một chỗ**, các seeder khớp bằng **tên đầy đủ**:

| Section trong file | Seeder dùng |
| --- | --- |
| `vendor` · `store` · `categories` · `tags` · `products[].name/description/category/placement/items` | `CatalogDemoSeeder` |
| `products[].primaryElement/secondaryElements/vibes/styles` · `products[].items[].sizeClass` · `defaults` | `ProductFengShuiDemoSeeder` |
| `products[].elementInputs` | `ProductElementInputDemoSeeder` |

`defaults` chỉ áp cho sản phẩm **không có trong file** (vd tạo tay lúc test) mà chưa khai phong thủy.

> ⚠️ **`sizeClass` nằm ở `products[].items[]`, không phải ở sản phẩm cha** — kích thước biến thiên theo
> SKU (migration `MoveSizeClassToProductItem`). Đừng thêm `sizeClass` ở cấp product, seeder không đọc.

> **Quy ước đặt tên:** file demo **phải** có hậu tố `-demo`. Nhìn tên là biết có được mang lên production hay không, không cần mở seeder ra đọc.

Không tách ra file (đúng theo bảng quyết định bên dưới): `FengShuiRuleSeeder` (25 luật tính từ `FengShuiCalculator` — chép ra file là tạo 2 nguồn sự thật), `GeographySeeder`/`GeoSyncService` (đồng bộ từ GHN), `AdminUserSeeder` (1 dòng, cần biến môi trường).

## Hệ số scale weight

Mỗi file weight có trường `weightScale` (mặc định `1.0`). Weight lưu vào DB = giá trị trong file × scale.

Thứ tự ưu tiên: `weightScale` trong file → config `Seeding:WeightScale` (appsettings) → `1.0`.

Ví dụ giảm ảnh hưởng workspace weights còn một nửa: đặt `"weightScale": 0.5` trong `workspace-types.json` / `workspace-type-elements.json`. **Không nên** scale `scoring-params.json` (các tham số tỉ trọng phải giữ tổng = 1.0).

## Đường dẫn

`SeedDataLoader` tìm thư mục `seed-data/` theo thứ tự:

1. Config `Seeding:DataPath` (appsettings hoặc env `Seeding__DataPath`) — đường dẫn tuyệt đối nếu muốn để file chỗ khác.
2. `{thư mục app}/seed-data` (Docker — Dockerfile đã COPY sẵn).
3. Dò ngược thư mục cha từ chỗ chạy lệnh (dev: chạy từ `src/FengDeskAI.WebAPI` sẽ thấy `<repo>/seed-data`).

## Khi nào tách ra file, khi nào để trong seeder

| Đặc điểm của data | Để ở đâu |
| --- | --- |
| Người **không phải dev** cần sửa (BA/domain expert chỉnh trọng số, thêm loại phòng) | 📄 **File JSON** |
| Sửa xong muốn chạy lại `-- seed` mà **không build lại** | 📄 **File JSON** |
| Là **tham số nghiệp vụ** cần tinh chỉnh nhiều lần (weight, delta, share) | 📄 **File JSON** |
| Suy ra được từ **code** (25 luật ngũ hành từ `FengShuiCalculator`) — chép ra file là tạo 2 nguồn sự thật | 💻 **Trong seeder** |
| Lấy từ **API bên ngoài** (địa giới GHN) | 💻 **Trong seeder** |
| Cần **secret / biến môi trường** (mật khẩu admin) | 💻 **Trong seeder** |
| Chỉ 1–2 dòng và **không ai chỉnh** | 💻 **Trong seeder** |

**Nguyên tắc:** file JSON cho *thứ sẽ được chỉnh sửa*, code cho *thứ được suy ra*. Nghi ngờ thì chọn file — chi phí đọc thêm 1 file rẻ hơn chi phí build lại để đổi một con số.

## Lưu ý quan trọng

Các seeder **idempotent** — chỉ INSERT row còn thiếu, không UPDATE row đã có. Đổi weight/scale trong file **không tự cập nhật** data đã seed trong DB; cần xóa row cũ (hoặc reset DB dev) rồi chạy lại `dotnet run -- seed`.
