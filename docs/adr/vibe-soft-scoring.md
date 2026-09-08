# ARD — Vibe: từ bộ lọc cứng sang tham số điểm

> **Status:** Implemented (2026-08-15), **tắt bằng kill-switch** — merge với `VIBE_FILTER_HARD = 1.0` nên ranking giữ nguyên v3. Bật bằng cách hạ tham số qua API `scoring-config`, không cần deploy.
> **Tiền đề:** engine v3. Không đổi kiến trúc, không migration — chỉ thêm 4 row `scoring_params`.

---

## 1. Vấn đề

`RecommendationScorer.ScoreOne` bước 2a loại thẳng ứng viên khi sản phẩm không mang vibe hợp `WorkPurpose` của phòng:

```csharp
if (targetVibe is { } vibe && !product.Vibes.Contains(vibe))
    if (mode == ScoreMode.Rank) return null;
```

Ba điểm sai:

1. **Chạy trước khi chấm ngũ hành.** Sản phẩm bù đúng hành phòng đang thiếu vẫn biến mất chỉ vì thiếu một dòng `product_vibes` — trong khi ngũ hành mới là lõi nghiệp vụ.
2. **Gộp "lệch" với "chưa biết".** Sản phẩm *có* vibe `Relax` trong phòng `Focus` là lệch thật. Sản phẩm *chưa khai vibe nào* là thiếu dữ liệu. Cả hai cùng bị xóa sổ — mà với catalog mới, phần lớn ca là vế thứ hai. Đây là nguyên nhân số 1 của lỗi 422 "Không có sản phẩm nào phù hợp".
3. **Bất nhất sẵn có.** `WorkPurpose.Other` → `TargetVibe` trả `null` → không lọc gì. Nên 1 trong 13 purpose đã hành xử mềm, 12 cái còn lại loại cứng.

## 2. Quyết định

### 2.1 Tách "lệch" khỏi "chưa biết" — hai tham số

Cùng nguyên tắc đã dùng ở `recommendation-scoring-v4-polarity.md` §3.2 (polarity chưa xác định thì giữ hành vi cũ, không coi unknown là một giá trị hợp lệ), áp theo chiều ngược lại:

| Code | Default | Áp khi |
|---|---|---|
| `VIBE_MISMATCH_PENALTY` | `0.40` | Sản phẩm **có** vibe nhưng không chứa vibe mục tiêu |
| `VIBE_UNKNOWN_PENALTY` | `0.10` | Sản phẩm **chưa khai** vibe nào |

```
score = clamp(gapScore − userPenalty − dirPenalty − vibePenalty, −1, 1)
```

Cùng dạng với `USER_CONFLICT_PENALTY`/`DIRECTION_PENALTY` sẵn có.

> ⚠️ **v3.2 đã nhân đôi hai giá trị này** (0.20→0.40 · 0.05→0.10) cùng lúc với `USER_CONFLICT_PENALTY`
> và `DIRECTION_PENALTY`, vì miền `gapScore` nở từ ±0.5 lên ±1.0 — xem
> [score-explainability-v3.2 §8.3](./score-explainability-v3.2.md). Tỉ lệ giữa hai penalty **không đổi**:
> "chưa khai vibe" vẫn nhẹ hơn "lệch vibe" đúng 4 lần. Cả hai trường hợp đều sinh `cautionFact` để AI diễn giải được lý do tụt hạng.

### 2.2 Lưới an toàn: loại theo ĐIỂM TỔNG

| Code | Default | Ý nghĩa |
|---|---|---|
| `MIN_SCORE_THRESHOLD` | `-1.00` | Điểm cuối dưới ngưỡng → loại (chỉ mode Rank). `-1.0` = không cắt vì điểm đã clamp về [−1,1] |

Bỏ lọc cứng nghĩa là **mọi** sản phẩm vào danh sách, kể cả điểm âm — với catalog nghèo, top 3–5 có thể toàn hàng dở mà vẫn được gợi ý tự tin. Ngưỡng này thay vai trò đó, nhưng loại vì **kết luận tổng hợp** chứ không vì một thuộc tính đơn lẻ: sản phẩm lệch vibe mà bù ngũ hành xuất sắc thì sống sót; sản phẩm khớp vibe mà ngũ hành sai bét thì bị loại.

