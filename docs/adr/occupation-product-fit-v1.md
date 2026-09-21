# ARD — Nghề nghiệp là một TRỤC chấm điểm (N3) · v2

> **Status:** **Implemented v3.4 (2026-09-11) · BẬT 2026-09-19** — `OCCUPATION_WEIGHT = 0.20` (P6.6, migration `OccupationWeightEnable`; kết quả soát ở §9). **Supersede v1 (2026-09-10)**: v1 thiết kế một chỉ số
> độc lập không chạm ranking; v2 đưa nghề vào cả hai luồng xếp hạng vì câu hỏi nghiệp vụ thật là
> *"tôi làm nghề A hợp với những sản phẩm nào"*, không phải *"sản phẩm này hợp nghề A bao nhiêu"*.
> **Tiền đề:** engine v3.3 + P5 (`20260908172533_OccupationP5`).
> **Thay thế N1** (delta bẻ `r`, ADR v3.2 §11) — gỡ hẳn, không chạy song song.
> **Kill-switch:** 1 tham số `OCCUPATION_WEIGHT` seed `0.00` ⇒ `d` byte-identical, golden set không đổi.
> **Formula version:** `3.3` → `3.4`.

---

## 0. Ba câu hỏi, một vector

|  Mặt  | Câu hỏi                                                  | Người dùng cần gì                                         | Công thức                       |
| :---: | -------------------------------------------------------- | --------------------------------------------------------- | ------------------------------- |
| **A** | Sản phẩm X hợp **nghề nào**, bao nhiêu %?                | không cần đăng nhập, không cần phòng, không cần ngày sinh | `ô · p` cho mọi nghề đang bật   |
| **B** | Tôi làm nghề A, **vật mang theo** nào hợp?               | nghề + ngày sinh (dụng thần)                              | `d = (1−Wo)·n̂ + Wo·ô`          |
| **C** | Tôi làm nghề A, có **phòng B**, vật đặt cố định nào hợp? | nghề + phòng (bản mệnh đã nằm sẵn trong `current` qua phiếu chủ nhân, và trong `Wp·r` nếu có ngày sinh) | `d = (1−Wp−Wo)·ĝ + Wp·r + Wo·ô` |

`ô` là **cùng một** vector ở cả ba mặt; `OCCUPATION_SCORE = ô · p` là **cùng một** con số. Người dùng thấy
"hợp nghề 88%" ở trang sản phẩm thì dòng "Hợp nghề của bạn" trong waterfall gợi ý cũng là 88%.

**Mặt C — nghề độc lập với cá nhân.** Bản mệnh chủ phòng đã tác động ở hai chỗ có sẵn: (1) phiếu chủ nhân
trong `current` (`PERSON_PRESENCE_VOTES_<scope>`, glossary §3a) ⇒ `ĝ` đã "biết" chủ phòng mệnh gì; (2) trục
`Wp·r`. Nghề là trục **thứ ba**, không đọc scope, không đọc ngày sinh — một phòng hợp nghề Tài chính bao
nhiêu là tính chất của phòng × nghề, không phụ thuộc phòng đó riêng tư hay chung. Vì thế **một** trọng số
cho mọi scope **và cả hai luồng** (B, C) — không chia `PRIVATE/SHARED/PUBLIC` như `Wp`, không tách Carry.

---

## 1. Vì sao thay N1 bằng N3

P5 đã đưa nghề vào công thức bằng N1: `r′[e] = clamp(r[e] + delta[e]·OCCUPATION_SHARE)`. Ba lỗi cấu trúc,
không sửa được bằng cấu hình:

|                          | N1 (P5)                                                    | Hệ quả                                                                                                    |
| ------------------------ | ---------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| Sống nhờ trục cá nhân    | `ApplyOccupation` chỉ chạy khi `destiny != null && Wp > 0` | Khách chưa khai ngày sinh, hoặc phòng `Public` (`Wp = 0`) ⇒ nghề **không tồn tại** — trái với câu hỏi B/C |
| Tan vào `PERSONAL_SCORE` | `r′` nhân với `p` thành một số hạng                        | Không tách được dòng "Hợp nghề" trong waterfall; không có % riêng                                         |
| Không áp cho Carry       | nhánh dụng thần không dựng `r`                             | Câu hỏi B không có lời giải (P5 ghi rõ ở "Ba ranh giới", `SCORE-OCC-06`)                                  |

