# ADR — Gộp migration EF (squash) không cần chạm DB remote

> **Status:** Done (2026-09-19) — 56 migration → **15** (1 baseline + 14), thư mục 8.6 MB → 3.3 MB. Áp dụng
> được cho local, test và remote mà **không chạy lệnh nào trên remote**.

## 1. Vấn đề

`src/FengDeskAI.Infrastructure/Persistence/Migrations/` có 56 migration (30 của riêng tháng 6), mỗi cái kèm
một `.Designer.cs` ~190 KB chụp toàn bộ model — 8.6 MB, `migrations list` dài hai màn hình, và mỗi lần đọc
lịch sử schema phải lần qua hàng chục file "thêm cột rồi đổi tên rồi gỡ".

Ràng buộc: **ba DB ở ba mốc khác nhau** — dev + test đã ở tip, còn Supabase (remote) mới ở migration #43
(`20260802073311_NormalizeModel3DQueueFlow`), thiếu 13 migration từ 14/08 (remote chỉ migrate lúc deploy).
Cách squash "xoá hết, tạo `InitialSchema` mới" chuẩn của EF sẽ làm remote **crash lúc deploy**: id mới không có
trong `__EFMigrationsHistory` ⇒ EF chạy `CREATE TABLE` lên DB đã có bảng.

## 2. Quyết định — gộp đúng phần *mọi* môi trường đã có, baseline mang **id cũ**

```
trước:  #1 … #43 | #44 … #56          (remote ở #43, dev/test ở #56)
sau:    [InitialSchema ≡ #43] | #44 … #56
```

- Baseline **thay 43 migration đầu** và mang attribute `[Migration("20260802073311_NormalizeModel3DQueueFlow")]`
  — đúng id của #43. EF chỉ so attribute với bảng history: DB nào đã có dòng #43 (cả ba DB) thì coi baseline là
  *đã áp* và bỏ qua; 42 dòng history cũ thừa ra EF không biết tới nên không đụng. DB trống thì chạy baseline rồi
  tiếp 14 migration sau.
- 14 migration từ #44 **giữ nguyên** ⇒ remote deploy lần tới vẫn áp đúng 13 + 1 migration còn thiếu như chưa
  từng có squash. **Không cần chạm remote.**
- Baseline sinh bằng máy, không viết tay: checkout worktree tại commit `5252f8f` (commit cuối có đúng 43
  migration), xác nhận `migrations add Probe` ra **rỗng** (model ≡ snapshot #43), xoá thư mục migration, build
  lại, `migrations add InitialSchema` ⇒ 61 bảng / 102 index+FK. Chép sang repo chính, đổi namespace + attribute.
- Ba thứ ngoài model mà 43 migration cũ tạo bằng SQL tay được giữ ở cuối `Up` của baseline (đối chiếu
  `pg_constraint`/`pg_extension` của dev DB): extension `unaccent`, 5 dòng lookup `elements`, 7 FK
  `ON UPDATE CASCADE` (`product_element`/`workspace_profiles`/`feng_shui_rules` → `elements`;
  `product_styles`/`product_vibes`/`workspace_profiles` → `styles`/`vibes`). Backfill dữ liệu của migration cũ
  (owner cửa hàng, trạng thái staff, participant chat…) không cần cho DB trống nên bỏ.

## 3. Kiểm chứng

| Kiểm | Kết quả |
|---|---|
| `dotnet ef migrations list` trên dev DB | baseline hiện là `20260802073311_NormalizeModel3DQueueFlow`, **0 pending** |
| DB test **xoá tạo lại từ trống** → fixture migrate (baseline + 14) + seed → toàn bộ API test | 388/391 — cùng 3 ca DEF-10/12/16 pre-existing |
| `pg_dump --schema-only` DB trống vs dev DB | không thiếu object nào; khác biệt chỉ là tên constraint NOT NULL do PG18 in ra, thứ tự cột, một `DEFAULT 'User'` tạm của backfill cũ |
| Phát hiện tiện thể | dev DB có bảng **không thuộc model**: `ai_order_drafts` (entity đã gỡ, chưa từng có migration drop) và bộ `tenant/merchant/credit_*` của project khác dùng chung DB local — không liên quan squash, không đụng |

Lỗi lộ ra khi chạy DB trống (đã sửa cùng lượt): `scoring_params.description` là `varchar(200)`, ba mô tả mới
trong `seed-data/scoring-params.json` dài hơn ⇒ seeder nổ và app không lên. DB có sẵn không lộ vì seeder không
đụng row đã có. Rút mô tả ≤ 200 và `ScoringParamSeeder` cắt + cảnh báo thay vì chết.

## 4. Lần sau muốn gộp tiếp (khi remote đã ở tip)

1. Xác nhận **mọi** môi trường đã áp migration cuối `X` (`SELECT max("MigrationId") FROM "__EFMigrationsHistory"`
   trên từng DB; remote qua Supabase).
2. `git worktree add ../_squash <commit có đúng tập migration tới X>`; trong đó tạo `appsettings.json` tối thiểu
   (chỉ cần `ConnectionStrings:DefaultConnection`, không kết nối thật), `dotnet build`, rồi
   `migrations add Probe` **phải rỗng**. Xoá `Persistence/Migrations/*.cs`, **build lại** (không `--no-build` —
   assembly cũ vẫn chứa snapshot cũ), `migrations add InitialSchema`.
3. Chép `.cs` + `.Designer.cs` sang repo chính, đổi namespace về `…Persistence.Migrations`, attribute về
   **id của `X`**, gắn lại phần SQL ngoài model (mục 2), xoá các migration ≤ X. Snapshot giữ nguyên.
4. Kiểm như mục 3: `migrations list` không pending trên dev; drop-create DB test rồi chạy full API test;
   diff `pg_dump --schema-only`.

Không bao giờ squash phần remote chưa áp — đó là cách duy nhất làm deploy chết.
