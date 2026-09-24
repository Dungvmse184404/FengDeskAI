# ADR — Cache cấu hình chấm điểm & siết RLS

**Trạng thái:** đã áp dụng
**Ngày:** 2026-09-25

## 1. Bối cảnh — đo trước đã

Đo trực tiếp trên `https://fengdesk.io.vn` (Resource Timing API), không đoán:

| | |
|---|---|
| TTFB | 57 ms — máy chủ/CDN không phải vấn đề |
| First Contentful Paint | 1 924 ms |
| Tổng tải về trang chủ | **9,1 MB**, trong đó **ảnh 8,0 MB / 27 file** |
| API chậm nhất | `/api/products?pageSize=12` — **2 851 ms** |

Hai nhóm nguyên nhân tách bạch: **tài nguyên tĩnh quá nặng** và **quá nhiều lượt đi về DB**.
DB ở Sydney, mỗi round-trip ~300 ms (xem `docs/ard/`, ghi chú tune VPS 20/09), nên **số LƯỢT hỏi DB
mới là chi phí chính, không phải khối lượng dữ liệu**.

## 2. Ảnh: 7,9 MB → 57 KB

Icon của `PopularCategories` hiển thị ở **40×40 px** nhưng đang ship ảnh gốc:

| File | Kích thước thật | Dung lượng | Sau khi sửa |
|---|---|---|---|
| `DragonStatue.png` | **3000×3000** | **4,2 MB** | 8,8 KB |
| `PlantPot.png` | 2000×2000 | 1,8 MB | 8,9 KB |
| `Lamp.png` | 2500×2500 | 853 KB | 2,9 KB |
| 4 icon còn lại | ~500² | 848 KB | 22 KB |
| `fengdesk_logo_2.png` (mọi trang) | 377×377 | 104 KB | 14 KB |

Đổi sang **WebP 128 px** (logo 192 px), xoá PNG gốc, sửa import. Tổng **7,9 MB → 57 KB**.

Lệnh tái lập (không cần thêm dependency vào `package.json`):

```bash
npx --yes sharp-cli -i "src/assets/icon/*.png" -o src/assets/icon \
    -f webp --quality 90 resize 128 128 --fit contain --background "rgba(0,0,0,0)"
```

⚠ Đừng thay ngược lại bằng ảnh gốc. Icon 40 px mà dùng ảnh 3000 px là thừa **5 625 lần** số điểm ảnh.

## 3. Cache cấu hình chấm điểm

### Vấn đề

`GET /workspace/{id}/element-analysis` bắn **10 lượt hỏi DB TUẦN TỰ** ⇒ ~3 s chỉ để chờ mạng.
Trong đó **5 lượt là cấu hình gần như không bao giờ đổi**, bị đọc lại ở mọi request chấm điểm —
và không chỉ endpoint này: trang chấm điểm một sản phẩm và mỗi lượt gợi ý đều trả giá y hệt.

### Quyết định

`CachedScoringConfigRepository` — lớp bọc `IScoringConfigRepository`, cache trong `IMemoryCache`
(đã có sẵn, theo đúng lối `TaxonomyService` đang dùng).

| Hàm | Cache? | Lý do |
|---|---|---|
| `GetScoringParamsAsync` | ✅ | tham số toàn cục |
| `GetElementInputMapAsync` | ✅ | map ngũ hành toàn cục |
| `GetWorkspaceTypeElementsAsync` | ✅ | vector loại phòng, tĩnh |
| `GetWorkPurposeModifiersAsync` | ✅ | modifier intent, tĩnh |
| `GetOccupationProfileAsync`, `GetOccupationsAsync` | ✅ | hồ sơ nghề, tĩnh |
| `GetWorkspaceProfileInputsAsync`, `GetProductElementInputs*` | ❌ | **theo request** — cache vào là trả nhầm phòng người khác |
| `GetOccupationByCodeAsync` | ❌ | trả entity **đang được theo dõi** cho luồng admin sửa; cache entity tracked là giữ tham chiếu tới `DbContext` đã huỷ |

Chỉ bọc các hàm đọc `AsNoTracking()` — đã kiểm từng hàm trong `ScoringConfigRepository`.

### Vô hiệu hoá cache

Khoá cache mang theo một **số "đời"** (`ScoringConfigCacheState`, singleton). Tăng số đó là mọi khoá
cũ thành mồ côi trong một nhịp, không phải đi xoá từng khoá.

