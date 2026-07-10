
# ARD — Recommendation Scoring v3 (đã triển khai)

> Trạng thái: **ĐÃ CODE** · Migration `20260707160422_RecommendationScoringV3`.
> Tài liệu này mô tả engine chấm điểm gợi ý **đang chạy trong code** (không phải đề xuất).
> API admin: xem [25-scoring-config](../api-documents/25-scoring-config.md).

## 1. Ý tưởng cốt lõi

Mệnh của **phòng** và của **sản phẩm** đều là **vector 5 hành** (`ElementVector` trên {Kim, Moc, Thuy, Hoa, Tho}).
Sản phẩm là "viên thuốc" bù mất cân bằng của phòng: bơm vào hành **thiếu** → điểm dương, hành **thừa** → điểm âm.

## 2. Data model (đã có trong DB)

**Bảng mới (6):**

| Bảng                             | Vai trò                                                                       |
| -------------------------------- | ----------------------------------------------------------------------------- |
| `workspace_type_elements`        | Vector `Ideal`/`Interior` theo loại phòng (cột `source`, `element`, `weight`) |
| `element_input_map`              | Tra `(input_kind, input_code)` → hành + trọng số; dùng chung phòng & sản phẩm |
| `workspace_profile_inputs`       | Màu/vật liệu/hình khối thực tế của 1 phòng                                    |
| `product_element_inputs`         | Màu/vật liệu/hình khối của 1 sản phẩm (auto-calc vector)                      |
| `work_purpose_element_modifiers` | Bẻ vector lý tưởng theo mục đích (Intent); `delta` có thể âm                  |
| `scoring_params`                 | Tham số phẳng của engine (`code`, `value`)                                    |

**Cột thêm vào bảng cũ:**
- `workspace_types.scope` (`Private/Shared/Public`).
- `workspace_profiles.entrance_direction`, `.toilet_direction`, `.dark_directions` (hướng bị chắn).
- `products.element_kim/moc/thuy/hoa/tho` (cache vector) + `.is_vector_overridden`.

**Enum mới:** `WorkspaceScope {Private, Shared, Public}`, `ElementInputKind {Color, Material, Shape}`.

**Legacy (giữ, engine v3 KHÔNG dùng):** `workspace_profiles.feng_shui_element`, `workspace_types.personal_weight`.

## 3. Dựng vector (code: `ElementVectorBuilders.cs`)

**Vector phòng** — `WorkspaceVectorBuilder`:
1. `BuildIdeal` — từ `workspace_type_elements` (source=`Ideal`), chuẩn hóa Σ=1.
2. `ApplyIntent` — cộng các `WorkPurposeElementModifier` (delta ±) rồi chuẩn hóa → `adjustedIdeal`.
3. `BuildCurrent` — nếu phòng có `workspace_profile_inputs` → cộng qua `element_input_map`; nếu không → fallback source=`Interior`.

**Vector sản phẩm** — `ProductVectorProvider.Build`, 3 tầng fallback:
1. Override tay (`is_vector_overridden`) → dùng vector `element_*` nhập sẵn.
2. Có `product_element_inputs` → `MATERIAL_SHARE`·(chất liệu) + `COLOR_SHARE`·(màu+hình).
3. Backfill từ `product_elements` (primary `FALLBACK_PRIMARY`, secondary `FALLBACK_SECONDARY`).

## 4. Chấm điểm (code: `RecommendationScorer.cs`)

```
gap   = adjustedIdeal − current           // + = thiếu cần bù, − = thừa cần tránh
gapScore(product) = gap · productVector / |gap|₁
```

Các bước cho mỗi sản phẩm (`ScoreOne`):
1. **Intent filter (hard):** thiếu vibe khớp mục đích → loại khỏi danh sách.
2. **User constraint:** hành trội sản phẩm khắc mệnh user (`BiKhac`) → **loại** nếu `Private`, **trừ `USER_CONFLICT_PENALTY`** nếu `Shared/Public`.
3. **Gap score:** như trên; kèm mô tả bù/thừa hành nào.
4. **Directional Validation:** hướng hợp = hướng cùng hành trội ∪ hướng sinh ra hành trội, trừ hướng bị chắn (`entrance/toilet/dark`). Còn hướng hợp → `placementHint`; hết → trừ `DIRECTION_PENALTY`.
5. `score = clamp(gapScore − userPenalty − dirPenalty, −1, 1)`, sắp giảm dần, lấy `topN`.

## 5. Tham số engine (`scoring_params`, default trong `ScoringParameters`)

| Code | Default | | Code | Default |
|------|:---:|---|------|:---:|
| `SELF_SHARE` | 0.60 | | `USER_CONFLICT_PENALTY` | 0.30 |
| `SUPPORT_SHARE` | 0.30 | | `DIRECTION_PENALTY` | 0.15 |
| `CHILD_SHARE` | 0.10 | | `FALLBACK_PRIMARY` | 0.70 |
| `MATERIAL_SHARE` | 0.60 | | `FALLBACK_SECONDARY` | 0.30 |
| `COLOR_SHARE` | 0.40 | | | |

## 6. Response API (v3)

`RecommendationResponse` thêm `gap` (breakdown ideal/current/gap từng hành) và mỗi item thêm `placementHint`.
Chi tiết: [18-recommendations](../api-documents/18-recommendations.md).

## 7. Khoảng trống đã biết (DB có, API chưa lộ)

Các field/bảng sau đã ở DB + engine nhưng **chưa** vào request/response của API workspace thường:
- `workspace_types.scope`
- `workspace_profiles.entrance_direction / toilet_direction / dark_directions`
- `workspace_profile_inputs` (màu/vật liệu của phòng do user khai)

→ Hiện chỉ nạp được qua seeder/DB. Cần bổ sung vào `CreateWorkspaceProfileRequest`/`WorkspaceProfileResponse` và workspace-types DTO nếu muốn user tự khai.

## 8. Bản đồ file code

```
Engine/   ElementVector.cs · ElementVectorBuilders.cs · RecommendationScorer.cs · ScoringModels.cs · FengShuiCalculator.cs
Services/ RecommendationService.cs · ScoringConfigAdminService.cs
Domain/Entities/Recommendation/  WorkspaceTypeElement · ElementInputMap · WorkspaceProfileInput · ProductElementInput · WorkPurposeElementModifier · ScoringParam
WebAPI/Controllers/  RecommendationsController.cs · ScoringConfigController.cs
Seeding/  WorkspaceTypeElementSeeder · ElementInputMapSeeder · WorkPurposeModifierSeeder · ScoringParamSeeder
```
