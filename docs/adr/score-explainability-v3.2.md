# ADR — Giải thích điểm số (Score Explainability), Chuẩn hoá thang điểm & Yếu tố nghề nghiệp

> **Trạng thái:** **ACCEPTED** rev.5 — 13/13 quyết định đã chốt 08/09/2026 (bảng ở PHẦN E)
> **Đã xong:** 12 doc liên quan + bộ test `RecommendationScorerTests.cs`. **Chưa xong:** code engine (P1).
> **Vị trí đề xuất:** `docs/adr/score-explainability-v3.2.md`
> **Tiền đề:** `docs/adr/personalized-recommendation-v3.1.md` · `docs/adr/recommendation-scoring-v3.md` · `docs/adr/vibe-soft-scoring.md`

---

## Nhật ký xác minh

### Bản nháp đầu SAI ở đâu

| # | Nháp nói | Sự thật trong code | Xử lý |
|:-:|---|---|---|
| ❌1 | Migration `PersonalizedRecommendationV31` chưa tạo | **Đã có** `20260830102642_…` + 3 migration mới hơn (03–04/09) | Xoá P0 build/migration |
| ❌2 | Cần `UPDATE … WHERE placement='Architectural'` | Đã dọn sạch | Xoá |
| ❌3 | `work_purpose_modifiers` **nhân hệ số** | `ApplyIntent` **CỘNG delta** (`Add(Single(e).Scale(m.Delta))`) rồi normalize | Đổi thiết kế nghề nghiệp sang delta |
| ❌4 | `ProductVectorProvider` có 3 tầng | Có **4** — thiếu tầng 1.5 `DecorItem` | Bổ sung |
| ✅5 | `GapElementRow.Ideal` là ideal thô? | Là **`adjustedIdeal`** (`BuildGap(adjustedIdeal, current)`) | Đổi tên field |

### Phát hiện mới

| # | Phát hiện | Hệ quả |
|:-:|---|---|
| 🔴 **A** | **FE không gọi `POST /recommendations` ở đâu cả.** Màn hình duy nhất hiện điểm là `ProductFitPanel` → `GET /recommendations/fit` | Mọi việc nhắm vào `ProductFitResponse` trước |
| 🔴 **B** | `gapScore` bị **trần toán học ±0.5** ⇒ tier "Rất hợp" (`≥0.6`) **bất khả thi** | Gốc của "màn hình không phân biệt gì" — §10 |
| 🔴 **C** | **`personalScore` chỉ dùng `personalVector.Dominant()`** — toàn bộ hình dạng vector bị vứt bỏ | Đổi thiết kế radar — §11 · `SELF/SUPPORT/CHILD_SHARE` là **tham số chết** |
| 🟡 **D** | Đã có precedent explainability hoàn chỉnh cho vector `Current` (`Contributions[] + SharePercent + Confidence`, FE đã có tooltip nguồn) | Sao chép pattern, không phát minh |
| 🔴 **E** | `workspace-types.json`: **20/24 phòng là `Private`** (gồm Bếp, Phòng khách, Phòng ăn, Home Theater, Guest Room, Phòng thờ) | Bật `Wp` lúc này = neo phòng chung vào bản mệnh một người |

---

# PHẦN A — HIỆN TRẠNG

## 1. Đường đi của con số

```
   Loại phòng ──► BuildIdeal (rows Source="Ideal", normalize)
                     │
   Mục đích  ──► ApplyIntent: ideal + Σ Single(e)·delta → Normalize()   ◄── CỘNG DELTA
                     └──► adjustedIdeal

   Hiện trạng ─► BuildCurrentBreakdown — mô hình "PHIẾU" (Dirichlet prior)
                   ① nền phòng Interior × 3 phiếu
                   ② mỗi tag user khai ≈ 1 phiếu
                   ③ mỗi sản phẩm đã đặt × voteWeight
                   → cộng thô theo phiếu → Normalize() một lần
                   └──► currentVector  (+ Contributions[] để giải thích)

                gap = adjustedIdeal − currentVector

   Ngày sinh ──► GetLunarYear → Nạp Âm → BuildPersonalVector (SELF .60/SUPPORT .30/CHILD .10)
                   └──► personalVector   ⚠️ engine CHỈ dùng .Dominant() — xem §5

   Sản phẩm ──► ProductVectorProvider.Build — 4 TẦNG:
                   ① IsVectorOverridden + đủ 5 cột       → dùng thẳng
                   ①·5 có input kind DecorItem           → contributions của code đó  ★
                   ② có product_element_inputs           → Material×0.60 + (Color∪Shape)×0.40
                   ③ fallback product_elements           → primary 0.70 / secondary 0.30
                   └──► productVector

            ScoreOne ──► score ∈ [−1,1] ──► FE scorePercent() ──► SỐ TRÊN MÀN HÌNH
```

## 2. Công thức

```
score = round( clamp( blended − userPenalty − dirPenalty − vibePenalty , −1, 1) , 3)
blended       = (1 − Wp)·gapScore + Wp·personalScore      ← chỉ nhánh WorkspaceGap
gapScore      = target · productVector / |target|₁
personalScore = Σ productVector[e] × ruleScore(mệnhDominant, e)      clamp [−1,1]
```

| Thành phần | Miền HIỆN TẠI | Nguồn |
|---|---:|---|
| `gapScore` | **[−0.5, +0.5]** ⚠️ | gap phòng · vector SP |
| `personalScore` | [−1.0, +1.0] | `feng_shui_rules` (25 dòng, có dấu) |
| `Wp` | [0, 1] | `scoring_params` theo `WorkspaceScope`; **=0 nếu user chưa có `DateOfBirth`** |
| `userPenalty` | 0 / 0.30 | chỉ khi trục cá nhân TẮT |
| `dirPenalty` | 0 / 0.15 | hướng cửa ∪ WC ∪ góc tối |
| `vibePenalty` | 0 / 0.05 / 0.20 | mục đích phòng vs vibe SP |

## 3. 🔴 B — Trần ±0.5 và hệ quả

`adjustedIdeal` và `current` đều Σ=1 ⇒ `Σ gap = 0` ⇒ tổng phần dương = tổng phần âm = `P`
⇒ `|gap|₁ = 2P`. Còn `gap · productVector` là **trung bình có trọng số** của các `gap[e]`
(vì `productVector` ≥ 0, Σ=1) nên `≤ max gap[e] ≤ P`.

```
gapScore ≤ P / 2P = 0.5          (đối xứng: ≥ −0.5)
```

**Trần này do MẪU SỐ, không do dữ liệu**: `|gap|₁` đếm cả hai nửa (thiếu + thừa), tử số chỉ với tới một nửa.

| Phòng thiếu | gap dương | SP tốt nhất | `gapScore` | Màn hình |
|---|---|---|---:|---:|
| **1 hành** (cực đoan) | Mộc +1.0 | thuần Mộc | 0.500 | 75% |
| **2 hành** (fixture test) | Mộc +0.6, Thủy +0.4 | thuần Mộc | 0.300 | 65% |
| **3 hành** (thực tế) | +0.4/+0.35/+0.25 | thuần hành thiếu nhất | 0.200 | 60% |

FE `tierFor(score)`: `≥0.6` Rất hợp · `≥0.2` Phù hợp · `≥−0.2` Trung tính · còn lại Cân nhắc.

```
      0%       25%      38%   50%   65%    75%              100%
      ├────────┼────────┼─────┼─────┼──────┼─────────────────┤
Wp=0           └──── DẢI THỰC TẾ ────┘     ▲ trần toán học
                                            "Rất hợp" cần ≥80% ──► KHÔNG BAO GIỜ TỚI
```

## 4. 🔴 E — `WorkspaceScope` seed sai

`seed-data/workspace-types.json`: **Private 20 · Shared 3 · Public 1**

| Phòng | Hiện tại | Nên là |
|---|---|---|
| Kitchen · Living Room · Dining Room · Home Theater · Guest Room · Altar Room | `Private` | **`Shared`** |
| Bathroom · Laundry · Garage · Balcony · Rooftop Garden | `Private` | ✅ **chốt `Private`** (Q6) |
| Personal Desk · Home Office · Private Office · Bedroom · Study · Kids Room · Walk-in Closet · Meditation | `Private` | ✅ |
| Meeting Room · Co-working Booth · Open Workspace | `Shared` | ✅ |
| Reception / Lounge | `Public` | ✅ |

Sửa **trong file seed**, không phải SQL tay.

> ### 🔴 Đính chính (phát hiện khi làm P0.1) — sửa file seed thôi thì **KHÔNG ĐỦ**
> Bản nháp viết *"seeder idempotent theo tên"* và coi thế là xong. Nhưng `WorkspaceTypeSeeder`
> idempotent theo nghĩa **bỏ qua** tên đã có, không phải **đồng bộ** nó:
> ```csharp
> var toAdd = file.Rows.Where(t => !existingNames.Contains(t.Name)).ToList();   // chỉ INSERT tên mới
> ```
> ⇒ Trên mọi DB đã seed, 6 phòng kia vẫn `Private` dù JSON đã đổi. Và **không có endpoint admin nào
> sửa được `WorkspaceType.Scope`** (`WorkspaceTypesController` chỉ có `GET` + `POST` tạo loại của
> user) ⇒ không còn đường nào khác ngoài SQL tay — đúng thứ mục này muốn tránh.
>
> **Xử lý:** seeder nay **đồng bộ `Scope`** cho row `IsSystemSeeded` đã tồn tại, ngoài việc chèn tên
> mới. Chỉ đồng bộ `Scope` — `Name`/`Description`/`PersonalWeight` giữ nguyên, và loại do user tự tạo
> (`IsSystemSeeded = false`) không bị chạm tới.

## 5. 🔴 C — `personalVector` không phải thứ tạo ra điểm

```csharp
// RecommendationScorer.ScoreOne — bước 2d
if (personalBlend && policy.Target == ScoringTarget.WorkspaceGap && ctx.PersonalVector is { } personalVec)
{
    var personalDominant = personalVec.Dominant();          // ◄── CHỈ LẤY ĐỈNH
    decimal personalScore = PersonalAffinity(ctx, personalDominant, product.Vector);
}

private static decimal PersonalAffinity(ScoringContext ctx, FengShuiElement personalDominant, ElementVector productVector)
{
    foreach (var (element, weight) in productVector.Enumerate())
        sum += weight * ctx.RuleScoreOf(personalDominant, element);   // ◄── ruleScore, KHÔNG phải personalVector
    return Math.Clamp(sum, -1m, 1m);
}
```

`BuildPersonalVector` = `self×0.60 + supporter×0.30 + child×0.10` rồi normalize.
Vì `0.60 > 0.30 > 0.10`, **`Dominant()` LUÔN = hành Nạp Âm**. Suy ra:

1. **`SELF_SHARE` / `SUPPORT_SHARE` / `CHILD_SHARE` là tham số CHẾT** ở luồng workspace — chỉnh chúng không đổi một điểm nào (bước 2b lọc khắc mệnh cũng chỉ dùng `Dominant()`).
2. **Vẽ `personalVector` lên radar là vẽ một hình không tạo ra điểm.** Thứ nhân với `productVector` là **vector điểm quan hệ** `r[e] = ruleScore(mệnh, e)`.

Với mệnh **Mộc**, `r` = `{ Mộc +1.0, Thủy +0.8, Thổ +0.2, Hỏa −0.2, Kim −1.0 }`.

> **Đây KHÔNG phải bug.** Ngữ nghĩa `ruleScore(subject, object)` yêu cầu `subject` là **một** hành bản
> mệnh. Trộn cả vector vào sẽ pha loãng (thuần Mộc rơi từ +1.0 xuống +0.62) và làm trục cá nhân yếu đi
> — đúng ngược mục tiêu. **Giữ `Dominant()`**; cái cần sửa là *tài liệu* và *thiết kế radar*.

## 6. API đang trả gì

### `GET /api/recommendations/fit` → `ProductFitResponse` ← **màn hình đang dùng**

| Có | Không có |
|---|---|
| `score` · `matchFacts[]` · `cautionFacts[]` · `placementHint` | ❌ `gapScore`, `personalScore`, `Wp` |
| `gap: List<ElementAnalysisRow>` (6 cột) | ❌ từng penalty riêng |
| `productVector: List<ProductElementRow>` | ❌ vector cá nhân / vector điểm quan hệ |
| | ❌ `Contributions[]` (có ở element-analysis) |

### `POST /api/recommendations` → `RecommendationResponse` ← FE chưa dùng
`Gap` là `GapBreakdownResponse` **3 cột** (lệch với `ElementAnalysisRow` 6 cột) · `Items[]` không có vector/breakdown · `PersonalWeight` là **cột LEGACY v2**.

### Engine
```csharp
public sealed record ScoredProduct(Guid ProductId, decimal Score,
    IReadOnlyList<string> MatchFacts, IReadOnlyList<string> CautionFacts, string? PlacementHint);
```
Mọi biến trung gian là **cục bộ trong `ScoreOne`** ⇒ phải sửa **từ engine trở ra**.

## 7. Bẫy còn nguyên

| # | Bẫy |
|:-:|---|
| 7.1 | `RecommendationResponse.PersonalWeight` = `WorkspaceType.PersonalWeight` (**LEGACY v2**, mặc định 1.0). `Wp` v3.1 chỉ vào `ScoringContext`, không bao giờ ra API. ✅ FE **chưa** đọc field này |
| 7.2 | ~~`PERSONAL_WEIGHT_*` seed **0.00**~~ → **đã nâng lên 0.50/0.30/0.00 cho khớp default code**. Lệch hai chỗ gây bẫy ngược đời: DB **thiếu** row thì rơi về default code (0.50, trục cá nhân BẬT), seed xong lại thành 0.00 (TẮT) — tính năng biến mất đúng lúc vừa seed. `SCORE-PARAM-01` nay khoá cả nhóm này. Vẫn giữ: `Wp = 0` bắt buộc khi user chưa có `DateOfBirth` |
| 7.3 | `WorkspaceType.IsPublic` + `PersonalWeight` là 2 cột legacy chưa dọn (còn cả trong FE type) |
| 7.4 | `GeneratePersonalAsync` dựng `ScoringContext` **thiếu** `Aspiration` · `AspirationDirections` · `RuleScores` |
| 7.5 | `VIBE_FILTER_HARD = 1.00` ⇒ SP lệch vibe **bị loại trước khi tới điểm** — không xuất hiện để mà giải thích |

---

# PHẦN B — YÊU CẦU

