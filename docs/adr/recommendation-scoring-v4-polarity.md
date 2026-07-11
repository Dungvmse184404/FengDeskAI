# ARD — Recommendation Scoring v4: Âm/Dương (Polarity)

> **Status:** Proposal — đã review lần 2 (2026-07-11): sửa nguồn functionPolarity (§2.2), rule polarity-unknown khi khắc (§3.2), thêm rollout kill-switch (§3.4) và chống lệch pha radar (§3.5).
> **Tiền đề:** engine v3 (`recommendation-scoring-v3.md`) đã chạy: mọi thực thể quy về `ElementVector`, điểm = gap-matching. v4 **không đổi** kiến trúc đó — chỉ thêm **1 trục scalar** `Polarity` chạy song song.

---

## 1. Khái niệm

Ngũ hành × Âm dương = 10 Thiên Can (Giáp = Dương Mộc, Ất = Âm Mộc…). Với hệ thống ta tách làm 2 trục độc lập:

| Trục | Kiểu | Người | Workspace | Product |
|---|---|---|---|---|
| Hành | `ElementVector` (đã có) | Nạp Âm | inputs → vector | inputs → vector |
| **Polarity** | **`decimal` ∈ [-1, +1]** (mới) | Can năm sinh (nhị phân ±1) | 40% công năng + 60% môi trường | suy từ inputs sẵn có |

`-1` = thuần Âm (tĩnh), `+1` = thuần Dương (động), `0` = cân bằng. Dùng số liên tục (không enum) vì workspace/product được tính từ nhiều tín hiệu có trọng số — tránh vỡ ở ranh giới.

**Nguyên tắc UX giữ nguyên v3:** không ai phải điền "Âm hay Dương". Vendor/customer chỉ khai tín hiệu quan sát được (màu, vật liệu, ánh sáng, tiếng ồn) — engine tự quy đổi.

## 2. Cách tính polarity từng thực thể

### 2.1 Người — miễn phí từ Can

Can dương (Giáp/Bính/Mậu/Canh/Nhâm) → `+1`; Can âm → `-1`. Với mapping stem hiện có trong `GetNapAmElement` (0=Canh…9=Kỷ): **stem chẵn = Dương**.

```csharp
// FengShuiCalculator
public static decimal GetCanPolarity(int birthYear)
    => ((birthYear % 10) + 10) % 10 % 2 == 0 ? 1m : -1m;
```

### 2.2 Workspace — 40% công năng + 60% môi trường

```
currentPolarity = FUNCTION_POLARITY_SHARE × functionPolarity(WorkspaceType)
                + (1 − FUNCTION_POLARITY_SHARE) × avg(polarity của env inputs)
```

- `functionPolarity`: **cột mới `base_polarity numeric(4,3) default 0` trên bảng `workspace_types`** — KHÔNG dùng dictionary in-code vì `WorkspaceType` là bảng DB và **user tự thêm loại được** (map theo Name sẽ vỡ với loại tự tạo). Seed cho loại hệ thống (Meeting Room `+0.6`, Reception `+0.5`, Open Workspace `+0.2`, Personal Desk `-0.3`, Home Office `-0.2`…); loại user tự tạo giữ default `0` (trung tính — polarity phòng khi đó dựa hoàn toàn vào env inputs).
- Env inputs: thêm 2 `ElementInputKind` mới — `Lighting` (`BrightSun +0.7`, `WarmDim -0.5`, `Blackout -0.8`…), `Ambience` (`Noisy +0.6`, `ManyDevices +0.5`, `Quiet -0.6`…). Không có input → fallback `functionPolarity` 100%.

### 2.3 Product — không bắt vendor khai thêm gì

Tái dùng `product_element_inputs` sẵn có: thêm cột `polarity` vào `element_input_map`, mỗi tín hiệu đã mang sẵn nghĩa âm/dương (đèn LED/kim loại bóng/màu nóng → dương; gỗ tối/gốm/đá/màu trầm → âm; mặc định `0`). `productPolarity = avg(polarity các input)`, cache vào `products.polarity` cùng chỗ 5 cột `Element*`. Override thủ công đi theo `IsVectorOverridden` như vector.

## 3. Thay đổi công thức điểm (RecommendationScorer)

### 3.1 Polarity gap score — cùng triết lý "viên thuốc"

```
idealPolarity  = targetPolarity(Purpose)        // Focus/Calm −0.4 · Relax −0.2 · Creative +0.2 · Energize +0.5
polarityGap    = clamp(idealPolarity − currentPolarity, −1, +1)
polarityScore  = polarityGap × productPolarity   // ∈ [−1, +1]
```