N3 (đã liệt kê ở ADR v3.2 §11.3, khi đó gác lại vì "thêm một tham số"): nghề là **trục thứ ba**, ngang hàng
gap phòng và bản mệnh. Cái giá là 1 row `scoring_params`; cái được là cả ba mặt A/B/C dùng chung một
đại lượng, và nghề vẫn chạy khi trục cá nhân tắt.

**Gỡ N1 hẳn**, không giữ ở share 0: một đầu vào tác động điểm qua hai đường là hai nguồn chân lý cho
một công thức — đúng lý do P5.1 từ chối tách ADR riêng.

---

## 2. Dữ liệu: hồ sơ Σ=1, delta là dẫn xuất

Chuyên gia phát biểu nghề bằng **phân bố**, không bằng delta có dấu:

> Tài chính / Kế toán: Kim 0.5 · Thủy 0.3 · Thổ 0.1 · Hỏa 0.05 · Mộc 0.05

Lưu đúng dạng đó. Delta suy ra khi cần, không mất mát:

```
δ[e] = o[e] − 0.2                 lệch so với phân bố đều · Σδ = 0 · cùng hình dạng với gap phòng
ô    = δ / (|δ|₁ / 2)             chuẩn hoá NỬA-L1 — y hệt ĝ (v3.2 §8) · mỗi trục ∈ [−1, +1]
```

FINANCE ⇒ `ô` = Kim **+0.75** · Thủy +0.25 · Thổ −0.25 · Hỏa −0.375 · Mộc −0.375.
Sản phẩm 100% Kim ⇒ `ô·p = 0.75` ⇒ **88%**. Sản phẩm 100% Hỏa ⇒ −0.375 ⇒ **31%**.

`OTHER` = 0.2 đều ⇒ `δ = 0` ⇒ `|δ|₁ = 0` ⇒ **trục tự tắt** (`ô = null`), không cần nhánh `if` cho "nghề trung tính".

### 2.1 Schema — migration `OccupationAxisN3`

```sql
-- Đổi tên, không migrate data: bảng đang RỖNG (P5 cố ý không seed delta)
ALTER TABLE occupation_element_modifiers RENAME TO occupation_element_profiles;
ALTER TABLE occupation_element_profiles RENAME COLUMN delta TO share;   -- numeric(4,3), CHECK share BETWEEN 0 AND 1
-- Σ=1 per occupation không CHECK được ở mức row ⇒ cưỡng chế ở service + seeder (ném khi lệch > 0.001)

-- scoring_params
DELETE WHERE code = 'OCCUPATION_SHARE';                                 -- N1 gỡ hẳn
INSERT OCCUPATION_WEIGHT  0.000   -- Wo, dùng cho CẢ mặt B lẫn C, mọi scope · đích 0.20 sau golden set
-- Một tham số duy nhất: phần nghề chiếm trong điểm cuối không phụ thuộc vật đặt cố định hay mang theo.
-- Vì sao đích 0.20: các hệ số chia nhau ngân sách = 1; gap phòng / dụng thần là mục đích gốc của engine,
-- nghề là khẩu vị. Wo = 0.5 làm sản phẩm Kim vào phòng đã THỪA Kim thành "trung tính" (−0.5 + 0.375)
-- chỉ vì user làm Tài chính — nghề che mất nhu cầu thật của phòng. 0.20 giữ nó ở −0.65, vẫn bị loại.
```

Entity `OccupationElementModifier` → `OccupationElementProfile { OccupationId, Element, Share }`.
`Occupation.Modifiers` → `Occupation.Profile`.

### 2.2 Seed — `seed-data/occupation-element-profiles.json`