| Mã | Yêu cầu |
|---|---|
| **R1** | Giải thích chi tiết **thành phần nào tạo ra con số** hiển thị |
| **R2** | Radar hiển thị thêm **trục cá nhân** khi phòng có `Wp > 0` |
| **R3** | Sản phẩm **`Carry`** cũng có radar, nhưng **chỉ có hành của người** (không có phòng) |
| **R4** | **Sửa tận gốc** thang điểm — không chữa triệu chứng ở FE |
| **R5** | Đưa **nghề nghiệp người dùng** vào tính điểm (chưa tồn tại gì) |

---

# PHẦN C — THIẾT KẾ

## 8. R4 — Chuẩn hoá `gapScore` về ±1.0 *(sửa tận gốc)*

### 8.1 Thay đổi

```csharp
// TRƯỚC — mẫu số đếm cả hai nửa của gap ⇒ trần ±0.5
decimal gapScore = targetL1 == 0m ? 0m : target.Dot(product.Vector) / targetL1;

// SAU — chỉ nhánh WorkspaceGap chia đôi; nhánh PersonalNeed GIỮ NGUYÊN
decimal denom = policy.Target == ScoringTarget.PersonalNeed ? targetL1 : targetL1 / 2m;
decimal gapScore = denom == 0m ? 0m : Math.Clamp(target.Dot(product.Vector) / denom, -1m, 1m);
```

> ### 🔴 Đính chính (phát hiện khi viết test) — phép chia đôi phải **theo nhánh**
> Bản nháp đầu của §8.1 chia đôi cho **mọi** nhánh. **Sai với `Carry`.**
>
> | Nhánh | `target` | Tính chất | Mẫu số đúng |
> |---|---|---|---|
> | `WorkspaceGap` | `gap = adjustedIdeal − current` | **Σ = 0**, có âm ⇒ nửa dương = `\|gap\|₁/2` | **`\|gap\|₁ / 2`** |
> | `PersonalNeed` | `personalNeedVector` | **Σ = 1**, không âm ⇒ **không có "hai nửa"** | **`\|target\|₁`** (= 1) |
>
> Nếu chia đôi cả nhánh `PersonalNeed`: sản phẩm khớp hoàn hảo cho ra `1.0 / 0.5 = 2.0` → clamp về 1.0,
> và **mọi** sản phẩm khớp từ 50% trở lên đều bão hoà ở 1.000 ⇒ luồng `Carry` mất hết khả năng xếp hạng.
> Ca `SCORE-A4-04` chốt hành vi này.

> **Không thêm kill-switch.** Giữ hai nhánh công thức sẽ nhân đôi ma trận test mà không đổi hành vi
> mong muốn — cùng lý do đã ghi trong `ProductPlacement.cs` về việc không thêm lại enum suông.
> Thay vào đó **đóng dấu phiên bản** (§8.4) để dữ liệu cũ không bị so nhầm.

### 8.2 Vì sao đúng về toán

Sau khi sửa, **cả hai thành phần đều là tích vô hướng của `productVector` với một "vector hướng"**:

```
ĝ = gap / (|gap|₁ / 2)                     entries ∈ [−1, +1]
r[e] = ruleScore(mệnhDominant, e)          entries ∈ [−1, +1]

score = (1−Wp)·(ĝ·p) + Wp·(r·p) = p · [ (1−Wp)·ĝ + Wp·r ]
                                        └────────┬────────┘
                                          combinedDirection d
```

⇒ **`score = productVector · d` — chính xác, không xấp xỉ.** `d` là vector 5 hành, mỗi trục ∈ [−1,+1],
và đó chính là thứ radar cần vẽ (§11).

### 8.3 Penalty phải nhân đôi theo

Penalty là **hằng số tuyệt đối**, nên khi miền điểm nở gấp đôi thì sức nặng tương đối của chúng **giảm một nửa**.
Giữ tỉ lệ ⇒ nhân đôi:

| Mã | Cũ | **Mới** | Tỉ lệ so với biên độ |
|---|---:|---:|---|
| `DIRECTION_PENALTY` | 0.15 | **0.30** | giữ 30% |
| `USER_CONFLICT_PENALTY` | 0.30 | **0.60** | giữ 60% |
| `VIBE_MISMATCH_PENALTY` | 0.20 | **0.40** | giữ 40% |
| `VIBE_UNKNOWN_PENALTY` | 0.05 | **0.10** | giữ 10% |
| `MIN_SCORE_THRESHOLD` | −1.00 | −1.00 | vẫn = không cắt |

> ### 🔴 Đính chính — sửa **hai chỗ**, và là **một thay đổi nguyên khối** với §8.1
> **(1) Hai chỗ.** `ScoringParameters.FromRows` chỉ đọc row có trong DB; **code lạ bị bỏ qua, code
> thiếu giữ default trong code**. Sửa mỗi `seed-data/scoring-params.json` thì môi trường nào thiếu row
> sẽ chấm bằng giá trị CŨ. ⇒ Phải sửa cả `ScoringParameters` default. Ca `SCORE-PARAM-01` khoá việc
> hai chỗ không được lệch nhau.
>
> **(2) Không phải "không cần deploy".** Bản nháp viết vậy vì tưởng đây là tinh chỉnh tham số độc lập.
> Thực ra penalty ×2 chỉ đúng **sau khi** §8.1 đã nở miền điểm. Nếu seed ×2 lên trước lúc code còn ở
> thang ±0.5 thì: `DIRECTION_PENALTY = 0.30` **bằng 60% toàn bộ biên độ**, `USER_CONFLICT_PENALTY =
> 0.60` **lớn hơn cả biên độ** ⇒ mọi SP khắc mệnh ở `Shared`/`Public` rơi thẳng đáy.
> ⇒ **§8.1 + §8.3 lên cùng một lần**, không tách.

### 8.4 Ảnh hưởng

| Chỗ | Ảnh hưởng |
|---|---|
| `recommendation_items.score` **đã lưu** | Không so sánh được với điểm mới ⇒ thêm `recommendations.formula_version` (`"3.1"` / `"3.2"`), FE ẩn so sánh chéo phiên bản |
| 37 ca unit test | Toàn bộ kỳ vọng nhóm A1/A2/A3/A6 đổi; bất biến A3-06 đổi từ `[−0.5, 0.5]` → `[−1, 1]` |
| Golden set | Phải dựng lại baseline |
| `tierFor` ở FE | **Giữ nguyên ngưỡng 0.6/0.2/−0.2** — giờ đã đạt được |

### 8.5 Kết quả — fixture test (gap: Mộc +0.6, Thủy +0.4, Kim −0.5, Thổ −0.5; mệnh Mộc)

| SP | `gapScore` cũ | **`gapScore` mới** | `Wp=0` màn hình | `Wp=0.5` score | màn hình | tier |
|---|---:|---:|---:|---:|---:|---|
| **Mộc** | 0.300 | **0.600** | **80% Rất hợp** ✅ | **+0.800** | **90%** | Rất hợp |
| **Thủy** | 0.200 | 0.400 | 70% Phù hợp | +0.600 | 80% | Rất hợp |
| **Hỏa** | 0.000 | 0.000 | 50% Trung tính | −0.100 | 45% | Trung tính |
| **Thổ** | −0.250 | −0.500 | 25% Cân nhắc | −0.150 | 43% | Trung tính |
| **Kim** | −0.250 | −0.500 | 25% Cân nhắc | −0.750 | 13% | Cân nhắc |
| **Độ rộng dải** | 27 điểm % | | **55 điểm %** | | **77 điểm %** | |

> Hai cách sửa **độc lập và cộng dồn**: ×2 một mình đã mở khoá tier "Rất hợp"; bật `Wp` mới làm
> **hai người khác mệnh thấy thứ hạng khác nhau**.

## 9. `ScoreBreakdown` — contract

### 9.1 Engine

```csharp
public sealed record ScoredProduct(
    Guid ProductId, decimal Score,
    IReadOnlyList<string> MatchFacts, IReadOnlyList<string> CautionFacts,
    string? PlacementHint,
    ScoreBreakdown? Breakdown = null);          // optional: không phá call-site cũ

public sealed record ScoreBreakdown(
    string FormulaVersion,                      // "3.2"
    ScoringTarget Target, ProductPlacement Placement,
    decimal GapScore, decimal? PersonalScore,
    decimal PersonalWeight, string? PersonalWeightCode,
    decimal Blended,
    decimal UserPenalty, decimal DirectionPenalty, decimal VibePenalty,
    decimal RawScore, bool Clamped,
    ElementVector ProductVector,
    ElementVector NormalizedGap,        // ĝ  — gap / (|gap|₁/2)
    ElementVector? RuleScoreVector,     // r  — ruleScore(mệnh, ·), null khi trục cá nhân tắt
    ElementVector CombinedDirection,    // d  = (1−Wp)·ĝ + Wp·r        ← radar vẽ cái này
    ElementVector? PersonalNeedVector); // dụng thần — chỉ luồng Carry
```

**Bất biến (chốt bằng test):**
```
ProductVector · CombinedDirection                        == Blended
Blended − UserPenalty − DirectionPenalty − VibePenalty   == RawScore
round(clamp(RawScore, −1, 1), 3)                         == Score
```

### 9.2 JSON — thêm vào `ProductFitResponse` trước

```jsonc
"breakdown": {
  "formulaVersion": "3.2", "target": "WorkspaceGap", "placement": "Living", "displayPercent": 90,
  "components": [
    { "code": "GAP_SCORE", "labelVi": "Khớp nhu cầu của phòng",
      "value": 0.600, "weight": 0.50, "contribution": 0.300,
      "reasonVi": "Phòng đang thiếu Mộc (+0.6) và Thủy (+0.4); sản phẩm cấp Mộc." },
    { "code": "PERSONAL_SCORE", "labelVi": "Hợp bản mệnh của bạn",
      "value": 1.000, "weight": 0.50, "contribution": 0.500,
      "reasonVi": "Sản phẩm hành Mộc, tỷ hòa với bản mệnh Mộc (Canh Ngọ 1990)." }
  ],
  "penalties": [
    { "code": "DIRECTION_PENALTY", "labelVi": "Hướng hợp bị chắn", "value": 0.000, "applied": false,
      "reasonVi": "Không xét hướng — cây đặt theo ánh sáng." },
    { "code": "VIBE_MISMATCH_PENALTY", "labelVi": "Lệch vibe mục đích", "value": 0.000, "applied": false },
    { "code": "USER_CONFLICT_PENALTY", "labelVi": "Khắc bản mệnh", "value": 0.000, "applied": false,
      "reasonVi": "Không áp dụng — xung khắc đã tính có dấu trong điểm hợp mệnh." }
  ],
  "rawScore": 0.800, "clamped": false, "score": 0.800,
  "personalWeight": { "value": 0.50, "code": "PERSONAL_WEIGHT_PRIVATE", "scope": "Private",
    "reasonVi": "Phòng riêng tư — ưu tiên bản mệnh chủ nhân ngang với nhu cầu phòng." },
  "vectors": {
    "product":           [ { "element": "Moc", "value": 1.00 }, … ],   // Σ=1
    "normalizedGap":     [ { "element": "Moc", "value": 0.60 }, … ],   // ĝ ∈[−1,1]
    "ruleScore":         [ { "element": "Moc", "value": 1.00 }, { "element": "Kim", "value": -1.00 }, … ],  // r ∈[−1,1]
    "combinedDirection": [ { "element": "Moc", "value": 0.80 }, … ],   // d = (1−Wp)·ĝ + Wp·r
    "priorityVector":    [ { "element": "Moc", "value": 0.571 }, { "element": "Thuy", "value": 0.429 }, … ],
                                          // normalize(max(d,0)) — Σ=1, LỚP VÀNG TRÊN RADAR
    "personalNeed":      null                                          // chỉ luồng Carry
  },
  "destinyElement": "Moc", "destinyLabelVi": "Mộc — Đại Lâm Mộc (Mậu Thìn 1988)"
},
"contributions": [ /* CurrentContributionRow — sao chép từ element-analysis */ ],
"evidenceCount": 4, "confidence": 0.57
```

**Nguyên tắc:** `code` để FE map i18n/icon · `labelVi`/`reasonVi` luôn đi kèm số ·
**không gửi số thô cho LLM** (model đọc số sẽ bịa phép tính — vẫn chỉ gửi `matchFacts`/`cautionFacts`).

## 10. R2 + R3 — Thiết kế radar

### 10.1 "Private 50% personal" — mix ở TẦNG NÀO?

Trực giác *"trộn hành cá nhân với hành phòng rồi chuẩn hoá về %"* là **đúng**. Nhưng có **hai tầng**
có thể trộn, và code đang trộn ở tầng thứ hai:

| | **Tầng 1 — trộn MỤC TIÊU** *(mental model)* | **Tầng 2 — trộn CHÊNH LỆCH** *(code hiện tại)* |
|---|---|---|
| Công thức | `T = (1−Wp)·adjustedIdeal + Wp·personalVector`<br>`gap = T − current`<br>`score = gap·p / (\|gap\|₁/2)` | `ĝ = gap / (\|gap\|₁/2)`<br>`r[e] = ruleScore(mệnh, e)`<br>`d = (1−Wp)·ĝ + Wp·r`<br>`score = d·p` |
| Vector cá nhân dùng | `personalVector` — Σ=1, **chỉ dương** | `r` — điểm quan hệ, **CÓ DẤU** ∈[−1,+1] |
| Vẽ được chồng lên radar Σ=1? | ✅ trực tiếp | ❌ khác đơn vị |
| Phạt được sản phẩm **khắc mệnh**? | ❌ **KHÔNG** | ✅ có |
| `feng_shui_rules` (25 dòng; **chưa có endpoint admin** — xem §16) | ❌ không dùng | ✅ dùng |
| `SELF/SUPPORT/CHILD_SHARE` | ✅ sống lại | ❌ chết (chỉ dùng `Dominant()`) |

### 10.2 Ví dụ quyết định — vì sao KHÔNG chọn tầng 1

Phòng thiếu **Kim**, user mệnh **Mộc** (Kim khắc Mộc), `Wp = 0.5`:
`adjustedIdeal = {Kim 1.0}` · `current = {Mộc 1.0}` · `personalVector = {Mộc .6, Thủy .3, Hỏa .1}`

**Tầng 1:** `T = {Kim .5, Mộc .3, Thủy .15, Hỏa .05}` → `gap = {Kim +.5, Mộc −.7, Thủy +.15, Hỏa +.05}`
→ `|gap|₁ = 1.4` → sản phẩm **thuần Kim** = `+0.5 / 0.7` = **+0.714 → 86% "Rất hợp"**