`ScoringConfigAdminService` có helper `SaveAndInvalidateAsync` thay cho `_uow.SaveChangesAsync` ở
**cả 12 đường ghi**. Ai thêm đường ghi mới mà quên thì admin sửa xong engine vẫn chấm theo số cũ tới
10 phút — đó là lý do helper tồn tại thay vì gọi `Invalidate()` rải rác.

TTL 10 phút là **lưới an toàn, không phải cơ chế chính**: chạy nhiều instance thì instance A sửa cấu
hình không đụng được cache của instance B, nên cache phải tự hết hạn.

⚠ Danh sách trả ra là **dùng chung**, không phải bản sao. Người gọi phải coi là chỉ đọc — sửa tại chỗ
là đầu độc cache cho mọi request sau.

### Bớt thêm một lượt nữa

`WorkspaceProfileRepository.GetByIdForUserAsync` giờ `Include(w => w.WorkspaceType)`: gần như mọi nơi
đọc hồ sơ đều cần loại phòng ngay sau đó, mà hỏi riêng là thêm một round-trip. Một LEFT JOIN vào bảng
vài dòng rẻ hơn hẳn.

`LoadAnalysisContextAsync` **vẫn giữ nhánh dự phòng** hỏi DB khi navigation chưa nạp. Cố ý: biến
"chưa nạp" thành "không có loại phòng" sẽ âm thầm tắt trục cá nhân của cả căn phòng — hỏng mà không
báo lỗi.

### Kết quả

`element-analysis`: **10 → 5 lượt** khi cache ấm (hồ sơ+loại phòng, input phòng, placement, input sản
phẩm, user). Ước ~3 s → ~1,5 s. Lần gọi đầu sau khi khởi động/hết hạn vẫn đủ 10 lượt.

## 4. RLS — 4 bảng thủng

Kiểm tra quyền thật (không chỉ đọc cảnh báo linter):

```
model3d_requests, occupations, occupation_element_profiles, product_aspirations
  anon → SELECT, INSERT, UPDATE, DELETE, TRUNCATE      +      RLS = OFF
```

Không phải "lộ dữ liệu" mà là **ai cầm anon key đều xoá trắng được** — mà anon key theo thiết kế là
công khai. Ba bảng đầu là dữ liệu nền của bộ chấm điểm: mất là hỏng toàn bộ gợi ý.

Đã chạy `scripts/enable-rls-public-tables.sql` trên production. Sau khi chạy: **65/65 bảng bật RLS,
0 bảng sót, 0 bảng `FORCE`, 0 policy.**

Bật RLS mà **không** policy nào là đúng kiến trúc ở đây: API .NET nối bằng vai trò `postgres` — chủ
sở hữu bảng — nên **bỏ qua RLS** (đã xác nhận `relforcerowsecurity = false` toàn bộ). 61 bảng khác
đã ở đúng trạng thái này từ trước và chạy bình thường.

⚠ Đừng "sửa" cảnh báo INFO *"RLS Enabled No Policy"* bằng cách thêm policy — thêm là mở cửa ra
Internet. ⚠ Đừng bật `FORCE ROW LEVEL SECURITY` — bật là API mất quyền đọc sạch.

Cảnh báo `unaccent in public` thì **để yên**: `AppDbContext` map hàm `unaccent()` không kèm schema,
chuyển đi là gãy tìm kiếm không dấu.

## 5. Còn phải làm

1. **`/api/products` 2,85 s** — `ProductRepository` có 24 lời gọi `Include(`, nghi nổ join. Chưa đào.
2. **Cache-control sai**: `/assets/*` đã có hash nội dung trong tên nhưng trả
   `public, max-age=0, must-revalidate` ⇒ mỗi lần F5 phải hỏi lại cả 27 file. Phải là
   `max-age=31536000, immutable`. Nằm ở cấu hình nginx/Caddy trên VPS, **không** trong repo.
3. **JS 1,26 MB một chunk** — chưa code-split; Vite vẫn cảnh báo mỗi lần build.
4. **7,2 MB ảnh chết** trong `FengDeskAI_FE/src/assets/image/` (`FengDeskIllustration.jpeg`,
   `FengShuiWallpaper.png`, `FengShuiv2.png` — không chỗ nào import). Không ảnh hưởng tốc độ vì Vite
   không đóng gói, nhưng nên xoá.
5. **Đo lại `element-analysis` và `statistics` trên bản deploy** — cần tài khoản đăng nhập, chưa làm.

## 6. Kiểm thử

- API **399/399**, unit **385/385** sau thay đổi.
- Không thêm test riêng cho lớp cache: nó là decorator trong suốt, mọi test hiện có đều đi qua nó
  (DI đã đổi), nên 399 ca xanh chính là bằng chứng hành vi không đổi.