```jsonc
{
  "_comment": "Hồ sơ ngũ hành theo nghề — BẢN NHÁP, chờ chuyên gia duyệt. Σ mỗi nghề = 1.000 (seeder ném nếu lệch > 0.001). Seeder CHỈ chèn cho nghề chưa có dòng nào; sửa sau đi qua PUT /api/admin/scoring/occupations/{code}/profile.",
  "rows": [
    { "occupation": "FINANCE",   "element": "Kim",  "share": 0.50, "why": "tiền tệ, kim loại quý, số liệu chính xác" },
    { "occupation": "FINANCE",   "element": "Thuy", "share": 0.30, "why": "luồng tiền, thanh khoản, đầu tư" },
    { "occupation": "FINANCE",   "element": "Tho",  "share": 0.10, "why": "tài sản cố định, quỹ dự trữ" },
    { "occupation": "FINANCE",   "element": "Hoa",  "share": 0.05, "why": "giao dịch điện tử, chứng khoán" },
    { "occupation": "FINANCE",   "element": "Moc",  "share": 0.05, "why": "hợp đồng, giấy tờ" },

    { "occupation": "CONSTRUCT", "element": "Tho",  "share": 0.50, "why": "đất đai, gạch đá, nền móng" },
    { "occupation": "CONSTRUCT", "element": "Moc",  "share": 0.30, "why": "vật liệu gỗ, cây cối, bản vẽ" },
    { "occupation": "CONSTRUCT", "element": "Kim",  "share": 0.10, "why": "máy móc thi công, khung sắt thép" },
    { "occupation": "CONSTRUCT", "element": "Hoa",  "share": 0.05, "why": "ánh sáng không gian, năng lượng mặt trời" },
    { "occupation": "CONSTRUCT", "element": "Thuy", "share": 0.05, "why": "cảnh quan nước, sơn thủy" }
    // IT · SALES · EDU · HEALTH · CREATIVE · OTHER — xem §2.3
  ]
}
```

### 2.3 Bản nháp 6 nghề còn lại — **CẦN CHUYÊN GIA DUYỆT**

| Nghề | Kim | Mộc | Thủy | Hỏa | Thổ | Lý do |
|---|:-:|:-:|:-:|:-:|:-:|---|
| `IT` | 0.30 | 0.10 | **0.40** | 0.15 | 0.05 | Thủy: trí tuệ, luồng dữ liệu · Kim: logic, máy móc · Hỏa: điện, màn hình |
| `SALES` | 0.30 | 0.10 | 0.20 | **0.35** | 0.05 | Hỏa: nhiệt huyết, giao tiếp · Kim: tiền, quyết đoán · Thủy: lưu thông, quan hệ |
| `EDU` | 0.05 | **0.40** | 0.30 | 0.15 | 0.10 | Mộc: tri thức, phát triển · Thủy: trí tuệ · Hỏa: truyền đạt · Thổ: nền tảng |
| `HEALTH` | 0.10 | **0.35** | 0.30 | 0.05 | 0.20 | Mộc: sinh khí, hồi phục · Thủy: thanh lọc · Thổ: nuôi dưỡng, ổn định · Kim: dụng cụ |
| `CREATIVE` | 0.05 | 0.30 | 0.20 | **0.40** | 0.05 | Hỏa: cảm hứng, biểu đạt · Mộc: sáng tạo · Thủy: linh hoạt |
| `OTHER` | 0.20 | 0.20 | 0.20 | 0.20 | 0.20 | Trung tính ⇒ `ô = null`, trục tự tắt |

`FINANCE` và `CONSTRUCT` giữ nguyên của người ra đề.

Seeder **chèn thẳng** bản nháp (khác P5.4 để trống): người ra đề đã chọn seed sẵn. Chốt an toàn nằm ở
**trọng số** — `OCCUPATION_WEIGHT` seed 0 nên hồ sơ có trong DB vẫn **chưa tác động điểm** cho tới
khi đối chiếu golden set xong và nâng qua API admin (đúng cách `PERSONAL_WEIGHT_*` đã đi).

---

## 3. N3 — cụ thể làm gì trong engine

### 3.1 `OccupationAxis` — vector `ô` + trọng số, dựng MỘT lần, không phụ thuộc sản phẩm

```csharp
// Engine/OccupationAxis.cs — thuần, không I/O
public sealed record OccupationAxis(
    string Code, string NameVi,
    ElementVector Direction,          // ô — đã clamp BiKhac (§3.2)
    ElementVector RawDirection,       // ô trước clamp — để breakdown nói "phần nghề KHÔNG kéo được"
    decimal Weight,                   // Wo đã kẹp theo §3.4
    string WeightCode)                // OCCUPATION_WEIGHT
{
    /// null khi: hồ sơ null · hồ sơ đều (|δ|₁ = 0, tức OTHER) · weight ≤ 0.
    /// null ⇒ mọi công thức rơi về đúng v3.3 — đó là kill-switch.
    public static OccupationAxis? Build(
        ElementVector? profile, FengShuiElement? destiny,
        decimal weight, string weightCode, string code, string nameVi)
    {
        if (profile is not { } o || weight <= 0m) return null;

        var delta = o.Subtract(ElementVector.Uniform);      // mỗi trục − 0.2
        decimal half = delta.L1() / 2m;
        if (half == 0m) return null;                         // OTHER

        var raw = delta.Divide(half);                        // ô ∈ [−1, +1]
        var clamped = destiny is { } mine ? ClampAgainstDestiny(raw, mine) : raw;
        return new(code, nameVi, clamped, raw, weight, weightCode);
    }
}
```

