# ARD — Recommendation Scoring v4: Âm/Dương (Polarity)

> **Status:** Proposal — spec trước khi code.
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

- `functionPolarity`: map tĩnh theo loại phòng (phòng họp/sảnh `+0.6`, bếp `+0.4`, phòng làm việc chung `+0.2`, phòng học cá nhân `-0.4`, phòng ngủ `-0.7`…). Để trong code cạnh `DirectionElements` (dictionary, unit-test được); chuyển DB sau nếu cần admin sửa.
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
bool samePolarity = personalPolarity * Math.Sign(productPolarity) > 0; // product 0 → coi là dị tính
// Đồng tính:  giữ hard-filter (Private + Rank) như v3; penalty × KHAC_SAME_POLARITY (1.2)
// Dị tính:    KHÔNG hard-filter nữa, chỉ soft; penalty × KHAC_DIFF_POLARITY (0.6)
```

→ v4 **nới** hard-filter của v3: chỉ loại candidate khi khắc đồng tính. Ghi rõ vào caution fact để AI diễn giải.

### 3.3 Tham số mới (`scoring_params`, default trong code)

| Code | Default |
|---|---|
| `POLARITY_SHARE` | `0.20` |
| `FUNCTION_POLARITY_SHARE` | `0.40` |
| `KHAC_SAME_POLARITY` | `1.20` |
| `KHAC_DIFF_POLARITY` | `0.60` |

## 4. Danh sách file thay đổi

### Domain
- `Enums/Workspace/ElementInputKind.cs` — thêm `Lighting`, `Ambience` (enum lưu string → thêm an toàn, không cần migrate data cũ).
- `Entities/Recommendation/ElementInputMap.cs` — thêm `public decimal Polarity { get; set; } = 0m;`.
- `Entities/Catalog/Product.cs` — thêm `public decimal? Polarity { get; set; }` (cache, cạnh `ElementTho…`).

### Application (Engine)
- `FengShuiCalculator.cs` — thêm `GetCanPolarity`, dictionary `WorkspaceTypePolarity`, map `PurposeTargetPolarity`.
- `ScoringModels.cs` — `ScoringParamCodes` + `ScoringParameters` thêm 4 param; `ScoringContext` thêm `PersonalPolarity`, `CurrentPolarity`, `IdealPolarity`; `ProductFacts` thêm `Polarity`.
- `ElementVectorBuilders.cs` — `ElementInputResolver` đọc thêm polarity; thêm `PolarityBuilder` (2 hàm: `BuildWorkspacePolarity`, `BuildProductPolarity`).
- `RecommendationScorer.cs` — §3.1 blend + §3.2 modifier; thêm fact/caution mô tả cân bằng âm dương.
- `WorkspaceElementAnalyzer.cs` + DTO analysis — expose `currentPolarity` cho FE hiển thị.

### Infrastructure
- `Configurations/ElementInputMapConfiguration.cs`, `ProductConfiguration` — cột mới `numeric(4,3)`.
- Migration `RecommendationScoringV4Polarity` — 2 cột + backfill `polarity = 0`.
- `Seeding/ElementInputMapSeeder.cs` — gán polarity cho code hiện có + seed code `Lighting`/`Ambience`; `ScoringParamSeeder.cs` — 4 row mới.

### DTO / FE (giai đoạn 2, tách PR)
- `WorkspaceProfileResponse`, `ProductFengShuiResponse` + FE types: thêm `polarity`.
- Form workspace (FE): 2 câu hỏi chọn nhanh (ánh sáng? độ ồn/thiết bị?) → ghi vào `workspace_profile_inputs` với kind mới. Vendor form: **không đổi**.
- `ElementVectorFit` UI: thêm 1 thanh Âm ↔ Dương.

## 5. Không làm (out of scope v4)

- Âm dương theo Can **ngày** sinh (Bát Tự đầy đủ) — chỉ dùng Can năm.
- Polarity theo giờ trong ngày / mùa.
- Admin UI chỉnh `functionPolarity` (đang hard-code, chuyển DB khi có nhu cầu).

## 6. Test cases tối thiểu

1. `GetCanPolarity(1990) == +1` (Canh — dương), `GetCanPolarity(1991) == -1` (Tân — âm).
2. Phòng ngủ không env input → `currentPolarity == functionPolarity == -0.7`.
3. Phòng họp sáng gắt (`+0.6`, `+0.7`) → currentPolarity `= 0.4×0.6 + 0.6×0.7 = 0.66`; Purpose Calm (ideal `-0.4`) → gap `-1` (clamp) → sản phẩm âm (`-0.5`) được `+0.5` polarityScore.
4. BiKhac đồng tính + Private + Rank → vẫn bị loại; BiKhac dị tính → còn trong list với penalty `0.6 × USER_CONFLICT_PENALTY`.
5. Thiếu row `scoring_params` mới → engine chạy với default (`FromRows` giữ nguyên hành vi).
