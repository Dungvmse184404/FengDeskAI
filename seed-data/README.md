# seed-data — Dữ liệu seeding tách khỏi code

Các seeder trong `src/FengDeskAI.Infrastructure/Persistence/Seeding/` đọc data từ các file JSON ở đây (qua `SeedDataLoader`) thay vì hard-code. Sửa data → chạy lại seed, **không cần build lại code**.

## Files

| File                           | Bảng                             | Có weight?                                      |
| ------------------------------ | -------------------------------- | ----------------------------------------------- |
| `styles-vibes.json`            | `styles`, `vibes`, `elements`    | Không                                           |
| `scoring-params.json`          | `scoring_params`                 | Có (`value`) các cặp *Share phải giữ tổng = 1.0 |
| `element-input-map.json`       | `element_input_map`              | Có (`weight`)                                   |
| `work-purpose-modifiers.json`  | `work_purpose_element_modifiers` | Có (`delta`, có thể âm)                         |
| `workspace-types.json`         | `workspace_types`                | Có (`personalWeight`)                           |
| `workspace-type-elements.json` | `workspace_type_elements`        | Có (vector 5 hành, tổng = 1.0)                  |

Không tách ra file: `FengShuiRuleSeeder` (25 luật tính từ `FengShuiCalculator`, không có bảng hard-code), `GeographySeeder`/`GeoSyncService` (đồng bộ từ GHN), 3 demo seeder (`CatalogDemo`, `ProductFengShuiDemo`, `ProductElementInputDemo` — data demo, sẽ bỏ ở production).

## Hệ số scale weight

Mỗi file weight có trường `weightScale` (mặc định `1.0`). Weight lưu vào DB = giá trị trong file × scale.

Thứ tự ưu tiên: `weightScale` trong file → config `Seeding:WeightScale` (appsettings) → `1.0`.

Ví dụ giảm ảnh hưởng workspace weights còn một nửa: đặt `"weightScale": 0.5` trong `workspace-types.json` / `workspace-type-elements.json`. **Không nên** scale `scoring-params.json` (các tham số tỉ trọng phải giữ tổng = 1.0).

## Đường dẫn

`SeedDataLoader` tìm thư mục `seed-data/` theo thứ tự:

1. Config `Seeding:DataPath` (appsettings hoặc env `Seeding__DataPath`) — đường dẫn tuyệt đối nếu muốn để file chỗ khác.
2. `{thư mục app}/seed-data` (Docker — Dockerfile đã COPY sẵn).
3. Dò ngược thư mục cha từ chỗ chạy lệnh (dev: chạy từ `src/FengDeskAI.WebAPI` sẽ thấy `<repo>/seed-data`).

## Lưu ý quan trọng

Các seeder **idempotent** — chỉ INSERT row còn thiếu, không UPDATE row đã có. Đổi weight/scale trong file **không tự cập nhật** data đã seed trong DB; cần xóa row cũ (hoặc reset DB dev) rồi chạy lại `dotnet run -- seed`.