`ElementVector.Uniform` (0.2 × 5) là hằng mới — cạnh `Zero`.

### 3.2 Nghề không lật được kiêng kỵ — clamp `BiKhac`

```csharp
// hành e mà GetRelation(destiny, e) == BiKhac  ⇒  ô[e] = min(ô[e], 0)
```

Cùng nguyên tắc chặn `−0.1` của N1 (`ElementDirection.ApplyOccupation`), chuyển sang trục mới: mệnh
Mộc làm FINANCE thì Kim (+0.75 trong `ô` thô) bị chặn về **0** — nghề **không được cộng** vào hành khắc
mệnh, nhưng cũng không bị bắt trừ thêm (phần trừ đã có `r[Kim]` và `USER_CONFLICT_PENALTY`).

Không chuẩn hoá lại sau clamp: phần bị chặn **mất đi** có chủ ý, và breakdown phải nói ra
(`RawDirection` ≠ `Direction` ⇒ câu "Nghề bạn cần Kim nhưng Kim khắc mệnh Mộc — phần này không tính").

Chỉ clamp khi **biết mệnh**. Mặt A (trang sản phẩm, không user) dùng `RawDirection` — đó là tính chất
của sản phẩm × nghề, không của một người.

### 3.3 `ElementDirection` — thêm trục vào cả hai factory

```csharp
// TRƯỚC (v3.3)
d = (1 − Wp)·ĝ + Wp·r′                       // ForWorkspaceGap
d = n̂                                        // ForPersonalNeed

// SAU (v3.4)
d = (1 − Wp − Wo)·ĝ + Wp·r + Wo·ô            // ForWorkspaceGap  — r KHÔNG còn bị bẻ, N1 gỡ
d = (1 − Wo)·n̂ + Wo·ô                        // ForPersonalNeed
```

Chữ ký:

```csharp
public static ElementDirection ForWorkspaceGap(
    ElementVector gap, FengShuiElement? destiny, decimal personalWeight,
    Func<FengShuiElement, FengShuiElement, decimal> ruleScoreOf,
    OccupationAxis? occupation = null);          // thay cặp (occupationDelta, occupationShare)

public static ElementDirection ForPersonalNeed(
    ElementVector personalNeed,
    OccupationAxis? occupation = null);
```

`ElementDirection` gỡ `BaseRuleScoreVector` / `OccupationShift`, thêm `OccupationDirection` (= `ô` đã
clamp, `null` khi trục tắt). `DetectConflict` vẫn đọc `ruleScoreOf` gốc — không đổi.

**Vì sao Carry cũng dùng `ô` có dấu chứ không trộn hồ sơ `o` (Σ=1) thẳng vào `n`:** trộn
`(1−Wo)·n + Wo·o` cũng ra Σ=1 và tưởng là gọn, nhưng khi đó `OCCUPATION_SCORE` ở Carry là `o·p ∈ [0,1]`
còn ở phòng là `ô·p ∈ [−1,1]` — **cùng một mã, hai thang** — và mặt A không biết hiển thị theo thang nào.
Dùng `ô` ở mọi nơi thì "88%" là một con số duy nhất.

### 3.4 Kẹp trọng số — `Wp + Wo ≤ 1`

```csharp
decimal wo = Math.Min(paramWeight, 1m − personalWeight);    // luồng phòng
decimal wo = paramWeight;                                    // Carry — không có Wp
```

Admin nhập `PERSONAL_WEIGHT_PRIVATE = 0.9` và `OCCUPATION_WEIGHT = 0.3` thì hệ số của `ĝ` âm —
phòng bị chấm **ngược**. Kẹp ở code, log cảnh báo, không tin dữ liệu. Đây là chỗ **duy nhất** `Wo` chạm tới
`Wp`, và chỉ để giữ tổng ≤ 1 — không phải logic scope.