> ⚠️ **Sản phẩm KHẮC BẢN MỆNH được 86%.** Vì `personalVector` không có phần tử âm, nó chỉ *kéo nhẹ*
> mục tiêu ra khỏi Kim, không bao giờ *phản đối* Kim.

**Tầng 2:** `ĝ[Kim] = +1.0`, `r[Kim] = −1.0` → `d[Kim] = 0.5(1.0) + 0.5(−1.0) = 0` → **50% "Trung tính"**

Thêm nữa, ở tầng 1 sản phẩm **Thổ** (vô hại) và **Kim** (khắc mệnh) đều ra **25%** — hệ thống
không phân biệt được hai thứ đó.

⇒ **Giữ tầng 2 CHO CHẤM ĐIỂM.**

> ### 🔴 Đính chính (phát hiện khi test FE) — tầng 1 vẫn dùng, nhưng để **VẼ**
> Bản nháp loại tầng 1 và dừng ở đó. Sai ở chỗ nó loại cho **cả hai** việc, trong khi lý do loại chỉ
> áp cho việc chấm điểm. Để **hiển thị**, tầng 1 mới là thứ đúng:
>
> ```
> T = (1 − Wp)·adjustedIdeal + Wp·personalVector          ← lớp radar "Mục tiêu của bạn"
> d = (1 − Wp)·ĝ + Wp·r                                    ← vẫn là thứ chấm điểm
> ```
>
> **Vì sao `T` vẽ được mà `priorityVector` thì không:** cả `adjustedIdeal` lẫn `personalVector` đều
> Σ=1 và `Wp ∈ [0,1]`, nên tổ hợp lồi **tự Σ=1** — không phải chuẩn hoá lại, và nó nằm gọn **cùng
> thang** với hai lớp kia. Còn `priorityVector = normalize(max(d,0))` zero hoá các trục âm rồi dồn
> 100% vào phần còn lại: trải trên 3/5 trục là mỗi trục ~33% thay vì ~20%, nên nó **luôn** to và nhọn
> hơn bất kể dữ liệu. Đo trên phòng thật (Kitchen · Shared · mệnh Kim):
>
> | Lớp | max | số trục > 0 |
> |---|---:|---|
> | Mức lý tưởng | 39.1% | 5 |
> | Hiện tại | 32.2% | 5 |
> | **`T` @ Wp=0.30** | **27.4%** | **5** |
> | ~~`priorityVector`~~ | 52.8% | 3 |
>
> **Cái giá phải trả, ghi rõ để không ai quên:** radar và điểm số nói hai thứ khác nhau. `T` nâng mục
> tiêu Kim từ 13.0% lên 27.1% nên radar ngầm bảo *"phòng nhiều Kim cũng không sao vì bạn mệnh Kim"*,
> trong khi engine vẫn coi Kim thừa −19.2% và vẫn trừ điểm sản phẩm hành Kim. Đó là **cố ý**: xem
> bảng ở §10.2b để biết chuyện gì xảy ra nếu đem `T` đi chấm điểm.
>
> `priorityVector` vẫn được trả trong API, chỉ **không vẽ chồng** — để dành cho radar phụ §10.5.

### 10.2b Vì sao KHÔNG đem `T` đi chấm điểm — số thật

Cùng phòng Kitchen · Shared · `Wp = 0.30` · chủ phòng mệnh **Kim**:

| SP thuần | Tầng 2 (đang chạy) | Tầng 1 (nếu chấm theo `T`) | |
|---|---:|---:|---|
| Kim | 42% | 36% | |
| Mộc | 45% | 19% | |
| Thủy | 53% | 66% | |
| **Hỏa** | **55%** | **84%** | ← **khắc** mệnh Kim |
| **Thổ** | **59%** | **45%** | ← **sinh** mệnh Kim |

Trục cá nhân **lật ngược**: hành khắc mệnh lên đầu bảng, hành sinh mệnh tụt dưới trung tính.

Lý do: `personalVector` **không có phần tử âm** nên nó chỉ *nâng* mục tiêu, không bao giờ *phản đối*.
Thổ vốn đã sát mục tiêu, nâng thêm chút thành "hơi thừa" ⇒ điểm âm; còn Hỏa thì phần thiếu vẫn nguyên
mà mất hẳn `r = −1.0` kéo xuống ⇒ vọt lên. Đây đúng là ca Q3 đã chốt **tầng CHÊNH LỆCH**.

### 10.3 ⭐ Lớp radar "Mục tiêu của bạn" — `personalTarget`

> ### 🔴 SUPERSEDED bởi §12 cho radar PHÒNG
> Mục này đi qua hai lần sửa: bản gốc chọn `priorityVector`, rồi §10.2 đổi sang `T`. **Cả hai nay đều
> không còn vẽ trên radar phòng.**
>
> Lý do: §12 đưa chủ nhân phòng thành một **nguồn phiếu thật trong `current`**. Khi bản mệnh đã nằm
> trong `current` rồi thì vẽ thêm `T` là **đếm ảnh hưởng của bản mệnh hai lần**. Lớp vàng nay là phần
> đóng góp thật của chủ nhân, đọc từ `contributions[]`, nhãn ghi **số phiếu**.
>
> Phần dưới giữ lại làm hồ sơ quyết định, và vì `priorityVector` vẫn đúng cho **radar phụ** (§10.5).

```
T = (1 − Wp)·adjustedIdeal + Wp·personalVector          Σ=1, đủ 5 trục
```

`personalVector` = bản mệnh 60% · hành **sinh ra** mệnh 30% · hành mệnh **sinh ra** 10%
(`SELF/SUPPORT/CHILD_SHARE`). Ba tham số này trước đây là **tham số chết** với engine — nó chỉ đọc
`.Dominant()` (§5). Vẽ lớp này làm chúng **sống lại** thành thứ admin chỉnh được để đổi hình radar;
sửa lại mục C.4 cho đúng.

#### Vẽ HAI lớp lồng nhau để thấy "bản mệnh chiếm bao nhiêu phần"

Đây là câu hỏi gốc của tính năng, và một đa giác đơn không trả lời được — vì `T` luôn Σ=1 nên nó
**không to lên hay nhỏ đi** khi `Wp` đổi, nó chỉ **đổi hình**. Tách đôi thì thấy ngay:

```
phần PHÒNG    = (1 − Wp)·adjustedIdeal        ← vẽ đè lên, tô nền
phần BẢN MỆNH = Wp·personalVector             ← chính là VÀNH còn lại giữa hai đường
```

`Σ` phần phòng = `(1−Wp)×100%`, `Σ` phần bản mệnh = `Wp×100%` — **độ dày vành đúng bằng trọng số**.
Kéo `Wp` 5% → 50% thì vành mỏng như sợi chỉ dày lên thành một dải rõ rệt.

| `Wp` | Σ phần phòng | Σ phần bản mệnh | Dày theo trục (mệnh Kim) |
|---:|---:|---:|---|
| 0.05 | 95% | 5% | Kim 3 · Thổ 2 |
| 0.30 | 70% | 30% | Kim 18 · Thổ 9 · Thủy 3 |
| 0.50 | 50% | 50% | Kim 30 · Thổ 15 · Thủy 5 |

#### ⚠️ Ba hiểu nhầm dễ mắc khi kéo slider

| Kỳ vọng | Thực tế |
|---|---|
| "Kéo `Wp` lên thì lớp **Hiện tại** co lại" | **Không.** `Hiện tại` là hiện trạng THẬT của phòng (nền phòng + tag user khai). `Wp` là trọng số trên **mục tiêu**, không có mặt trong công thức của `current`. Kéo slider mà tường nhà đổi màu mới là sai |
| "Lớp **Mức lý tưởng** cũng phải co theo `(1−Wp)`" | **Không.** `adjustedIdeal` bị nhân `(1−Wp)` **bên trong** `T`; bản thân lớp xám vẫn vẽ nguyên 100%. Muốn thấy phần đã bị nhân thì nhìn lớp "phần của phòng" ở trên |
| "`Wp` = 50% thì vàng phải **to hơn** `Wp` = 5%" | **Không.** `T` luôn Σ=1. Đo trên phòng thật: `Wp`=0.05 → đỉnh **Hỏa 37.2%**; `Wp`=0.50 → đỉnh **Kim 36.5%**. Đỉnh gần bằng nhau, diện tích cũng gần bằng nhau — thứ đổi là **HƯỚNG** của đa giác, không phải kích thước. `Wp`=0 thì `T ≡ adjustedIdeal` (trùng lớp xám), `Wp`=1 thì `T ≡ personalVector` |

#### Bẫy cài đặt đã vấp

`toMap(undefined)` trả về **vector 0** — một giá trị trông hợp lệ. Thiếu `personalVector` (BE chưa
deploy) thì `T = (1−Wp)·adjustedIdeal`, tức đa giác **co đều** khi kéo `Wp` lên (Σ tụt còn 70%, rồi
50%) thay vì đổi hình. Sai mà vẫn "chạy được". FE nay chặn bằng `hasValues()`: thiếu dữ liệu thì
**không vẽ lớp đó**, thay vì vẽ một hình sai.

---

### 10.3b Bản gốc — `priorityVector` *(giữ cho radar phụ §10.5)*

`d` chính là **vector đã trộn** mà bạn đang hình dung, chỉ khác là nó có dấu. Muốn đưa về thang Σ=1
để chồng lên radar hiện tại, lấy **phần dương rồi chuẩn hoá**:

```
priorityVector = normalize( max(d, 0) )        // Σ=1, vẽ được như 2 lớp kia
```

Ý nghĩa: ***"sau khi tính 50% bản mệnh của bạn, hệ thống đang ưu tiên bù hành nào"***.
Phần âm (hành bị trừ điểm) **không mất đi** — nó hiện thành **màu đỏ trên nhãn trục**.

**Ví dụ**: phòng ideal `{Kim .6, Thổ .4}`, current `{Mộc .5, Hỏa .5}`, mệnh Mộc, `Wp = .5`

| Hành | `ĝ` (phòng) | `r` (mệnh) | `d` | `priorityVector` | Nhãn trục |
|---|---:|---:|---:|---:|---|
| Thủy | 0.00 | +0.80 | **+0.40** | **42%** | xanh |
| Thổ | +0.40 | +0.20 | **+0.30** | **32%** | xanh |
| Mộc | −0.50 | +1.00 | **+0.25** | **26%** | xanh |
| Kim | +0.60 | −1.00 | **−0.20** | 0% | 🔴 đỏ |
| Hỏa | −0.50 | −0.20 | **−0.35** | 0% | 🔴 đỏ |

So với `adjustedIdeal = {Kim 60%, Thổ 40%}` — hình đa giác **dịch hẳn** sang Thủy/Thổ/Mộc.
Đó chính là thứ trực quan hoá "50% personal", và nó **đúng bằng toán của engine**.

#### `priorityVector` CHÍNH LÀ "personal weight trên radar"

`Wp` nằm ngay trong `d = (1 − Wp)·ĝ + Wp·r`, nên nó **đổi hình đa giác**. Cùng phòng
`ideal = {Kim .6, Thổ .4}` · `current = {Mộc .5, Hỏa .5}` · mệnh **Mộc**:

| `Wp` | `priorityVector` — hình trên radar | Đọc là |
|---:|---|---|
| **0.0** | Kim **60%** · Thổ **40%** | thuần *"phòng đang thiếu gì"* |
| **0.3** | Thổ 36% · Kim 24% · Thủy 24% · Mộc 16% | phòng vẫn trội |
| **0.5** | **Thủy 42%** · Thổ 32% · Mộc 26% · Kim 🔴 | lai — *"phòng cần Kim nhưng bạn kỵ Kim"* |
| **1.0** | Mộc **50%** · Thủy **40%** · Thổ 10% | thuần *"bản mệnh bạn hợp hành gì"* |

Đa giác **xoay hẳn** từ Kim/Thổ sang Thủy/Thổ/Mộc khi kéo `Wp` từ 0 lên 0.5 — đó chính là thứ bạn
muốn nhìn thấy. Không cần lớp riêng cho "personal weight": **nó đã nằm trong hình rồi**.

Kèm chip số cạnh biểu đồ: **"Phòng riêng — 50% bản mệnh"**.

**Kéo slider `Wp` ⇒ đa giác morph** — FE tự tính, có đủ `ĝ`, `r`, `Wp`, **không cần gọi API**.

#### ⚠️ Vì sao KHÔNG vẽ `personalVector` — "không tạo ra điểm" nghĩa là gì

`personalVector` mệnh Mộc = `{ Mộc 0.6, Thủy 0.3, Hỏa 0.1 }`. Engine dùng nó **đúng một lần**:

```csharp
var personalDominant = personalVec.Dominant();      // ← lấy DUY NHẤT "hành nào lớn nhất" = Mộc
personalScore = Σ productVector[e] × RuleScoreOf(personalDominant, e);
//                                    ↑ bảng feng_shui_rules, KHÔNG phải personalVector
```

Ba con số `0.6 / 0.3 / 0.1` **không bao giờ được đọc**. Bằng chứng: thay `personalVector` bằng

| Thay bằng | `Dominant()` | `personalScore` |
|---|---|---|
| `{ Mộc 0.99, Thủy 0.005, Hỏa 0.005 }` | Mộc | **y hệt** |
| `{ Mộc 0.34, Thủy 0.33, Hỏa 0.33 }` | Mộc | **y hệt** |

Hình trên radar đổi hoàn toàn, **điểm không nhúc nhích một phần nghìn**. Đó là ý của "không tạo ra điểm".

**Hậu quả nếu vẫn vẽ:** user nhìn thấy đỉnh Thủy 30% và kết luận *"Thủy đang đóng góp 30% vào điểm của tôi"*
— sai. Thứ quyết định phần cá nhân là **vector điểm quan hệ** `r = { Mộc +1.0, Thủy +0.8, Thổ +0.2,
Hỏa −0.2, Kim −1.0 }`, và `r` **đã nằm trong `priorityVector`** rồi.

> Nói ngắn: `personalVector` là *"bản mệnh tôi gồm những hành nào"* (thông tin nền).
> `r` là *"tôi hợp / kỵ hành nào, mạnh yếu ra sao"* (thứ chấm điểm). Radar phải vẽ cái thứ hai.

### 10.4 Ba lớp trên MỘT radar *(khuyến nghị — đúng mockup của bạn)*