Phòng quá dương + sản phẩm âm → dương điểm; phòng cân bằng → trục này trung tính (≈0). Blend:

```
score = (1 − POLARITY_SHARE) × gapScore + POLARITY_SHARE × polarityScore − userPenalty − dirPenalty
```

### 3.2 Khắc "hữu tình / vô tình" — tinh chỉnh bước 2b

Luật cổ: khắc **đồng tính** (cùng âm/cùng dương) nặng — "vô tình"; khắc **dị tính** nhẹ — "hữu tình". Khi `BiKhac`:

```csharp
// |productPolarity| < POLARITY_KNOWN_THRESHOLD (0.1) → polarity KHÔNG XÁC ĐỊNH
//   → giữ nguyên 100% hành vi v3 (hard-filter + penalty như cũ). KHÔNG coi là dị tính!
// Xác định + cùng dấu với personalPolarity (đồng tính):
//   → giữ hard-filter (Private + Rank); penalty × KHAC_SAME_POLARITY (1.2)
// Xác định + trái dấu (dị tính):
//   → không hard-filter, chỉ soft; penalty × KHAC_DIFF_POLARITY (0.6)
```

→ v4 chỉ **nới** hard-filter cho sản phẩm *đã biết* polarity và dị tính. Lý do rule threshold: phần lớn catalog ban đầu chưa có polarity (= 0); nếu coi 0 là "dị tính" thì toàn bộ catalog cũ mặc nhiên thoát hard-filter → v4 vô tình vô hiệu hóa lớp bảo vệ khắc mệnh của v3. Ghi caution fact tương ứng để AI diễn giải.

### 3.3 Tham số mới (`scoring_params`, default trong code)

| Code | Default trong code | Seed ban đầu |
|---|---|---|
| `POLARITY_SHARE` | `0.20` | **`0.00` — kill-switch, xem §3.4** |
| `FUNCTION_POLARITY_SHARE` | `0.40` | `0.40` |
| `KHAC_SAME_POLARITY` | `1.20` | `1.20` |
| `KHAC_DIFF_POLARITY` | `0.60` | `0.60` |
| `POLARITY_KNOWN_THRESHOLD` | `0.10` | `0.10` |

### 3.4 Rollout — kill-switch bằng chính scoring_params

Seed `POLARITY_SHARE = 0` khi merge: toàn bộ code v4 nằm im (polarityScore × 0, và quy ước thêm — share = 0 thì tắt luôn modifier khắc §3.2). Sau khi seed polarity được hiệu chỉnh bằng **golden set** (~10 sản phẩm × 3 phòng mẫu, chấm tay và so thứ hạng trước/sau), admin nâng lên `0.2` qua API `scoring-config` sẵn có — bật/tắt không cần deploy. Đây cũng là đường lui nếu ranking lệch ngoài ý muốn.

### 3.5 Chống lệch pha radar ↔ recommendation

Nguy cơ: score có thành phần polarity mà radar chỉ vẽ 5 hành → user/QA thấy sản phẩm "bù gap kém hơn lại xếp trên" mà không có gì trên màn hình giải thích. Ba chốt bắt buộc:

1. **Một nguồn số duy nhất:** `CurrentPolarity/IdealPolarity/PolarityGap` chỉ được tính trong `WorkspaceElementAnalyzer` — cả endpoint element-analysis lẫn `RecommendationService` đọc từ đó (đúng pattern Gap hiện tại). Cấm tính lại polarity inline ở service.
2. **UI đi cùng đợt bật:** thanh `PolarityBar` (Âm ↔ Dương, chấm current + vạch ideal) phải lên FE **trước hoặc cùng lúc** admin nâng `POLARITY_SHARE > 0` — không bật scoring khi UI chưa hiển thị trục này. Radar 5 hành giữ nguyên, tuyệt đối không thêm polarity làm trục thứ 6 (khác đơn vị: share 0..1 Σ=1 vs tọa độ −1..+1).
3. **Facts giải thích chênh lệch:** mọi lượt polarityScore có |giá trị| ≥ 0.05 phải sinh match/caution fact ("mang tính tĩnh, làm dịu không gian đang quá động") — user đọc được lý do xếp hạng ngay cả khi không nhìn PolarityBar. `ProductFitResponse` thêm `productPolarity`, `currentPolarity`, `idealPolarity` cho trang Fit.

## 4. Danh sách file thay đổi

### Domain
- `Enums/Workspace/ElementInputKind.cs` — thêm `Lighting`, `Ambience` (enum lưu string → thêm an toàn, không cần migrate data cũ).
- `Entities/Recommendation/ElementInputMap.cs` — thêm `public decimal Polarity { get; set; } = 0m;`.
- `Entities/Catalog/Product.cs` — thêm `public decimal? Polarity { get; set; }` (cache, cạnh `ElementTho…`).