### 3.5 `RecommendationScorer.ScoreOne` — số hạng thứ ba

```csharp
decimal gapScore = clamp(normalizedGap · p);                        // như cũ (ĝ·p hoặc n̂·p)
decimal? personalScore = r is null ? null : clamp(r · p);           // như cũ, r GỐC
decimal? occupationScore = ô is null ? null : clamp(ô · p);         // MỚI

// Luồng phòng
blended = (1 − Wp − Wo)·gapScore + Wp·personalScore + Wo·occupationScore;
// Luồng Carry
blended = (1 − Wo)·gapScore + Wo·occupationScore;
```

`BuildComponents` thêm dòng — `ScoreComponentCodes.OccupationScore = "OCCUPATION_SCORE"`, nhãn
**"Hợp nghề của bạn"**, `Value = occupationScore`, `Weight = Wo`, `ReasonVi` qua `DescribeVectorMatch(ô, p,
"hành nghề bạn cần", "hành nghề bạn nên tránh")` + câu clamp nếu `Raw ≠ Direction`. Trọng số của
`GAP_SCORE` đổi từ `1 − Wp` thành `1 − Wp − Wo`. Σ `Contribution` = `Blended` — bất biến giữ nguyên.

Penalty **không đổi**: `USER_CONFLICT_PENALTY`, `MINOR_CLASH_PENALTY`, `DIRECTION_PENALTY`, `VIBE_*` đều
đọc `personalVector` / `GetRelation` / hướng / vibe — không đọc `ô`. Đó là cách "nghề không lật kiêng
kỵ" được cưỡng chế lần thứ hai, độc lập với clamp §3.2.

### 3.6 `ScoringContext` / `ScoreBreakdown`

| Gỡ | Thêm |
|---|---|
| `OccupationDelta`, `Params.OccupationShare`, `OccupationActive` | `OccupationProfile` (Σ=1), `OccupationWeight`, `OccupationWeightCode`; `OccupationAxis` dựng trong scorer từ 3 trường này + `destiny` |
| `BaseRuleScoreVector`, `OccupationShift`, `OccupationShare` trong breakdown | `OccupationDirection` (ô), `OccupationRawDirection`, `OccupationWeight`, `OccupationWeightCode`; giữ `OccupationCode/NameVi` |

`ScoringFormulaVersions.Current = "3.4"`.

---

## 4. Service — hai luồng + một endpoint mới

### 4.1 Luồng phòng (`GenerateAsync`, `GetProductFitAsync`)

```csharp
var occ = await LoadOccupationAsync(user, ct);      // trả (Code, NameVi, Profile Σ=1 | null)
decimal wo = Math.Min(p.OccupationWeight, 1m − personalWeight);
context = context with { OccupationProfile = occ.Profile, OccupationWeight = wo, … };
```

Không có `ResolveOccupationWeight(scope)`: `Wo` đọc thẳng một tham số, **không gate theo scope, không gate
theo ngày sinh** — khác hẳn `ResolvePersonalWeight`, và đó là toàn bộ lý do có N3.

### 4.2 Luồng Carry (`GeneratePersonalAsync`, `GetPersonalFitAsync`)

Bỏ comment "Nghề nghiệp KHÔNG áp cho luồng Carry", truyền `OccupationProfile` + `OccupationWeight = p.OccupationWeight` (không kẹp — Carry không có `Wp`).
Điều kiện **ngày sinh vẫn bắt buộc** (giữ nguyên `422` khi thiếu) — nhu cầu "chỉ theo nghề, không cần
sinh nhật" đã được mặt A trả lời; vật đeo trên người vẫn phải có căn cứ cá nhân (ADR product-placement §4).

`recommendation_logs.Detail` thêm `occupation { code, weight, directionApplied }`.

### 4.3 Mặt A — `GET /api/products/{id}/occupation-fit`

| | |
|---|---|
| Auth | `AllowAnonymous` |
| Query | `occupationCode` tuỳ chọn — bỏ trống ⇒ trả **mọi** nghề đang bật có hồ sơ, sắp giảm dần |
| Đầu vào | vector sản phẩm (tách `BuildProductVectorAsync(productId)` từ `GetProductFitAsync` hiện có) + hồ sơ mọi nghề (`GetOccupationsAsync(includeInactive:false)` + `Profile`) |
| Tính | `OccupationAxis.Build(profile, destiny: null, weight: 1, …)` ⇒ `score = clamp(RawDirection · p)` |
| `Consumable` | trả mảng rỗng + `noteVi` "hàng tiêu hao không xét phong thủy" |
| Không hồ sơ / OTHER | không có dòng cho nghề đó (không phải 50%) |