| Lớp | Nguồn | Kiểu vẽ | Điều kiện |
|---|---|---|---|
| Mức lý tưởng | `adjustedIdeal` | nét đứt xám | luôn ✅ *(đã có)* |
| Hiện tại | `current` | nét liền + fill xanh | luôn ✅ *(đã có)* |
| Xem trước | `previewCurrent` | nét đứt primary | khi `hasPreview` ✅ *(đã có)* |
| ~~**Mục tiêu của bạn**~~ | ~~`personalTarget` (T)~~ | ~~nét liền + fill vàng~~ | 🔴 **SUPERSEDED → §12.7** |
| ~~— trong đó phần của phòng~~ | ~~`(1−Wp)·adjustedIdeal`~~ | ~~nét đứt mảnh~~ | 🔴 **ĐÃ BỎ — xem §12.7** |
| **Phần của bạn** | `contributions[Person]` | **nét liền + fill vàng**, nhãn **số phiếu** | khi chủ nhân có phiếu > 0 (§12) |
| — | `gap` vượt ngưỡng **0.1** | **dấu trục `↑` đỏ (thừa) / `↓` xanh (thiếu)** | ⬅ MỚI |

Legend: `-- Mức lý tưởng` · `● Hiện tại` · `● Phần của bạn (3 phiếu)` · `↑ thừa` `↓ thiếu`

> 🔴 **Lớp vàng đã đổi nghĩa ở §12.** Nó từng là `T` — một **mục tiêu** tưởng tượng nằm ngoài `current`.
> Nay chủ nhân là một **nguồn phiếu thật trong `current`**, nên lớp vàng là **phần có thật của căn
> phòng**, luôn nằm trong lớp xanh. Vẽ `T` thêm nữa là **đếm bản mệnh hai lần**. Lớp con nét đứt
> `(1−Wp)·adjustedIdeal` đã gỡ hẳn.

> ### 🔴 Đính chính — dấu trục đọc từ `gap`, KHÔNG đọc từ `d`
> Bản nháp bảo tô đỏ trục có `d[e] < 0` và gọi đó là "hành bị khắc". **Sai tên, và sai cả nguồn.**
>
> `d` đã trộn điểm hợp mệnh nên dấu của nó không còn là "thừa/thiếu". Phản ví dụ trên phòng thật:
> `d[Thổ] = +0.172` (dương) trong khi phòng **đang thừa** Thổ (`gap = −2.8%`). Và nhãn "bị khắc" thì
> sai hẳn: chủ phòng **mệnh Kim**, phòng thừa Kim ⇒ `d[Kim] < 0` ⇒ trục Kim bị tô đỏ và gọi là "bị
> khắc", trong khi Kim **tỷ hòa** với chính bản mệnh chủ phòng (`r[Kim] = +1.00`).
>
> Ba đại lượng khác nhau, đừng gộp:
>
> | Muốn nói | Nguồn đúng |
> |---|---|
> | Phòng thừa/thiếu hành gì | **`gap`** ← dấu trục dùng cái này |
> | Hệ thống đang tránh bù hành gì | `d < 0` ← chỉ để trong tooltip |
> | Hành nào khắc bản mệnh | `r < 0` |
>
> Radar này tên là *"Ngũ hành không gian của bạn"* nên trục phải nói về **phòng**. Dùng lại đúng
> `TAG_GAP_THRESHOLD = 0.1` của hàng chip phía trên, để chip và radar không bao giờ nói khác nhau về
> cùng một hành. Hiện **cả hai chiều** — thừa cũng là lệch chuẩn như thiếu.

> ### 🔴 Đính chính (phát hiện khi test FE) — lớp vàng nằm ở radar **PHÒNG**
> Bản nháp chỉ ghi *"`ElementRadarChart` += lớp `priorityVector`"* mà không nói **màn hình nào**, nên
> lần cài đầu tôi gắn vào panel chấm sản phẩm và người dùng mở trang hồ sơ không gian thì không thấy gì.
>
> Đúng ra phải là radar phòng, vì `priorityVector` **không phụ thuộc sản phẩm**: `ĝ` là gap của phòng,
> `r` là điểm quan hệ của bản mệnh — chấm sản phẩm nào cũng ra cùng một đa giác. Bộ legend ở bảng trên
> (`Mức lý tưởng · Hiện tại · Xem trước`) cũng chính là legend của radar phòng.
>
> **Hệ quả về BE:** `GET /workspace-profiles/{id}/element-analysis` phải trả thêm khối
> `personalDirection` (`ĝ`, `r`, `Wp`, `priorityVector`, `conflictResolution`) — trước đó cả ba đại
> lượng này chỉ sống trong `ScoreBreakdown`, mà breakdown chỉ sinh khi chấm **một sản phẩm cụ thể**.
> Cả hai màn hình nay dùng chung `ElementDirection` nên không thể vẽ lệch nhau.

Chip cạnh biểu đồ: **"Phòng riêng — 50% bản mệnh"**, bấm vào mở giải thích `personalWeight.reasonVi`.

### 10.5 Radar phụ *(tuỳ chọn, sau)* — "Vì sao sản phẩm này được điểm đó"

Khi cần soi **một sản phẩm cụ thể**, dùng radar thang **−1 → +1** (tâm = −1, vành = +1):
lớp `d` (fill) + chấm `productVector` trên từng trục. `score = Σ productVector[e]·d[e]` — nhìn là thấy
*"chấm nằm trên trục `d` cao ⇒ điểm cao"*. Không bắt buộc cho đợt này.

### 10.6 Bản cũ — hai radar tách bạch *(để tham khảo)*

#### Radar 1 — "Phòng của bạn" *(phương án 2 radar — thay bằng §10.4)*
Trả lời: *phòng đang thiếu/thừa hành gì, và bạn hợp hành gì*. Đơn vị: tỉ lệ Σ=1.

| Lớp | Nguồn | Trạng thái |
|---|---|:-:|
| Mức lý tưởng (nét đứt xám) | `adjustedIdeal` | ✅ có |
| Hiện tại (nét liền + fill) | `current` | ✅ có |
| Xem trước (nét đứt primary) | `previewCurrent` | ✅ có |
| Tooltip nguồn theo % | `contributions` | ✅ có |

#### Radar 2 — "Vì sao sản phẩm này được điểm đó" *(MỚI)*
Trả lời: *hệ thống đang ưu tiên hành nào, và sản phẩm cấp hành nào*. Đơn vị: **thang −1 → +1**
(tâm = −1 "tối kỵ", vành = +1 "rất hợp"), vẽ bằng `(x+1)/2`.

| Lớp | Nguồn | Ý nghĩa |
|---|---|---|
| **Ưu tiên tổng hợp** (fill đậm) | `combinedDirection` **d** | `(1−Wp)·ĝ + Wp·r` — thứ nhân với sản phẩm |
| Nhu cầu phòng (nét đứt xám) | `normalizedGap` **ĝ** | phần phòng, khi `Wp` = 0 thì d ≡ ĝ |
| **Hợp bản mệnh** (nét đứt vàng) | `ruleScore` **r** | **chỉ hiện khi `Wp > 0`** ⬅ đúng yêu cầu R2 |
| Sản phẩm (chấm tròn trên trục) | `productVector` | Σ=1, chấm to nhỏ theo tỉ trọng |

**Vì sao đúng:** `score = Σ_e productVector[e] × d[e]`. Nhìn radar là thấy ngay *"chấm sản phẩm nằm
trên trục có `d` cao ⇒ điểm cao"*. Đây là **giải thích chính xác**, không phải minh hoạ.

**Ảnh hưởng của `Wp` hiện trực quan:** kéo slider `Wp` từ 0→0.5, lớp `d` **morph** từ trùng `ĝ`
sang lai giữa `ĝ` và `r`. FE tự tính `d` được (có đủ `ĝ`, `r`, `Wp`) ⇒ slider mô phỏng **không cần gọi API**.

#### Radar cho sản phẩm `Carry` *(R3)*
Không có phòng ⇒ **bỏ hẳn lớp `ĝ` và lớp `d`**:

| Lớp | Nguồn |
|---|---|
| Dụng thần của bạn (fill) | `personalNeedVector` — Σ=1, từ `PersonalTargetBuilder` (Tứ Trụ, fallback Nạp Âm) |
| Sản phẩm (chấm/nét liền) | `productVector` |

Chú thích cố định: *"Vật mang theo người — chấm theo bản mệnh, không phụ thuộc phòng hay hướng đặt."*
Legend **không** có "Mức lý tưởng"/"Hiện tại".

⚠️ **Chưa có endpoint cho việc này.** `GET /recommendations/fit` bắt buộc `workspaceProfileId`.
Cần thêm **`GET /api/recommendations/fit/personal?productId=`** → `PersonalFitResponse`
(`score` · `breakdown` · `personalNeedVector` · `productVector` · `personalTarget`), dùng
`PlacementPolicy.For(Carry)` và **không loại** (hợp đồng Fit).

### 10.7 Trạng thái rỗng

| Điều kiện | Hiển thị |
|---|---|
| `Wp = 0` vì phòng `Public` | Ẩn lớp cá nhân + chip *"Không gian chung — không tính bản mệnh"* |
| `Wp = 0` vì user chưa có `DateOfBirth` | Ẩn lớp cá nhân + CTA *"Thêm ngày sinh để nhận gợi ý hợp bản mệnh"* |
| `Wp = 0` vì tham số đang tắt | Chip *"Trục cá nhân đang tắt"* (chỉ hiện cho Admin) |

## 11. R5 — Nghề nghiệp

### 11.1 Schema — theo pattern `work_purpose_modifiers` (**cộng delta**)

```sql
occupations (id, code UNIQUE, name_vi, description, is_active, is_system_seeded, …audit)
occupation_element_modifiers (occupation_id, element, delta numeric(4,3), PK(occupation_id, element))
users.occupation_id uuid NULL REFERENCES occupations(id)
```

**Bảng tra cứu, không enum:** danh sách nghề **mở** (giống `Vibe`/`Style`), khác `Aspiration` (cố định = 4 cung Bát Trạch).
**Lưu trên `User`, khác `Aspiration` (runtime):** nghề nghiệp ổn định theo năm, và luồng `Carry` cũng cần.

### 11.2 Vào công thức ở đâu — **bẻ vector CÁ NHÂN**

| | **A. Bẻ vector cá nhân** ✅ | B. Bẻ `adjustedIdeal` của phòng |
|---|---|---|
| Ngữ nghĩa | nghề đi theo **người** | nghề đi theo **phòng** |
| Luồng `Carry` | dùng được | ❌ không có phòng |
| Trùng lặp | không | ❌ **trùng `WorkPurpose`** |

`WorkPurpose` = *phòng dùng để làm gì*; `Occupation` = *người làm nghề gì*.

### 11.3 ⚠️ Vướng với §5 — cần chốt

Vì `personalScore` **chỉ dùng `Dominant()`**, bẻ `personalVector` bằng delta nghề nghiệp sẽ **không đổi
điểm** trừ khi delta đủ lớn để **lật đỉnh** sang hành khác — và lật đỉnh là thay đổi *đột ngột*, không
mượt. Ba lựa chọn:

| | Cách | Đánh giá |
|---|---|---|
| **N1** | Nghề nghiệp bẻ **`r` (vector điểm quan hệ)**: `r'[e] = clamp(r[e] + delta[e]·OCCUPATION_SHARE, −1, 1)` | ✅ **Khuyến nghị** — tác động mượt, tuyến tính, vào thẳng thứ nhân với sản phẩm, và **hiện được trên Radar 2** |
| N2 | Bẻ `personalVector` rồi đổi `PersonalAffinity` sang tính trên cả vector | Pha loãng trục cá nhân (thuần tỷ hòa rơi 1.0 → 0.62) — ngược mục tiêu |
| N3 | Nghề nghiệp thành **trục thứ ba**: `score = (1−Wp−Wo)·ĝ·p + Wp·r·p + Wo·o·p` | Sạch nhất về khái niệm nhưng thêm một tham số trọng số nữa phải hiệu chỉnh |

Với **N1**, `occupation_element_modifiers.delta` mang nghĩa *"nghề này khiến hành X hợp/kỵ hơn bao nhiêu"* — trực tiếp, dễ giải thích cho user.

### 11.4 Kill-switch — HAI lớp, độc lập nhau

| Lớp | Trạng thái | Tắt bằng cách nào |
|---|---|---|
| `OCCUPATION_SHARE` seed **0.00** | ✅ đã seed | mọi delta × 0 |
| `occupation_element_modifiers` **rỗng** | ✅ seeder cố ý không seed | không có delta để nhân |

Cả hai cùng cho kết quả **byte-identical** (cùng pattern `PERSONAL_WEIGHT_*`, `VIBE_FILTER_HARD`), và
`SCORE-OCC-01`/`SCORE-OCC-02` khoá riêng từng lớp. Hai lớp vì chúng hỏng theo hai cách khác nhau: ai
đó nâng tham số khi bảng delta chưa duyệt, hoặc ai đó nhập delta khi chưa ai định bật tính năng.

Khi nghề nghiệp **không dịch được gì** (share = 0, delta = 0, hoặc delta chỉ trỏ vào hành khắc mệnh nên
bị chặn hết), breakdown trả `occupation = null` chứ không trả một khối có `shift` toàn 0 — nếu không FE
sẽ vẽ một lớp radar phẳng lì và một dòng giải thích rỗng nghĩa.

### 11.5 Bảng delta seed — **BẢN NHÁP, CẦN CHUYÊN GIA PHONG THỦY DUYỆT**

| `code` | Tên | delta (nháp) | Lý do |
|---|---|---|---|
| `IT` | CNTT / Lập trình | Thủy +0.15, Hỏa −0.08 | trí tuệ, linh hoạt; giảm hỏa khí màn hình |
| `SALES` | Kinh doanh / Sales | Kim +0.15 | quyết đoán, tiền bạc |
| `EDU` | Giáo dục / Nghiên cứu | Mộc +0.15 | phát triển, tri thức |
| `HEALTH` | Y tế | Thủy +0.10, Mộc +0.10 | sinh khí, hồi phục |
| `CREATIVE` | Sáng tạo / Nghệ thuật | Hỏa +0.15 | cảm hứng, biểu đạt |
| `CONSTRUCT` | Xây dựng / BĐS | Thổ +0.15 | nền tảng, vững chắc |
| `FINANCE` | Tài chính / Kế toán | Kim +0.12, Thổ +0.05 | Kim = tiền, Thổ sinh Kim |
| `OTHER` | Khác | *(không dòng nào)* | trung tính |