Nâng lên `0.00` khi tắt kill-switch.

### 2.3 Kill-switch

| Code | Seed | Ý nghĩa |
|---|---|---|
| `VIBE_FILTER_HARD` | `1.00` | `≥ 0.5` → loại cứng (hành vi v3); `< 0.5` → trừ điểm mềm |

`scoring_params.value` là `decimal` nên cờ encode thành số — cùng cách v4 dùng `POLARITY_SHARE = 0` làm kill-switch. Merge với `1.0` → ranking **byte-identical** với trước, kể cả mode Fit (khi cờ còn bật, Fit vẫn chỉ ghi caution mà không trừ điểm, đúng như v3). Sau khi đối chiếu golden set, hạ `VIBE_FILTER_HARD` về `0` và nâng `MIN_SCORE_THRESHOLD` lên `0.0` — cả hai qua API `scoring-config`. Đây cũng là đường lui nếu ranking lệch ngoài ý muốn.

## 3. Không làm (out of scope)

- **Ma trận tương thích vibe 5×5.** `Focus` vs `Calm` gần nhau (đều tĩnh), `Focus` vs `Energize` lệch hẳn — phạt phẳng coi hai ca như nhau. Làm đúng cần bảng 25 ô phải seed và hiệu chỉnh; để giai đoạn 2, sau khi có số liệu thực từ penalty phẳng.
- **Mềm hóa bộ lọc khắc mệnh.** Nó cũng loại trước khi chấm (khi `Scope = Private`), nhưng khắc mệnh là ràng buộc phong thủy thật chứ không phải vấn đề dữ liệu thiếu — giữ cứng. Nếu sau này muốn mềm nốt thì `MIN_SCORE_THRESHOLD` đã sẵn sàng gánh vai lưới an toàn.
- **Thêm nhóm vibe mới** (cầu tài/bình an/sức khỏe/thi cử) — xem `product-placement-personal-recommendation.md` §7.

## 4. File thay đổi

- `Engine/ScoringModels.cs` — 4 `ScoringParamCodes` + 4 property; `PlacementPolicy.FilterByPurposeVibe` đổi tên `UsePurposeVibe` (giờ nghĩa là "luật vibe theo mục đích phòng có áp cho placement này không", không còn hàm ý loại cứng).
- `Engine/RecommendationScorer.cs` — bước 2a phân biệt unknown/mismatch + kill-switch; thêm cắt ngưỡng sau khi tính điểm cuối.
- `seed-data/scoring-params.json` — 4 row mới (`ScoringParamSeeder` idempotent theo code nên DB hiện có chạy lại seed sẽ nhận thêm, không đụng giá trị cũ).

## 5. Test cases tối thiểu

1. `VIBE_FILTER_HARD = 1.0` → ranking byte-identical với trước thay đổi (golden set).
2. `VIBE_FILTER_HARD = 0` + sp lệch vibe → **còn trong danh sách**, điểm giảm đúng `VIBE_MISMATCH_PENALTY`, có caution.
3. `VIBE_FILTER_HARD = 0` + sp **không có** vibe nào → giảm đúng `VIBE_UNKNOWN_PENALTY` (nhẹ hơn ca 2), caution khác nội dung.
4. Sp khớp vibe → `vibePenalty = 0`, không sinh caution vibe.
5. `MIN_SCORE_THRESHOLD = 0.0` → sp điểm âm biến mất khỏi Rank nhưng `GET /recommendations/fit` vẫn trả kết quả (Fit không cắt).
6. `WorkPurpose.Other` → `TargetVibe` null → không phạt, không loại, ở mọi giá trị cờ.
7. Placement `Carry` (`UsePurposeVibe = false`) → không bao giờ dính vibe penalty dù cờ bật hay tắt.
8. Thiếu 4 row `scoring_params` → engine chạy default trong code = loại cứng như v3 (`FromRows` giữ hành vi).