```jsonc
{
  "productId": "…", "formulaVersion": "3.4",
  "productVector": [ … ],
  "fits": [
    { "code": "FINANCE", "nameVi": "Tài chính / Kế toán", "score": 0.750, "displayPercent": 88, "tierVi": "Rất hợp",
      "direction": [ … ],   // ô
      "reasonVi": "Sản phẩm cấp Kim (+0.75) - đúng hành nghề Tài chính cần." },
    { "code": "IT", "score": 0.125, "displayPercent": 56, "tierVi": "Trung tính", … },
    { "code": "CREATIVE", "score": -0.375, "displayPercent": 31, "tierVi": "Cân nhắc", … }
  ]
}
```

Tier tái dùng ngưỡng `ScoreBadge.tierFor`; BE tính sẵn `tierVi` và `displayPercent` (`DisplayPercentOf`)
để FE không tự làm tròn lại.

### 4.4 Admin — `PUT /api/admin/scoring/occupations/{code}/profile`

Thay `/modifiers`. Body `[{ element, share }]`: mỗi `share ∈ [0,1]`, hành thiếu = 0, **Σ = 1 ± 0.001** —
lệch ⇒ `400` kèm Σ thực tế. Replace trọn gói (giữ lý do của P5: bảng là một phát biểu trọn vẹn).
`OccupationAdminDto.Modifiers` → `Profile`.

---

## 5. Frontend

| Nơi | Việc |
|---|---|
| Trang sản phẩm | ✅ `OccupationFitList`: thanh % cho từng nghề (mặt A), đặt **ngoài** cổng đăng nhập; ghim nghề của user lên đầu nếu đã khai; màu theo `tierFor` của `ScoreBadge` |
| `ScoreWaterfall` | ✅ **không sửa** — map `breakdown.components` chung, dòng `OCCUPATION_SCORE` tự hiện |
| `ElementRadarChart` | ❌ **KHÔNG thêm lớp radar** — đổi so với bản đề xuất. `ô` có dấu, radar chính là các vector Σ=1 không âm; chồng hai thang là so sai (đúng bẫy §10.3 của v3.2 đã loại `priorityVector` khỏi radar phòng). Thay bằng thanh có dấu |
| `OccupationInfluencePanel` → `OccupationDirectionPanel` | ✅ tái dùng panel thanh-có-dấu của P5, đổi nguồn sang `occupationDirection`; hành nghề muốn nâng nhưng bị chặn (raw > 0, direction ≤ 0) hiện nhãn đỏ "khắc mệnh". Gắn ở cả `ProductFitPanel` lẫn `PersonalFitPanel` |
| `PersonalFitPanel` | ✅ đã dùng `ScoreWaterfall` chung nên 2 dòng tự hiện; thêm `OccupationDirectionPanel` |
| `lib/breakdown.ts` | ✅ `combinedDirection(ĝ, r, wp, occupation?)` = `(1−wp−wo)·ĝ + wp·r + wo·ô`, kẹp `wo ≤ 1 − wp` như BE; `simulateScore` đi qua đó nên slider `Wp` đã tính trục nghề |
| Admin scoring config | ⏸ **chưa có** — FE hiện không có màn quản trị nghề (P5 cũng chưa làm). Hồ sơ sửa qua Swagger/API `PUT .../profile` |
| `types/recommendation.d.ts` | ✅ `occupationDirection`/`occupationRawDirection`; `OccupationInfluence.weight/weightCode`; `ProductOccupationFitResponse` |

---

## 6. Test