⚠️ **Tôi không tự chế được bảng này** — cần người có chuyên môn ký duyệt, như `feng_shui_rules` đã làm.

> ### 📌 Trạng thái: bảng này VẪN CHƯA vào DB
> P5 đã code xong nhưng seeder **cố ý không seed delta** — `occupation_element_modifiers` đang rỗng.
> Seeder chỉ tạo 8 dòng *danh sách nghề* (taxonomy, không phát biểu gì về phong thủy).
>
> Nghề chưa có delta ⇒ engine bỏ qua hoàn toàn, không đoán. Cộng thêm `OCCUPATION_SHARE = 0.00`, hiện
> có **hai lớp khoá** độc lập giữ P5 ở trạng thái không-tác-động.
>
> **Đường bật:** chuyên gia duyệt bảng trên → nhập bằng `PUT /api/admin/scoring/occupations/{code}/modifiers`
> → đối chiếu golden set → mới nâng `OCCUPATION_SHARE`.

## 12. Chủ nhân phòng là một NGUỒN trong `current` — mô hình phiếu

> **Bổ sung sau khi test FE.** Mục này **supersede** cách dùng `personalTarget`/`T` ở §10.3 cho radar
> phòng, và bổ sung một tham số mới độc lập với `Wp`.

### 12.1 Công thức tổng quát của `current`

```
current = normalize( Σᵢ vᵢ · wᵢ )
```

Mỗi nguồn `i` góp một vector đã chuẩn hoá `vᵢ` (Σ=1) và một số **phiếu** `wᵢ`:

| Nguồn | `vᵢ` | `wᵢ` |
|---|---|---|
| Nền phòng | `normalize(rows Source="Interior")`; rỗng → `(.2,.2,.2,.2,.2)` | `INTERIOR_PRIOR_VOTES` = **3** |
| Mỗi tag user khai | `normalize(Σ map[kind,code])` | `‖Σ map[kind,code]‖₁` — thường **1** |
| **Chủ nhân phòng** ⬅ MỚI | `normalize(personalVector)` = mệnh 60 / hành sinh mệnh 30 / hành mệnh sinh 10 | `PERSON_PRESENCE_VOTES_*` |
| Mỗi sản phẩm đã đặt | `normalize(productVector)` | `voteWeight`, mặc định **1** |

Cộng **thô theo phiếu rồi chuẩn hoá MỘT lần** ở cuối — không chuẩn hoá từng nguồn rồi cộng, để giữ
đúng tỉ lệ giữa các nguồn.

Dẫn xuất: `sharePercentᵢ = wᵢ/Σw` · `evidenceCount` = số nguồn **Tag + Product** ·
`confidence = Σ w(Tag+Product) / Σw`.

### 12.2 Vì sao PHIẾU, không phải tỉ trọng %

Mô hình này là **Dirichlet prior**: suy đoán nhường chỗ cho bằng chứng. Nền phòng chiếm 100% khi chưa
khai gì, tụt còn 13% khi khai 20 tag. Bản mệnh cũng là **prior** — nó suy ra từ ngày sinh, không phải
điều quan sát được trong phòng — nên nó phải loãng đi giống hệt:

| Số tag | Nền phòng (3 phiếu) | Chủ nhân **3 phiếu** | Chủ nhân **share 30%** |
|---:|---:|---:|---:|
| 0 | 100% | 50.0% | 30.0% |
| 6 | 33.3% | 25.0% | 30.0% |
| 20 | **13.0%** | **11.5%** | **30.0%** |

Cột phiếu bám sát cột nền phòng. Cột share thì **đứng yên**: khai 20 tag thật về căn phòng mà bản mệnh
vẫn giữ nguyên phần của nó — ngược hẳn triết lý của mô hình.

Ba lý do phụ: phiếu **giải thích được bằng một câu** (*"chủ nhân nặng bằng 3 tag"*); **không cần máy
móc mới** (thêm một dòng `CurrentContribution` là xong, tooltip chạy nguyên); và **cùng đơn vị** với
mọi tham số khác trong bảng. Tỉ trọng cố định thì phải tính ngược `w = share/(1−share)·Σw_khác`, ra
những con số như **3.857 phiếu** — vô nghĩa khi hiện lên tooltip.

### 12.3 Giá trị seed và vì sao

| Mã | Private | Shared | Public |
|---|---:|---:|---:|
| `PERSON_PRESENCE_VOTES_*` | **3** | **2** | **0** |

Neo vào nền phòng: phòng riêng thì chủ nhân nặng **ngang** nền phòng — một mốc giải thích được, thay
vì một con số tự chọn. Phòng chung nhẹ hơn vì chia với người khác. `Public` = 0: không gian chung
không có "chủ nhân" nào để đưa bản mệnh vào.

⚠️ Chọn số lớn hơn (vd 8 cho Private) làm phòng **chưa khai tag nào** có chủ nhân chiếm **72.7%** —
`current` gần như chỉ còn là bản mệnh, đúng lúc `evidenceCount = 0` tức lúc hệ thống biết ít nhất về
căn phòng.

`INTERIOR_PRIOR_VOTES` = 3 cũng được đưa từ hằng hard-code vào `scoring_params` cho cùng họ.

### 12.4 `confidence` phải GIẢM khi thêm chủ nhân

Chủ nhân vào **mẫu số** (nó góp vào `current`) nhưng **không** vào tử số (không phải bằng chứng user
khai). `CurrentBreakdown.IsPrior` gom cả `Interior` lẫn `Person`.

| Phòng | nền | tag | người | trước | **sau** |
|---|---:|---:|---:|---:|---:|
| Bàn học | 3 | 7 | 3 | 70.0% | **53.8%** |
| Nhà bếp | 3 | 6 | 2 | 66.7% | **54.5%** |

Tính chủ nhân là bằng chứng thì phòng chưa khai gì vẫn báo độ tin cậy cao — sai hẳn bản chất.

### 12.5 Hai tham số song song, KHÔNG thay thế nhau

| Mã | Đơn vị | Trả lời | Đi vào |
|---|---|---|---|
| `PERSON_PRESENCE_VOTES_*` | **phiếu** | *"Phòng này thực tế có những hành gì"* | `current` → `gap` → radar **và** điểm |
| `PERSONAL_WEIGHT_*` | **tỉ trọng** | *"Đồ nào hợp với người này"* | `r` → `d` → điểm sản phẩm |

Bỏ `PERSONAL_WEIGHT_*` chính là phương án đã bị loại: `r` là **thứ duy nhất trong hệ thống có dấu âm**.
`personalVector` không có phần tử âm nên nó không bao giờ nói được "hành này khắc bạn" — bỏ `r` là mất
hẳn lớp bảo vệ khắc mệnh.

Hai cơ chế có thể **trừ nhau** ở cùng một hành, và đó là đúng chứ không phải xung đột:

> *"Kim hợp bạn, nhưng phòng đã quá nhiều Kim rồi — và một phần là do chính bạn."*

**Phạt khắc mệnh giữ nguyên** (ĐÃ CHỐT): `Carry` → loại thẳng · `Desk`/`Living` → `Scaled`, trừ
`UCP × Wp` · `Public` → không lọc không phạt.

### 12.6 Ảnh hưởng — chủ nhân vào cả ba đường

Chủ nhân phải có mặt ở **mọi** chỗ dựng `current`, không chỉ radar:

| Đường | Chỗ sửa |
|---|---|
| Radar phòng | `WorkspaceProfileService` — cả `current` lẫn `previewCurrent` |
| Fit sản phẩm | `RecommendationService.GetProductFitAsync` — cả hai |
| **Gap dùng CHẤM ĐIỂM** | `WorkspaceElementAnalyzer.Analyze` → `ScoringContext.CurrentVector` |

Bỏ sót đường thứ ba thì hình và điểm nói hai chuyện khác nhau về cùng một căn phòng — đúng bất nhất
đã mất công gỡ ở §10.2.

Ở Nhà bếp (Shared, 2 phiếu, mệnh Kim): `current[Kim]` 32.2% → **39.2%**, `gap[Kim]` −19.2 → **−26.1**.
Bếp càng thừa Kim — đúng thực tế, vì chủ nhân cũng là một nguồn Kim trong phòng.

⚠️ **Điểm sản phẩm đổi theo**, không chỉ radar. Golden set §P2 sẽ phải dựng lại khi có test phủ tầng
service (hiện golden set dựng `ScoringContext` trực tiếp nên chưa đỏ).

### 12.7 Lớp radar: "Phần của bạn", không phải `T`

Khi bản mệnh **đã thật sự nằm trong** `current`, vẽ thêm `T = (1−Wp)·adjustedIdeal + Wp·personalVector`
là **đếm ảnh hưởng của bản mệnh hai lần**. Lớp vàng nay là **phần đóng góp thật của chủ nhân vào
`current`** — đọc thẳng từ `contributions[]`, nhãn ghi **số phiếu**.

Nó **luôn nằm trong** lớp "Hiện tại" theo từng trục, vì là một số hạng không âm của chính tổng đó.
`Σ` của nó = `sharePercent` của chủ nhân (vd 18%), **không phải 1** — nó là một phần của phòng, không
phải một mục tiêu. Lớp nét đứt "phần của phòng" bị gỡ luôn.

`personalTarget` bị gỡ khỏi `personalDirection`. `priorityVector` vẫn trả nhưng **không vẽ** — để dành
cho radar phụ §10.5.

---

## 13. Xử lý xung khắc: user hành X, phòng cần hành Y mà Y khắc X

Engine **đã tự giải** — không cần luật thêm. Cùng ví dụ §10.2 (phòng cần Kim, mệnh Mộc):

| Hành | `ĝ` (phòng) | `r` (mệnh) | `d = 0.5ĝ + 0.5r` |
|---|---:|---:|---:|
| Kim | **+1.00** | **−1.00** | 0.00 — triệt tiêu |
| Mộc | −1.00 | +1.00 | 0.00 — triệt tiêu |
| **Thủy** | 0.00 | +0.80 | **+0.40** ⬅ **thắng** |
| Thổ | 0.00 | +0.20 | +0.10 |
| Hỏa | 0.00 | −0.20 | −0.10 |

Thủy thắng — và Thủy đúng là **hành hóa giải** kinh điển: *Kim sinh Thủy, Thủy sinh Mộc*.

**Quy luật luôn đúng trong ngũ hành:** nếu A khắc B thì **con của A = mẹ của B**.
```
GetGeneratedElement(hànhPhòngCần) == GetGeneratingElement(bảnMệnh)
Kim→Thủy→Mộc · Mộc→Hỏa→Thổ · Hỏa→Thổ→Kim · Thổ→Kim→Thủy · Thủy→Mộc→Hỏa
```

**Việc cần làm là NÓI RA**, không phải sửa công thức:

```csharp
var roomNeed = normalizedGap.Dominant();                        // hành phòng thiếu nhất
if (ctx.RuleScoreOf(destiny, roomNeed) < 0)                     // phòng cần đúng thứ mình kỵ
{
    var bridge = FengShuiCalculator.GetGeneratedElement(roomNeed);   // == GetGeneratingElement(destiny)
    // → thêm vào breakdown.conflictResolution
}
```

```jsonc
"conflictResolution": {
  "roomNeed": "Kim", "destiny": "Moc", "bridge": "Thuy",
  "reasonVi": "Phòng đang thiếu Kim, nhưng Kim khắc bản mệnh Mộc của bạn. Hệ thống ưu tiên vật hành Thủy — Kim sinh Thủy, Thủy sinh Mộc — bù cho phòng mà vẫn nuôi bản mệnh."
}
```

FE hiện thành **banner cảnh báo nhẹ** phía trên danh sách + highlight trục `Thủy` (hành hoá giải) trên radar.
`null` khi không có xung khắc.

## 14. Khắc bản mệnh — phương án **L2** *(ĐÃ CHỐT)*

### 14.1 Vấn đề còn lại sau khi đã chuẩn hoá thang điểm

Khi phòng cần **đúng hành khắc mệnh**, hai lực triệt tiêu nhau:
phòng `ideal={Kim 1.0}` · `current={Mộc 1.0}` · mệnh **Mộc** · `Wp=0.5`

```
d[Kim] = 0.5×(+1.00) + 0.5×(−1.00) = 0.00   →  50% "Trung tính"
```

Sản phẩm **khắc bản mệnh** hiển thị **"Trung tính"** — nhãn nói sai bản chất.

> ⚠️ Đây **không** phải lỗi "63%" của §12.2. 63% là con số của mô hình **trộn MỤC TIÊU** đã bị loại;
> code hiện tại cho **38%** ở ca đó. Vấn đề ở đây hẹp hơn: **chỉ khi phòng rất cần đúng hành mình kỵ**.

### 14.2 Công thức L2

```csharp
// PersonalConflictMode.Scaled — giá trị enum thứ 4, thay cho None khi personalBlend đang bật
if (FengShuiCalculator.GetRelation(destiny, productDominant) == FengShuiRelation.BiKhac)
{
    userPenalty = ctx.Params.UserConflictPenalty * ctx.PersonalWeight;
    cautions.Add($"Hành {productDominant} khắc bản mệnh {destiny} — trừ {userPenalty:0.00} điểm.");
}
```

`score = productVector · d − userPenalty − dirPenalty − vibePenalty`

`UCP = 0.60` (sau khi ×2 ở §8.3). Cùng ca §14.1:

| Scope | `Wp` | `d·p` | penalty | score | màn hình | tier |
|---|---:|---:|---:|---:|---:|---|
| `Private` | 0.50 | 0.000 | **0.300** | **−0.300** | **35%** | Cân nhắc ✅ |
| `Shared` | 0.30 | +0.400 | **0.180** | +0.220 | 61% | Phù hợp |
| `Public` | 0.00 | +1.000 | *xem §14.3* | | | |

Sản phẩm **không** khắc mệnh không bị đụng: SP Thủy → `TuongSinh` → penalty 0 → `+0.40 → 70%`.

**Vì sao L2 hơn L1/L3/L4:**

| | |
|---|---|
| Tự co giãn theo scope | phòng càng riêng tư, kiêng kỵ càng nặng |
| Giữ nguyên `score = p·d − penalties` | penalty là **một dòng có tên** trong waterfall ⇒ giải thích được |
| Không đụng `d` | `priorityVector` và radar (§10) không đổi |
| Không thêm tham số | dùng lại `USER_CONFLICT_PENALTY` |