### Application (Engine)
- `FengShuiCalculator.cs` — thêm `GetCanPolarity`, map `PurposeTargetPolarity` (WorkPurpose là enum → in-code OK).
- `Entities/Workspace/WorkspaceType.cs` — thêm `BasePolarity` (+ config + seeder + migration — xem §2.2).
- `ScoringModels.cs` — `ScoringParamCodes` + `ScoringParameters` thêm 4 param; `ScoringContext` thêm `PersonalPolarity`, `CurrentPolarity`, `IdealPolarity`; `ProductFacts` thêm `Polarity`.
- `ElementVectorBuilders.cs` — `ElementInputResolver` đọc thêm polarity; thêm `PolarityBuilder` (2 hàm: `BuildWorkspacePolarity`, `BuildProductPolarity`).
- `RecommendationScorer.cs` — §3.1 blend + §3.2 modifier; thêm fact/caution mô tả cân bằng âm dương.
- `WorkspaceElementAnalyzer.cs` + DTO analysis — expose `currentPolarity` cho FE hiển thị.

### Infrastructure
- `Configurations/ElementInputMapConfiguration.cs`, `ProductConfiguration` — cột mới `numeric(4,3)`.
- Migration `RecommendationScoringV4Polarity` — 3 cột (`element_input_map.polarity`, `products.polarity`, `workspace_types.base_polarity`) + backfill `0`.
- Lưu ý cache: admin sửa `polarity` trong `element_input_map` KHÔNG tự tính lại `products.polarity` đã cache (giới hạn sẵn có của cache vector — chấp nhận, ghi vào docs admin; endpoint recompute-all để backlog).
- `Seeding/ElementInputMapSeeder.cs` — gán polarity cho code hiện có + seed code `Lighting`/`Ambience`; `ScoringParamSeeder.cs` — 4 row mới.

### DTO / FE (giai đoạn 2, tách PR)
- `WorkspaceProfileResponse`, `ProductFengShuiResponse` + FE types: thêm `polarity`.
- Form workspace (FE): 2 câu hỏi chọn nhanh (ánh sáng? độ ồn/thiết bị?) → ghi vào `workspace_profile_inputs` với kind mới. Vendor form: **không đổi**.
- `ElementVectorFit` UI: thêm 1 thanh Âm ↔ Dương.

## 5. Không làm (out of scope v4)

- Âm dương theo Can **ngày** sinh (Bát Tự đầy đủ) — chỉ dùng Can năm.
- Polarity theo giờ trong ngày / mùa.
- Endpoint recompute-all cache polarity khi admin sửa map (chấp nhận stale, xem §Infrastructure).
- Polarity làm trục radar (sai đơn vị — chỉ vẽ PolarityBar riêng, xem §3.5).

## 6. Test cases tối thiểu

1. `GetCanPolarity(1990) == +1` (Canh — dương), `GetCanPolarity(1991) == -1` (Tân — âm).
2. Phòng ngủ không env input → `currentPolarity == functionPolarity == -0.7`.
3. Phòng họp sáng gắt (`+0.6`, `+0.7`) → currentPolarity `= 0.4×0.6 + 0.6×0.7 = 0.66`; Purpose Calm (ideal `-0.4`) → gap `-1` (clamp) → sản phẩm âm (`-0.5`) được `+0.5` polarityScore.
4. BiKhac đồng tính + Private + Rank → vẫn bị loại; BiKhac dị tính (|polarity| ≥ 0.1) → còn trong list với penalty `0.6 × USER_CONFLICT_PENALTY`.
5. **BiKhac + product polarity = 0 → hành vi GIỐNG HỆT v3** (hard-filter còn nguyên) — chốt chống vô hiệu hóa lọc khắc.
6. `POLARITY_SHARE = 0` → toàn bộ ranking byte-identical với v3 (golden set so sánh) — chốt kill-switch.
7. Catalog toàn sản phẩm polarity = 0, share = 0.2 → thứ hạng tương đối giữ nguyên v3 (mọi gapScore cùng nhân 0.8).
8. Loại phòng user tự tạo (base_polarity = 0), không env input → currentPolarity = 0, polarityScore = 0 — không nổ NPE.
9. Thiếu row `scoring_params` mới → engine chạy với default (`FromRows` giữ nguyên hành vi).
10. Endpoint element-analysis và recommendation trả cùng `currentPolarity` cho cùng profile (chống lệch pha, cùng nguồn Analyzer).