| Mã | Nội dung |
|---|---|
| `OCC-N3-01` | `OCCUPATION_WEIGHT = 0` ⇒ `RecommendationGoldenSetTests` **không đổi một chữ số**; `OccupationAxis` = null |
| `OCC-N3-02` | Hồ sơ đều (OTHER) ⇒ axis null kể cả weight > 0 |
| `OCC-N3-03` | Luồng phòng, 3 thành phần: Σ `Contribution` = `Blended`, sai số < 0.001 |
| `OCC-N3-04` | Luồng Carry, 2 thành phần: Σ `Contribution` = `Blended` |
| `OCC-N3-05` | Quét mọi cặp (mệnh × hành): hành `BiKhac` ⇒ `Direction[e] ≤ 0`, `RawDirection` giữ nguyên |
| `OCC-N3-06` | `Wp = 0.9, Wo = 0.3` ⇒ `Wo` kẹp về 0.1, hệ số `ĝ` = 0 chứ không âm |
| `OCC-N3-07` | **Không ngày sinh** (`Wp = 0`) + có nghề ⇒ trục nghề **vẫn bật** — điểm khác N1 |
| `OCC-N3-08` | Cùng phòng, cùng nghề, đổi scope `Private → Shared → Public` ⇒ `OCCUPATION_SCORE` **không đổi** (chỉ `Wp` đổi) |
| `OCC-N3-09` | Mặt A: FINANCE × sản phẩm 100% Kim ⇒ `0.750` ⇒ 88%; 100% Hỏa ⇒ `−0.375` ⇒ 31% |
| `OCC-N3-10` | Mệnh Mộc, nghề FINANCE, sản phẩm Kim: `USER_CONFLICT_PENALTY` **không đổi** so với không có nghề |
| `OCC-N3-11` | Seeder ném khi Σ ≠ 1 ± 0.001; idempotent lần 2 không chèn thêm |
| `OCC-N3-12` | `FormulaVersion = "3.4"` trong mọi breakdown |
| API | `PUT …/profile` Σ ≠ 1 ⇒ 400 · `GET …/occupation-fit` gọi được ẩn danh · `Consumable` ⇒ rỗng + note |

`SCORE-OCC-01..06` và `SCORE-L2-07` (N1) **xoá**, thay bằng bảng trên.

---

## 7. Không làm

| | Vì sao |
|---|---|
| Trục aspiration / vibe (v1 §2.1) | Nghề được định nghĩa hoàn toàn bằng ngũ hành theo người ra đề. Trong xếp hạng, gap phòng và dụng thần đã phân biệt sản phẩm cùng hành. Để lại P7 nếu mặt A tỏ ra quá thô |
| Carry thiếu ngày sinh chấm 100% theo nghề | Mặt A đã trả lời "chỉ theo nghề"; vật đeo trên người giữ nguyên tắc căn cứ cá nhân |
| Lọc / sắp xếp danh mục theo nghề | Cần batch endpoint hoặc cột tính sẵn — N+1 nếu làm ngây thơ |
| Trọng số nghề theo scope (`_PRIVATE/_SHARED/_PUBLIC`) | Hợp nghề là tính chất phòng × nghề, không phụ thuộc phòng riêng hay chung. Bản v2 đầu có chia theo scope rồi theo luồng vì bắt chước `Wp` — đã rút về **một** tham số `OCCUPATION_WEIGHT` |
| Nghề gắn vào `WorkspaceProfile` thay vì `User` | P5 đã đặt trên `User` và Carry cũng cần. Nếu sau này một phòng phục vụ nghề khác nghề chủ nhân (sảnh công ty) thì thêm `workspace_profiles.occupation_id` override — quyết định riêng |

---

## 8. File thay đổi

**Backend**
- `Domain/Entities/Recommendation/` — `OccupationElementModifier.cs` → `OccupationElementProfile.cs`; `Occupation.Modifiers` → `Profile`
- `Infrastructure/Persistence/Configurations/` — đổi tên config + bảng/cột; `AppDbContext`
- `Infrastructure/Persistence/Migrations/` — `OccupationAxisN3` (rename + 1 param + delete `OCCUPATION_SHARE`)
- `Infrastructure/Persistence/Seeding/` — `OccupationElementProfileSeeder.cs` *(mới)*, `Order` sau `OccupationSeeder`
- `seed-data/` — `occupation-element-profiles.json` *(mới)*, `scoring-params.json`, `README.md`
- `Application/Features/CustomerCare/Engine/` — `OccupationAxis.cs` *(mới)*; `ElementDirection.cs` (gỡ `ApplyOccupation`, thêm trục); `RecommendationScorer.cs` (§3.5); `ScoringModels.cs` (`ScoreComponentCodes.OccupationScore`, `ScoringParamCodes.OccupationWeight`, `ScoringContext`, `ScoreBreakdown`, version 3.4); `ElementVector.Uniform`
- `Application/Features/CustomerCare/DTOs/` — `OccupationDtos.cs` (Profile), `ScoreBreakdownDtos.cs` + `Mapping`, `OccupationFitDtos.cs` *(mới)*
- `Application/Features/CustomerCare/Services/` — `RecommendationService` (4.1, 4.2, tách `BuildProductVectorAsync`), `OccupationFitService` *(mới)*, `ScoringConfigAdminService` (profile)
- `Application/Interfaces/Repositories/IScoringConfigRepository.cs` + impl
- `WebAPI/Controllers/` — `ProductsController` (+1), `ScoringConfigController` (`/modifiers` → `/profile`)