**Phản biện phải ghi vào ADR:** v3.1 đã **cố ý bỏ** penalty này để tránh tính phạt hai lần
(`PersonalConflictMode.None`). Lý lẽ bảo vệ L2: `personalScore` đo **mức độ hợp** (liên tục), còn
"bị khắc" là một **phạm trù kiêng kỵ** — hai đại lượng khác loại nên tách hai số hạng.
⇒ **L2 supersede một phần `personalized-recommendation-v3.1.md` §3.3.**

### 14.3 ⚠️ Đứt gãy tại `Wp = 0` — **CHỐT: chấp nhận (a)**

L2 chỉ chạy khi `PersonalBlendActive` (`Wp > 0` **và** có `PersonalVector`). Khi `Wp = 0` thì
`PlacementPolicy` rơi về `PersonalConflictMode.ByScope` — **luật v3 nguyên bản**:

| `Wp` | Hành vi với SP khắc mệnh |
|---:|---|
| 0.01 | penalty = 0.60 × 0.01 = **0.006** (gần như không phạt) |
| **0.00** | **`Private` → LOẠI CỨNG · `Shared`/`Public` → penalty ĐẦY ĐỦ 0.60** |

**Nói bằng lời:** L2 chỉ chạy khi trục cá nhân **đang bật**. `Wp = 0` không có nghĩa là *"trọng số cá
nhân rất nhỏ"* — nó là **công tắc TẮT** cả tính năng v3.1, và code rơi trọn về luật v3 cũ (loại cứng
sản phẩm khắc mệnh ở phòng `Private`). Nên hành vi ở `Wp = 0` khác hẳn `Wp = 0.01`, chứ không phải
"gần giống".

| `Wp` | Nhánh nào chạy | SP khắc mệnh ở `Private` |
|---:|---|---|
| 0.50 | v3.1 + L2 | phạt 0.300 — **còn trong danh sách** |
| 0.01 | v3.1 + L2 | phạt 0.006 — **còn trong danh sách** |
| **0.00** | **v3 cũ** | **BỊ LOẠI khỏi danh sách** |

**Tại sao chấp nhận được:** người vận hành chỉ dùng **hai trạng thái** — `0` (tắt / đường lui khi
ranking lệch) hoặc `0.50 / 0.30` (bật). **Không ai đặt `0.01`.** Đây đúng là ngữ nghĩa kill-switch mà
dự án đã dùng ở `VIBE_FILTER_HARD` (0.4 vs 0.6 khác hẳn hành vi, không phải thang liên tục) và
`POLARITY_SHARE`.

Muốn liên tục thì phải **bỏ hẳn nhánh v3** — mất luôn đường lui khi cần rollback. Không đáng.

⇒ **Nhảy bậc**, không liên tục. Hai cách hiểu:

| | Cách | Đánh giá |
|---|---|---|
| **(a)** ✅ **ĐÃ CHỐT** | Chấp nhận — `Wp = 0` nghĩa là "tắt hẳn trục cá nhân v3.1", rơi trọn về v3 | Nhất quán ngữ nghĩa **kill-switch**; ca test `SCORE-L2-03` khoá hành vi này |
| (b) | Luôn dùng `Scaled` khi có `PersonalVector` | ❌ `Wp=0` ⇒ penalty 0 ⇒ **mất hẳn lớp bảo vệ khắc mệnh** khi kill-switch tắt |

**Hệ quả riêng của `Public`:** `PERSONAL_WEIGHT_PUBLIC` cố định `0.00` ⇒ phòng Public **luôn** ở chế độ v3
⇒ vẫn trừ **đủ 0.60** dựa trên bản mệnh của một người — **mâu thuẫn** với chính nguyên tắc
*"không gian chung không neo vào bản mệnh một người"*. Đây là bất nhất **có sẵn từ v3.1**, không do L2 sinh ra.
**ĐÃ CHỐT (Q12):** ở `Public`, `PersonalConflictMode` → `None` — **không lọc, không phạt**.

### 14.4 Tương tác với NGHỀ NGHIỆP (R5)

> *"Nghề nghiệp bẻ vào vector personal → đưa vào công thức này luôn, đúng chứ?"* — **gần đúng, nhưng
> phải bẻ đúng vector.** "Vector personal" trong code là **hai** thứ khác nhau:

| Vector | Vai trò trong công thức | Bẻ được không |
|---|---|---|
| `personalVector` (Σ=1, 60/30/10) | engine **chỉ lấy `.Dominant()`** (§5) | ❌ **bẻ vào đây thì công thức nuốt mất** — trừ khi delta đủ lớn để *lật đỉnh*, mà lật đỉnh là nhảy bậc |
| **`r[e] = ruleScore(mệnh, e)`** | **thứ thật sự nhân với `productVector`** | ✅ **bẻ vào đây** — đúng phương án **N1** §11.3 |

```csharp
r'[e] = Math.Clamp(r[e] + delta[e] * ctx.Params.OccupationShare, -1m, 1m);
```

**Phân vai rõ ràng với L2 — nghề nghiệp KHÔNG đụng vào phần kiêng kỵ:**

| Đại lượng | Bản chất | Nghề nghiệp bẻ được? |
|---|---|:-:|
| `r` → `personalScore` | **liên tục** — mức độ hợp | ✅ có |
| `GetRelation(destiny, ·) == BiKhac` → `userPenalty` | **phạm trù** — kiêng kỵ | ❌ **không** |

Đúng về nghiệp vụ: **nghề nghiệp không đổi được bản mệnh**, chỉ đổi mức ưa thích. Người mệnh Mộc làm
nghề cần Kim thì Kim vẫn khắc mệnh — chỉ là bớt khó chịu, không thành hợp.

⚠️ **Ràng buộc bắt buộc:** delta nghề nghiệp **không được đưa một hành `BiKhac` lên ≥ 0**.
```csharp
if (GetRelation(destiny, e) == FengShuiRelation.BiKhac)
    r'[e] = Math.Min(r'[e], -0.1m);      // vẫn âm, chỉ nhẹ đi
```

### 14.5 ⚠️ XUNG ĐỘT với ADR `recommendation-scoring-v4-polarity.md`

ADR v4 (đã duyệt trong `adr/README.md`, **chưa code, chưa seed**) khai một công thức **giành cùng một chỗ**:

```
v3.1:  score = (1 − Wp)             · gapScore + Wp             · personalScore
v4  :  score = (1 − POLARITY_SHARE) · gapScore + POLARITY_SHARE · polarityScore
```

Cả hai đều chiếm slot `(1 − x)` trên `gapScore`. Nếu làm cả hai, **phải hợp nhất**:

| | Cách | Kết quả với `Wp=0.5`, `POLARITY_SHARE=0.2` |
|---|---|---|
| **Phẳng** | `w_room = 1 − Wp − POLARITY_SHARE` | phòng **0.3** / mệnh 0.5 / polarity 0.2 — phòng bị bóp |
| **Lồng** ⭐ | `elementScore = (1−Wp)·(ĝ·p) + Wp·(r·p)`<br>`score = (1−PS)·elementScore + PS·polarityScore` | phòng **0.4** / mệnh **0.4** / polarity 0.2 — giữ nguyên cân bằng phòng↔mệnh đã hiệu chỉnh |

⇒ **Khuyến nghị: lồng.** Polarity là **lớp ngoài**, không tranh chấp với cân bằng phòng/mệnh.

Và v4 §3.2 nhân hệ số vào chính penalty của L2:
```csharp
userPenalty = UCP × Wp × khacPolarityFactor
// khacPolarityFactor = 1.0 (chưa biết polarity) | KHAC_SAME_POLARITY 1.2 | KHAC_DIFF_POLARITY 0.6
```
⇒ **L2 và v4 §3.2 tương thích, chỉ cần nhân thêm hệ số.** Ghi rõ thứ tự nhân trong cả hai ADR.

Ngoài ra v4 §3.5 chốt: **"tuyệt đối không thêm polarity làm trục thứ 6"** trên radar — trùng khớp
nguyên tắc "không trộn hai đơn vị trên một khung" ở §10.

### 14.6 Còn phải quyết — 9 điểm

| # | Quyết định | Khuyến nghị |
|:-:|---|---|
| 1 | Tên mode mới trong `PersonalConflictMode` | **`Scaled`** (giá trị thứ 4) |
| 2 | Phạt theo `productDominant` (nhị phân) hay theo **tỉ trọng** `productVector[hànhKhắc]`? | **Nhị phân** — nhất quán với luật v3 và với `DescribePersonalAffinity` |
| 3 | Dùng lại `USER_CONFLICT_PENALTY` hay tách `PERSONAL_CLASH_PENALTY` riêng? | **Dùng lại** — nó vốn đã đúng ngữ nghĩa |
| 4 | Luồng `Carry` giữ `AlwaysHard` (loại thẳng) hay đổi sang `Scaled`? | **Giữ `AlwaysHard`** — vật đeo trên người là riêng tư tuyệt đối |
| 5 | Mode `Fit` (trang chi tiết SP) có áp penalty không? | **Có** — không loại, nhưng phải trừ + caution |
| 6 | Cần kill-switch để rollback L2 không? | **Không** — `Wp = 0` đã là kill-switch tự nhiên (§14.3a) |
| 7 | Badge đỏ "Khắc bản mệnh": chỉ `BiKhac` hay cả `TietKhi`? | **Chỉ `BiKhac`** — `TietKhi` là hao nhẹ, đã nằm trong `r` = −0.2 |
| 8 | Thứ tự nhân khi có v4: `UCP × Wp × khacPolarityFactor` | ✅ như trên |
| 9 | `Public` có nên bỏ hẳn lọc/phạt khắc mệnh không? (§14.3) | **Có** → `PersonalConflictMode.None` |

### 14.7 Ca test bổ sung

| ID | Ca | Kỳ vọng |
|---|---|---|
| `SCORE-L2-01` [Normal] | `BiKhac` · `Private` · `Wp=0.5` | `d·p − 0.300` |
| `SCORE-L2-02` [Normal] | `BiKhac` · `Shared` · `Wp=0.3` | `d·p − 0.180` |
| `SCORE-L2-03` [Boundary] | `BiKhac` · `Wp=0` | **rơi về luật v3**: `Private` loại cứng |
| `SCORE-L2-04` [Abnormal] | `TuongSinh` · `Wp=0.5` | penalty = **0** |
| `SCORE-L2-05` [Boundary] | `Carry` · `BiKhac` · `Wp=0.5` | vẫn **LOẠI CỨNG** (`AlwaysHard` không đổi) |
| `SCORE-L2-06` [Abnormal] | mode `Fit` · `BiKhac` · `Wp=0.5` | **không loại**, có penalty + caution |
| `SCORE-L2-07` [Boundary] | `OCCUPATION_SHARE=1` · nghề đẩy hành `BiKhac` lên | ✅ **XONG (P5)** — `r'[e]` vẫn **≤ −0.1** với mọi cặp (mệnh × hành khắc); penalty không đổi (`SCORE-OCC-04`) |
| `SCORE-L2-08` [Boundary] | `BiKhac` · `Public` · `Wp=0` | **không loại, penalty = 0** (Q12). Ca này phân biệt `None` với `ByScope`: `ByScope` sẽ trừ đủ 0.60 ⇒ clamp −1.000 |

### 14.8 Docs phải sửa *(đã đối chiếu bằng grep, 13 file)*

| File | Sửa gì |
|---|---|
| `docs/adr/personalized-recommendation-v3.1.md` | §3.2 `Wp`; **§3.3 bị L2 supersede một phần** — thêm mục "Superseded by" |
| `docs/adr/recommendation-scoring-v3.md` | công thức `gapScore` ÷ `(\|gap\|₁/2)` (§8) |
| `docs/adr/recommendation-scoring-v4-polarity.md` | §3.1 hợp nhất **lồng** (§14.5); §3.2 thứ tự nhân penalty |
| `docs/adr/product-placement-personal-recommendation.md` | `PlacementPolicy` thêm `PersonalConflictMode.Scaled` |
| `docs/adr/vibe-soft-scoring.md` | giá trị `VIBE_*_PENALTY` ×2 |
| `docs/adr/score-explainability-v3.2.md` | chính file này |
| `docs/adr/README.md` | bảng ADR + đánh dấu quan hệ supersede |
| `docs/api-documents/18-recommendations.md` | `breakdown` · `conflictResolution` · endpoint `fit/personal` |
| `docs/api-documents/25-scoring-config.md` | giá trị 4 penalty ×2 + `PERSONAL_WEIGHT_*` + `OCCUPATION_SHARE` |
| `docs/ard/architecture-core/04-data-and-integrations.md` | mô tả engine |
| `docs/ard/bounded-contexts/customer-care.md` | luồng chấm điểm |
| `docs/ard/bounded-contexts/workspace.md` | vector phòng |
| `seed-data/scoring-params.json` | 4 penalty ×2 · `PERSONAL_WEIGHT_*` 0.50/0.30/0.00 · `OCCUPATION_SHARE` 0.00 |

⚠️ **`docs/ard/` (25 file) vẫn CHƯA từng được commit** — `.gitignore:120` có dòng `*ard`.
Sửa 3 file `ard/` ở trên sẽ **không vào git** cho tới khi xử lý dòng đó.

## 15. Vì sao **bỏ** `VIBE_FILTER_HARD` *(Q7 — ĐÃ CHỐT: hạ về 0.00)*

`VIBE_FILTER_HARD = 1.00` khiến sản phẩm lệch vibe **bị loại trước khi tính điểm**. Năm lý do bỏ:

| # | Lý do |
|:-:|---|
| 1 | **Mâu thuẫn trực tiếp với R1.** Sản phẩm bị loại thì **không còn gì để giải thích** — user hỏi *"sao không thấy cây Kim Tiền?"* và màn hình không có một dòng nào trả lời được. Cả `breakdown` lẫn radar đều vô nghĩa với thứ không xuất hiện. |
| 2 | **Lọc theo MỘT thuộc tính đơn lẻ** trong khi mọi trục khác đã chuyển sang chấm điểm có trọng số. Một sản phẩm hoàn hảo cả về gap phòng lẫn bản mệnh vẫn bị loại chỉ vì thiếu đúng một tag vibe. |
| 3 | **Phạt "chưa khai dữ liệu" y như "lệch thật".** Trong `ScoreOne`, điều kiện là `(unknown \|\| mismatch)` — sản phẩm **chưa khai vibe nào** cũng bị loại. Đó là phạt vendor chưa nhập liệu, không phải phán quyết phong thuỷ. Chính vì thế `VIBE_UNKNOWN_PENALTY` (0.10) mới tách khỏi `VIBE_MISMATCH_PENALTY` (0.40) — nhưng ở chế độ cứng, sự phân biệt đó **bị vứt bỏ**. |
| 4 | **Có thể trả danh sách RỖNG.** Catalog chưa khai vibe đầy đủ ⇒ `POST /recommendations` trả `422 "Không có sản phẩm nào phù hợp"`. Cắt theo **điểm tổng** không bao giờ rỗng theo cách đó: nó xếp hạng rồi cắt đuôi, luôn còn phần đầu. |
| 5 | **Đã có lưới thay thế tốt hơn.** `MIN_SCORE_THRESHOLD` cắt theo điểm tổng — một sản phẩm lệch vibe nhưng cực hợp phòng + hợp mệnh vẫn sống sót, đúng như trực giác. Xem [`vibe-soft-scoring.md`](./vibe-soft-scoring.md). |

**Thao tác:** `PUT /api/admin/scoring/params/VIBE_FILTER_HARD` → `0.00`, rồi cân nhắc nâng
`MIN_SCORE_THRESHOLD` từ `−1.00` lên `0.00`. **Không cần deploy.**

⚠️ Làm **sau P2** (đối chiếu golden set), không làm cùng lúc với việc bật `PERSONAL_WEIGHT_*` — đổi hai
biến một lúc thì không biết ranking lệch vì cái nào.

---

## 16. Dữ liệu tham chiếu: khi nào seeder được đồng bộ, khi nào phải đi migration

Hai lần trong đợt này việc sửa file seed **không xuống được DB** (§4 với `workspace_types`, P2 với
`scoring_params`). Gốc chung: mọi seeder đều idempotent kiểu *"row này có chưa? có rồi thì bỏ qua"*,
nên sửa giá trị trong JSON chỉ có tác dụng với DB dựng mới.

Cám dỗ là cho tất cả seeder đồng bộ. **Sai** — nhiều bảng trong số đó người vận hành chỉnh runtime
qua API, deploy đè lên là xoá sạch quyết định của họ mà không báo. Luật phân xử:

> **Cột đó có API nào ghi vào được không?**
> **Không** → seeder là đường duy nhất ⇒ seeder ĐỒNG BỘ.
> **Có** → deploy không được đè ⇒ seeder CHỈ CHÈN; đổi giá trị nền đi bằng **data migration** có
> `WHERE` canh đúng giá trị cũ (mẫu: `ScoringPenaltiesV32`).

| Bảng / cột | API ghi được | Seeder làm gì |
|---|---|---|
| `workspace_types.Scope` · `.Description` | ❌ (`WorkspaceTypesController` chỉ `GET` + `POST` tạo loại của user) | 🟢 **đồng bộ** |
| `feng_shui_rules` (25 cặp) | ❌ *(sẽ có endpoint + UI sau)* | 🟢 **đồng bộ**, kèm guard `UpdatedBy is null` để UI tương lai không bị đè |
| `workspace_type_elements.Weight` | ✅ `PUT /api/admin/scoring/workspace-type-elements` | 🟡 đồng bộ **chỉ** row `UpdatedBy is null` |
| `scoring_params.Value` | ✅ `PUT /api/admin/scoring/params/{code}` | 🔴 chỉ chèn → migration |
| `work_purpose_element_modifiers.Delta` | ✅ `PUT /api/admin/scoring/purpose-modifiers` | 🔴 chỉ chèn → migration |
| `element_input_map.Weight` | ✅ `PUT /api/admin/scoring/element-input-tags/{kind}/{code}` | 🔴 chỉ chèn → migration |
| `styles` · `vibes` · `elements` (`Name`, `SortOrder`) | ✅ `PUT /api/{styles\|vibes\|elements}/{code}` | 🔴 chỉ chèn — xem ghi chú dưới |

**`UpdatedBy is null` là cái phân biệt "row seed" với "row admin đã sửa".** `AppDbContext.SaveChangesAsync`
chỉ gán `UpdatedBy` khi có user đăng nhập; seeder chạy ngoài HTTP context nên row nó ghi luôn để trống.
Không cần thêm cột nào.

⚠️ **`styles`/`vibes`/`elements` không dùng được guard đó**: ba entity này hiện thực `ILookup` chứ
không kế thừa `BaseEntity`, nên **không có cột `UpdatedBy`**. Muốn đồng bộ có chọn lọc thì phải thêm
cột audit trước — chưa đáng cho vài nhãn hiển thị.

⚠️ **`FengShuiRuleSeeder` trước đây chặn bằng `AnyAsync()`**: bảng có ĐÚNG MỘT dòng là cả 25 luật bị
bỏ qua vĩnh viễn. Một lần seed hỏng giữa chừng hay một cặp bị xoá tay là engine im lặng rơi về
`FengShuiCalculator.DefaultScore` cho cặp thiếu, không ai biết. Nay đối chiếu theo từng cặp.

⚠️ **`WorkspaceTypeElementSeeder` trước đây upsert vô điều kiện** dù bảng CÓ endpoint admin ⇒ mỗi lần
deploy thổi bay hiệu chỉnh vector phòng. Vector phòng quyết định gap của **mọi** gợi ý, nên mất hiệu
chỉnh ở đây là đổi ranking toàn hệ thống. Đã thêm guard.

> **Xoá sạch DB rồi seed lại** làm mọi guard trên thành vô hiệu (bảng rỗng thì không có gì để bỏ qua)
> — đó là cách nhanh nhất thoát khỏi dữ liệu lẫn lộn, nhưng nhớ: sau đó `PERSONAL_WEIGHT_*` trở về
> `0.00` từ JSON, tức **trục cá nhân lại TẮT** và vẫn phải làm P2.3.

---

# PHẦN D — KẾ HOẠCH

## P0 — Sửa dữ liệu *(0.5 ngày)*

| # | Việc |
|:-:|---|
| 0.1 | ✅ **XONG** — `seed-data/workspace-types.json`: Kitchen · Living Room · Dining Room · Home Theater · Guest Room · Altar Room → `Shared`; **kèm** `WorkspaceTypeSeeder` nay đồng bộ `Scope` cho row đã có (xem đính chính §4) |
| 0.2 | ✅ **Đã chốt: `Private`** cho Bathroom · Laundry · Garage · Balcony · Rooftop Garden — không cần sửa seed |
| 0.3 | Chạy lại seeder + `dotnet test` lấy baseline |
| 0.4 | Quyết định `.gitignore:120` `*ard` — **25 file `docs/ard/` vẫn chưa track** |

## P1 — Chuẩn hoá thang điểm *(1 ngày)* ⬅ **sửa tận gốc, làm trước**

| # | Việc | File |
|:-:|---|---|
| 1.1 | ✅ **XONG** — `gapScore` chia `\|gap\|₁ / 2` + clamp, **chỉ** nhánh `WorkspaceGap` | `Engine/RecommendationScorer.cs` |
| 1.2 | ✅ **XONG** — 4 penalty ×2. Sửa **CẢ HAI**: `seed-data/scoring-params.json` *và* default trong `ScoringParameters` (ca `SCORE-PARAM-01` khoá việc hai chỗ không được lệch) | seed · `Engine/ScoringModels.cs` |
| 1.3 | Cột `recommendations.formula_version` — entity + EF config ✅ **XONG**, **còn thiếu migration** | Infrastructure |
| 1.4 | ✅ **XONG** — viết lại kỳ vọng toàn bộ ca; bất biến A3-06 → `[−1, 1]` | `RecommendationScorerTests.cs` |
| 1.5 | ✅ **XONG** | docs |
| 1.6 | ✅ **XONG** — `PersonalConflictMode.Scaled` (`userPenalty = UCP × Wp` khi `BiKhac`, §14.2) + `None` ở `Public` (Q12). `PlacementPolicy.For`/`WorkspaceFit` nay nhận thêm `WorkspaceScope` — luật vẫn nằm trong BẢNG, `ScoreOne` không mọc nhánh `if` mới | `Engine/ScoringModels.cs` · `RecommendationScorer.cs` |
| 1.7 | ✅ **XONG** — `SCORE-L2-01..06` + `SCORE-L2-08` (§14.7). `SCORE-L2-07` đã xong ở **P5** (`OccupationScoringTests`) | `RecommendationScorerTests.cs` |
| 1.8 | Xong 10/13 doc ở §14.8; còn 3 file `docs/ard/` bị `.gitignore` chặn (P0.4) | docs · seed |

## P2 — Bật trục cá nhân *(1 ngày)*

| # | Việc |
|:-:|---|
| 2.1 | ✅ **XONG** — golden set 20 bộ ở `tests/FengDeskAI.UnitTests/RecommendationGoldenSetTests.cs`. Vector phòng lấy **thật** từ `seed-data/workspace-type-elements.json` (`Ideal` = cột `ideal`, `Current` = cột `interior` — phòng vừa tạo, chưa khai tag). Đáp án kỳ vọng tính bằng **bản cài đặt độc lập** của công thức §8/§14, không lấy từ code C# |
| 2.2 | ✅ **XONG** — mỗi bộ chấm hai lần, `Wp = 0` (baseline) và `Wp` đích của scope; cả hai thứ hạng đều là kỳ vọng cố định |
| 2.3 | ⏳ **Việc vận hành, chưa làm** — `PUT /api/admin/scoring/params/PERSONAL_WEIGHT_PRIVATE` → `0.50`, `…_SHARED` → `0.30`. Đây là quyết định **go-live**, không phải thay đổi code |
| 2.3b | ✅ **XONG** — migration `ScoringPenaltiesV32` bù ×2 penalty cho DB đã seed (xem đính chính dưới) |
| 2.4 | ✅ **XONG** — khoá bằng `GOLDEN-01/02/03`: **16/20** bộ đổi thứ hạng khi bật `Wp`; đỉnh **0.717 = 86% "Rất hợp"**; hai mệnh khác nhau ra thứ hạng khác ở `Private`/`Shared`, **giống hệt** ở `Public` |
| 2.5 | ⏳ **SAU KHI 2.3 chạy thật**: hạ `VIBE_FILTER_HARD` → `0.00` (§15), đối chiếu golden set lần hai. **Không** đổi cùng lúc với `PERSONAL_WEIGHT_*` |
| 2.6 | ⏳ Cân nhắc nâng `MIN_SCORE_THRESHOLD` `−1.00` → `0.00` sau khi vibe filter đã mềm |

> ### 🔴 Đính chính — `scoring_params` cũng **không** tự đồng bộ, nhưng cách sửa KHÁC `workspace_types`
> `ScoringParamSeeder` chỉ CHÈN code mới, y hệt `WorkspaceTypeSeeder`. Nhưng ở đây **không được** cho
> seeder đồng bộ giá trị: `scoring_params` là bảng admin chỉnh runtime qua
> `PUT /api/admin/scoring/params/{code}` — seeder ghi đè mỗi lần khởi động sẽ **xoá sạch hiệu chỉnh của
> admin**. (Với `workspace_types.Scope` thì ngược lại: không có endpoint nào sửa được nó, nên seeder là
> đường duy nhất.)
>
> ⇒ ×2 penalty đi bằng **data migration** `ScoringPenaltiesV32`, chỉ `UPDATE` row **còn nguyên giá trị
> v3.1** (`WHERE value = <cũ>`). Vừa tôn trọng hiệu chỉnh của admin, vừa idempotent với DB dựng mới.
>
> `PERSONAL_WEIGHT_*` **không** nằm trong migration này — nó là kill-switch, việc bật là quyết định vận
> hành sau khi đối chiếu golden set (2.3), không phải hệ quả của deploy.

### Kết quả golden set *(20 bộ · `Living` + `WorkPurpose.Other` để cô lập phần ngũ hành)*

| Khẳng định | Số đo | Ý nghĩa |
|---|---|---|
| Bật `Wp` đổi thứ hạng | **16/20** bộ | Trục cá nhân có tác dụng thật, không chỉ xê dịch số lẻ |
| 4 bộ **không** đổi | 2 bộ `Public` (`Wp` cố định 0) + 2 bộ hành phòng thiếu trùng hành mệnh ưa | Đúng thiết kế |
| Đỉnh baseline (`Wp = 0`) | **0.933** | Trần ±0.5 của v3.1 đã hết — và hết **nhờ chuẩn hoá mẫu số**, không nhờ cộng điểm mệnh |
| Đỉnh khi bật `Wp` | **0.717 → 86%, "Rất hợp"** | Tier cao nhất **đạt được**, thứ v3.1 bất khả thi |
| Hai mệnh cùng phòng | khác thứ hạng ở `Private`/`Shared`; **giống hệt** ở `Public` | Đúng Q12 — chỗ dùng chung không neo vào bản mệnh một người |

Baseline ở `Private` chỉ còn **4** sản phẩm chứ không phải 5: `Wp = 0` rơi về luật v3, hành khắc mệnh bị
**loại cứng** (§14.3a). Tức baseline **không** trung lập với bản mệnh — golden set ghi lại đúng điều đó
thay vì giả vờ ngược lại.

## P3 — Breakdown ở BE *(2–3 ngày)*

|  #   | Việc                                                                                                                                                                                                                     | File                                                                                                |
| :--: | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------- |
| 3.1 | ✅ **XONG** — `ScoreBreakdown` + `ScoreComponent` + `ScorePenalty` + `ConflictResolution`; `PriorityVector` là property dẫn xuất | `Engine/ScoringModels.cs` |
| 3.2 | ✅ **XONG** — `ScoreOne` dựng `ĝ` thành VECTOR rồi mới nhân (thay vì chia điểm vô hướng sau), nhờ đó `ĝ`/`r`/`d` thành đại lượng có thật để trả ra ngoài. `Breakdown` luôn được dựng, kể cả nhánh Rank | `Engine/RecommendationScorer.cs` |
| 3.3 | ✅ **XONG** — `reasonVi` cho mọi component và **mọi** penalty, kể cả `applied: false` | `Engine/RecommendationScorer.cs` |
| 3.3b | ✅ **XONG** — kèm guard `normalizedGap[roomNeed] > 0`: `Dominant()` của vector toàn 0 rơi về Thổ và sẽ báo xung khắc ma | `Engine/RecommendationScorer.cs` |
| 3.4 | ✅ **XONG** — cả 4 field. `contributions` dựng KHÔNG kèm sản phẩm đang xem (ảnh hưởng của nó đã nằm ở `previewCurrent`); phần map tách ra `CurrentBreakdownMapping` dùng chung với `element-analysis` thay vì chép | `DTOs/` + `GetProductFitAsync` |
| 3.5 | ✅ **XONG** — cần thêm `ScoreMode.PersonalFit` trong engine: `ScoreSingle` cũ luôn ép `WorkspaceFit` (chấm theo phòng) nên không dùng lại được cho Carry | Controller + Service + Engine |
| 3.6 | ✅ **XONG** — `SCORE-BD-01..05`, quét **>500 tổ hợp** scope × Wp × mệnh × hành × placement × mục đích | tests |
| 3.7 | *(vẫn hoãn)* `RecommendationItemResponse` += `breakdown`; `Gap` → `ElementAnalysisRow` | khi FE dùng list |
| 3.8 | ✅ **XONG** | docs |
| 3.9 | ✅ **XONG (Q10)** — `search_products` += `placement`, mặc định `Desk`; `Placement` xuyên suốt `ProductSearchFilter` → `ProductQueryParams` → LINQ | `Tools/SearchProductsTool.cs` · `IProductRepository.cs` · `ProductRepository.cs` · `ProductDtos.cs` · `ProductService.cs` |