**Frontend** — §5.

**Docs** — `docs/glossary-scoring.md` §3b/§5 (sau khi code merge, để glossary luôn tả code đang chạy); `docs/ard/bounded-contexts/customer-care.md`; `docs/api-documents/`; ADR v3.2 §11 thêm dòng "superseded bởi N3"; `docs/adr/README.md`.

---

## 9. Kế hoạch

| Phase | Việc | Ước lượng |
|:-:|---|:-:|
| P6.0 | ADR v2 này | ✅ |
| P6.1 | ✅ Migration `20260911152848_OccupationAxisN3` (DROP+CREATE vì bảng rỗng, không RENAME); entity/config; `occupation-element-profiles.json` + `OccupationElementProfileSeeder` (Σ=1, idempotent theo nghề); DTO + `PUT …/profile`; `OccupationProfileRules` dùng chung service + seeder | ✅ |
| P6.2 | ✅ `OccupationAxis`; `ElementDirection` 2 factory; `RecommendationScorer` số hạng thứ ba + `OCCUPATION_SCORE`; gỡ N1 (`ApplyOccupation`, `OccupationShift`, `OCCUPATION_SHARE`); version 3.4 | ✅ |
| P6.3 | ✅ Service: 2 luồng + `ScoringParameters.OccupationWeightFor(Wp)` + log; mặt A ở `IOccupationService.GetProductFitAsync` + `GET /api/products/{id}/occupation-fit` | ✅ |
| P6.4 | ✅ `OCC-N3-01..12` (unit) + `SCORE-OCC-01..05`, `REC-OCC-01..03` (API); golden set ở weight 0 **byte-identical** (349/349 unit) | ✅ |
| P6.5 | ✅ FE §5 (trừ form admin — chưa có màn) | ✅ |
| P6.6 | ✅ **Bật 2026-09-19.** `OccupationGoldenSetTests` chạy 20 phòng golden × 8 hồ sơ nghề seed ở `Wo = 0.20` so với 0: 160 lượt, đổi thứ tự 66, đổi top-1 35 — mọi Δ ≤ 2·Wo = 0.4, hồ sơ `OTHER` byte-identical, **không lần nào** top-1 rơi vào hành khắc mệnh (3 test giữ lại làm hàng rào). Soát 35 ca đổi top-1: đều là ca sát nút trước đó (chênh #1–#2 ≤ 0.15) và hành lên đầu đúng là hành nghề cần (IT → Thủy, CONSTRUCT → Thổ, CREATIVE/SALES → Hỏa, EDU/HEALTH → Mộc, FINANCE → Kim). Bật bằng migration `OccupationWeightEnable` (canh `value = 0`) + seed/code default 0.20 thay vì PUT tay, để mọi môi trường cùng số | ✅ |

**Lỗi phát hiện khi test P6.1:** `ReplaceOccupationModifiersAsync` của P5 nạp lại dòng con bằng `FindAsync` (`AsNoTracking`)
rồi `Remove` trong khi `GetOccupationByCodeAsync` đã track chúng qua `Include` ⇒ EF ném *"already being tracked"*.
Chưa lộ vì bảng P5 luôn rỗng. Sửa: xoá qua chính `occupation.Profile` đã track; `ToDto` lọc `IsDeleted` vì
navigation đã track vẫn giữ dòng vừa xoá mềm (`SCORE-OCC-02` khoá lại).
| — | **Tổng** | **≈ 6.5 ngày** |

Đường găng ngoài code: **chuyên gia duyệt §2.3**. Code không chờ — hồ sơ nháp đã trong DB, trọng số 0
giữ nó không tác động cho tới P6.6.