## P4 — FE *(3 ngày)*

| # | Việc |
|:-:|---|
| 4.1 | ✅ **XONG — đã viết lại theo §12.7.** Lớp vàng nay là **phần đóng góp thật của chủ nhân**, đọc từ `contributions[Person]`, nhãn ghi **số phiếu**. Bỏ `personalTarget` (`T`) và lớp con nét đứt `(1−Wp)·adjustedIdeal`: khi bản mệnh đã nằm trong `current` thì vẽ `T` là đếm hai lần. `priorityVector` vẫn **ngoài radar**, để dành §10.5 |
| 4.2 | ✅ **XONG** — dấu trục đọc từ **`gap` hai chiều** (`↑` thừa đỏ · `↓` thiếu xanh), ngưỡng 0.1 dùng chung với chip. Bỏ nhãn sai "Hành đang bị khắc"; `d < 0` xuống tooltip. Tooltip thêm dòng tỉ lệ (`gấp 2.5 lần` / `còn 40% mức cần`) vì nhãn mức độ đo lệch TUYỆT ĐỐI nên bỏ qua chuyện trục đó mục tiêu to hay nhỏ |
| 4.3 | ✅ **XONG — viết lại theo §12.** Slider nay kéo **số phiếu** (0–8) chứ không phải `Wp` (%): phiếu là đại lượng gốc và ổn định, còn % đổi mỗi lần user khai thêm tag. Mô phỏng dựng lại **cả `current`** chứ không chỉ lớp vàng — chủ nhân là số hạng trong tổng, nên kéo phiếu phải làm mọi nguồn khác loãng đi; chỉ co giãn lớp vàng là vẽ hai căn phòng khác nhau trên cùng một hình. Chỉ mô phỏng tại client, không đổi tham số. Giữ chần `hasValues()` — thiếu dữ liệu thì không vẽ, thay vì vẽ một đa giác co đều vô nghĩa (bug đã vấp) |
| 4.4 | ✅ **XONG** — `ScoreWaterfall`; số lấy nguyên của BE, FE không tính lại để khỏi lệch cách làm tròn |
| 4.5 | ✅ **XONG** — chip trong `PersonalWeightControls`, bấm mở `reasonVi` |
| 4.6 | ✅ **XONG** — nhóm "Đã xét, không trừ điểm" liệt kê riêng phần `applied: false` |
| 4.7 | ✅ **XONG** — `PersonalFitPanel` là component RIÊNG, không tái dùng panel phòng: không gap, không hướng đặt, waterfall 1 thành phần. `ProductDetailPage` tự route theo `placement === "Carry"` |
| 4.10 | ✅ **XONG** — `ClashBadge` cạnh `ScoreBadge`, chỉ bật cho `BiKhac` |
| 4.11 | ✅ **XONG** — `ConflictResolutionBanner` |
| 4.8 | ✅ **XONG** — 3 trạng thái `Wp = 0` tách bạch (phòng chung · chưa có ngày sinh + CTA · tham số tắt) + trạng thái `evidenceCount = 0` + màn hình 422 của luồng Carry |
| 4.9 | ✅ **XONG** — truyền `fit.contributions` vào radar; tooltip "Đến từ" đã có sẵn, chỉ thiếu dữ liệu |

## P5 — Nghề nghiệp *(3–4 ngày, song song sau P0)*

| # | Việc |
|:-:|---|
| 5.1 | ✅ **XONG — không tách ADR riêng.** N1 đã chốt ngay trong §11.3 + §14.4; một ADR riêng chỉ để chép lại cùng một quyết định sẽ đẻ ra hai nguồn chân lý cho một công thức |
| 5.2 | ✅ **XONG** — `Occupation` + `OccupationElementModifier` (BaseEntity + unique index, cùng pattern `WorkPurposeElementModifier`) · migration `20260908172533_OccupationP5` |
| 5.3 | ✅ **XONG** — `User.OccupationId` nullable + FK `Restrict` (xóa nghề không được kéo theo tài khoản). `UpdateProfileRequest.OccupationCode`: `null` = giữ nguyên, `""` = xóa. Phân biệt hai cái vì `PUT` ghi đè cả hồ sơ — client cũ chưa biết field sẽ xóa nghề của user mỗi lần sửa SĐT |
| 5.4 | ⚠️ **XONG MỘT NỬA — chỉ seed DANH SÁCH nghề, KHÔNG seed delta.** 8 nghề trong `seed-data/occupations.json`; `occupation_element_modifiers` để **rỗng**. Danh sách nghề là taxonomy, delta là phát biểu phong thủy → phải qua chuyên gia rồi nhập bằng `PUT .../occupations/{code}/modifiers`. Bảng nháp §11.5 vẫn nằm nguyên trong ADR làm điểm khởi đầu |
| 5.5 | ✅ **XONG** — `OCCUPATION_SHARE` seed **0.00**, đã có trong `scoring-params.json` và DB local |
| 5.6 | ✅ **XONG** — `ElementDirection.ApplyOccupation` bẻ `r`; breakdown trả `baseRuleScore`, `occupationShift` (= `r' − r`, **đo SAU khi chặn**) và khối `occupation { code, nameVi, share, reasonVi }`. Lớp radar nghề nghiệp vẽ từ `occupationShift` |
| 5.7 | ✅ **XONG** — 5 endpoint dưới `/api/admin/scoring/occupations` (`ManagerOrAbove`) + `GET /api/occupations` public không kèm delta |
| 5.8 | ✅ **XONG** — `SCORE-OCC-01..06` + `SCORE-L2-07`, quét mọi cặp (mệnh × hành). 260/260 unit test xanh, golden set không đổi một chữ số nào ⇒ kill-switch đúng là byte-identical |

### Ba ranh giới P5 cố ý KHÔNG vượt qua

| | Vì sao |
|---|---|
| **Luồng `Carry` không chịu tác động** | N1 bẻ `r`, mà nhánh dụng thần không dựng `r`. Muốn nghề vào đó thì phải bẻ chính vector dụng thần — quyết định nghiệp vụ khác, chưa chốt (§11.2 chỉ nói "dùng được", không nói bẻ chỗ nào). Khoá bằng `SCORE-OCC-06` để nó là hành vi có chủ ý chứ không phải chỗ bỏ sót |
| **Nghề không chạm vào phần kiêng kỵ** | `BiKhac` bị chặn ở `−0.1` và `USER_CONFLICT_PENALTY` đọc `GetRelation` chứ không đọc `r'`. Một dòng delta gõ sai không được phép làm hệ thống gợi ý đúng thứ user phải kiêng (`SCORE-L2-07`, `SCORE-OCC-04`) |
| **`DetectConflict` đọc `r` GỐC** | Cảnh báo xung khắc là phạm trù do mệnh quyết định. Nghề cộng dương vào một hành không được phép xoá một cảnh báo đang đúng |

## Dọn dẹp

| # | Việc |
|:-:|---|
| C.1 | `GeneratePersonalAsync` bổ sung `Aspiration` · `AspirationDirections` · `RuleScores` |
| C.2 | `[Obsolete]` `WorkspaceType.IsPublic` + `PersonalWeight`; gỡ khỏi FE type |
| C.3 | Đổi tên `GapElementRow.Ideal` → `AdjustedIdeal` |
| C.4 | ✅ **ĐÃ ĐÚNG** — `SELF/SUPPORT/CHILD_SHARE` nay dựng `personalVector` cho lớp radar "Mục tiêu của bạn" (§10.3), nên chúng **thật sự ảnh hưởng hiển thị** và admin chỉnh được. Vẫn KHÔNG ảnh hưởng điểm — engine chỉ đọc `.Dominant()` (§5). **Đừng bỏ** |
| C.5 | `search_products` có thêm param `placement` không? (treo từ phiên trước) |
| C.6 | `Tools/CancelOrderTool.cs` là stub rỗng, chưa DI-register |
| C.7 | ✅ **ĐÃ SỬA** — `users.date_of_birth` là `timestamp with time zone` nhưng nhận `DateTime` Kind=Unspecified từ JSON ⇒ Npgsql ném khi lưu hồ sơ. Chuẩn hoá bằng `ValueConverter` trong `UserConfiguration` (lấy `.Date` **trước** khi đóng dấu UTC — quy đổi múi giờ sẽ lùi một ngày và ra SAI MỆNH) |

---

# PHẦN E — CÒN GÌ BẠN CẦN BIẾT

## Bảy điều dễ vấp

|  #  | Điều                                                                                           | Vì sao quan trọng                                                                                                                          |
| :-: | ---------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
|  1  | **`VIBE_FILTER_HARD = 1.00`**: sản phẩm lệch vibe bị **loại trước khi tới bước tính điểm**     | Nó **không xuất hiện** trong danh sách để mà giải thích. Muốn "giải thích mọi thứ" thì phải hạ về 0.0 và để `MIN_SCORE_THRESHOLD` làm lưới |
|  2  | **`Wp = 0` khi user chưa có `DateOfBirth`** — cứng trong `ResolvePersonalWeight`               | Phần lớn user thử lần đầu sẽ **không thấy trục cá nhân** dù phòng là Private                                                               |
|  3  | **`Consumable` bị loại khỏi mọi danh sách gợi ý**, nhưng `ScoreSingle` (Fit) **vẫn chấm**      | Trang chi tiết sản phẩm tiêu hao vẫn có điểm — cần caution rõ ràng                                                                         |
|  4  | **Aspiration lọc theo thẻ ĐÃ DUYỆT**; seed demo mới có 9 dòng                                  | Chọn mục tiêu lạ ⇒ engine bỏ lọc + trả `note` — FE **phải hiện `note` đó**                                                                 |
|  5  | **`recommendation_items.score` đã lưu** sẽ lệch sau P1                                         | Cần `formula_version`, và FE không so điểm chéo phiên bản                                                                                  |
|  6  | **Luồng `Carry` không có phòng** ⇒ `adjustedIdeal = current = Zero`, `gapScore` không dùng     | Waterfall của Carry chỉ có **1 thành phần** (`personalNeed·p`) — UI phải khác, không dùng chung component                                  |
|  7  | **`previewCurrent`** dùng cơ chế "phiếu" (`voteWeight`), không phải cộng 2 vector đã normalize | Nếu FE tự tính preview sẽ **lệch thang** với BE — luôn lấy số từ BE                                                                        |

## Cần quyết

| # | Câu hỏi | Chặn |
|:-:|---|---|
| # | Câu hỏi | **Quyết định (08/09/2026)** | Ở đâu |
|:-:|---|---|---|
| **Q1** | Chuẩn hoá `gapScore` ×2 | ✅ **Đồng ý** — mẫu số `\|gap\|₁/2`, **chỉ** nhánh `WorkspaceGap` | §8.1 · P1.1 |
| **Q2** | Nhân đôi 4 penalty theo | ✅ **Đồng ý** — 0.60 / 0.30 / 0.40 / 0.10 | §8.3 · P1.2 |
| **Q3** | Trộn ở tầng nào | ✅ **Tầng CHÊNH LỆCH** (giữ code) — tầng MỤC TIÊU không phạt được khắc mệnh | §10.1–10.2 |
| **Q4** | Lớp cá nhân trên radar | ✅ **`priorityVector`** — `Wp` đã nằm trong nó, đa giác xoay theo `Wp` (§10.3). `personalVector` bị loại vì engine chỉ đọc `.Dominant()` | §10.3–10.4 · P4.1 |
| **Q5** | Nghề nghiệp N1/N2/N3 | ✅ **N1** — bẻ vector điểm quan hệ `r`, không bẻ `personalVector` | §11.3 · §14.4 |
| **Q6** | Bathroom · Laundry · Garage · Balcony · Rooftop Garden | ✅ **`Private`** | §4 · P0.2 |
| **Q7** | Hạ `VIBE_FILTER_HARD` về 0.0 | ✅ **Có** — 5 lý do ở §15 | §15 · P2 |
| **Q8** | Nghề nghiệp bắt buộc hay tuỳ chọn | ✅ **Tuỳ chọn** trong profile — `users.occupation_id` nullable, không chặn đăng ký | §11.1 · P5.3 |
| **Q9** | Ai duyệt bảng delta theo nghề | ✅ **`ManagerOrAbove`** (cùng policy với `scoring-config`) | §11.5 · P5.7 |
| **Q10** | `search_products` param `placement` | ✅ **Thêm**, mặc định **`Desk`** khi model không truyền | P3.9 |
| **Q11** | Đứt gãy tại `Wp = 0` | ✅ **Chấp nhận (a)** — `Wp = 0` là công tắc TẮT v3.1, rơi trọn về v3. Không ai đặt 0.01 | §14.3 |
| **Q12** | `Public` bỏ lọc/phạt khắc mệnh | ✅ **Có** — `PersonalConflictMode.None` | §14.3 |
| **Q13** | Hợp nhất với v4-polarity | ✅ **Lồng** — giữ cân bằng phòng↔mệnh, polarity là lớp ngoài | §14.5 |

## Ước lượng

| Phase | Khối lượng | Chặn bởi |
|---|---|---|
| P0 | 0.5 ngày | — |
| **P1** | **1 ngày** | P0 — **làm trước tiên** |
| P2 | 1 ngày | P1 |
| P3 | 2–3 ngày | P1 |
| P4 | 3 ngày | P3 |
| P5 | 3–4 ngày | P0 + chuyên gia duyệt |

**Đường tới hạn:** P0 → **P1** → P3 → P4. P2 chen vào sau P1. P5 song song sau P0.
