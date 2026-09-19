/# Từ vựng chấm điểm ngũ hành — nói tên là hiểu

> Mục đích: gọi tên **đúng một** đại lượng. Cùng một câu tiếng Việt ("mệnh workspace + user") đang
> trỏ tới **ba** đại lượng khác nhau trong code, và chọn sai thì kết luận sai chứ không chỉ lệch số.
> Công thức đầy đủ + lý lẽ: [`docs/adr/score-explainability-v3.2.md`](adr/score-explainability-v3.2.md).
> Phiên bản công thức hiện tại: **`ScoringFormulaVersions.Current = "3.6"`** (3.4 = N3 nghề nghiệp là trục thứ ba; 3.5 = trần phiếu tag trong `current`; 3.6 = Carry đo "phủ − kỵ" thay tích trong).

Ký hiệu dùng xuyên suốt: `Σ=1` = vector chuẩn hoá tổng 1 · `|v|₁` = Σ|vᵢ| · 5 trục theo thứ tự enum
**Kim, Mộc, Thuỷ, Hoả, Thổ** (còn `ElementVector` là record struct khai theo thứ tự
`Tho, Kim, Thuy, Moc, Hoa` — đừng khai theo vị trí, luôn dùng tham số có tên).

---

## 1. "Mệnh của user" — 4 đại lượng KHÁC nhau

| Nói là | Tên trong code | Kiểu | Công thức |
|---|---|---|---|
| **bản mệnh**, mệnh nạp âm | `destinyElement` / `DestinyElement` | **1 hành** | `FengShuiCalculator.GetNapAmElement(GetLunarYear(dob))` |
| **vector bản mệnh** | `personalVector` | Σ=1, **không âm** | `0.6·bảnMệnh + 0.3·hànhSinhMệnh + 0.1·hànhMệnhSinh`, rồi normalize |
| **điểm quan hệ với mệnh** | `r[e]` = `ruleScore` / `RuleScoreVector` | **có dấu**, mỗi trục ∈ [−1,+1] | tra bảng `feng_shui_rules(mệnh, e)`; admin sửa được |
| **dụng thần** (vật mang theo người) | `personalNeed` / `PersonalTarget.Vector` | Σ=1, không âm | đủ **giờ sinh** → dụng thần Tứ Trụ; thiếu giờ sinh → rơi về `personalVector` |
| **kỵ thần** (v3.6) | `PersonalTarget.Avoid` / `PersonalAvoid` | **tập hành** | Tứ Trụ: thân nhược ⇒ thực thương + tài + quan sát; thân vượng ⇒ ấn + tỷ kiếp. Nạp Âm ⇒ chỉ hành khắc mệnh. Còn lại = nhàn thần (0) |

```
GetNapAmElement(lunarYear):
    canValue = { Giáp,Ất:1   Bính,Đinh:2   Mậu,Kỷ:3   Canh,Tân:4   Nhâm,Quý:5 }
    chiValue = { Tý,Sửu,Ngọ,Mùi:0   Dần,Mão,Thân,Dậu:1   Thìn,Tỵ,Tuất,Hợi:2 }
    s = canValue + chiValue;   if (s > 5) s -= 5
    s → 1 Kim · 2 Thuỷ · 3 Hoả · 4 Thổ · 5 Mộc
```

⚠️ **Năm phải là năm ÂM.** `dob.Year` thô làm người sinh tháng 1–2 trước Tết ra sai mệnh (bug đã
từng có ở 5 call site). Mọi đường đi qua `FengShuiCalculator.GetLunarYear(...)`.

⚠️ `personalVector` **không có phần âm** nên nó chỉ *nâng* mục tiêu, không bao giờ **phản đối** được
một hành khắc mệnh. Vì thế **chấm điểm đi đường `r`, không đi đường `personalVector`** (§10.2).
Tỉ lệ `0.6 / 0.3 / 0.1` là `SELF_SHARE` / `SUPPORT_SHARE` / `CHILD_SHARE` trong `scoring_params`.
Nhánh Carry còn có `CARRY_PRIMARY_SHARE` / `CARRY_SECONDARY_SHARE` chia tỉ trọng giữa các dụng thần.

---

## 2. "Mệnh của workspace" — là **vector**, và có HAI vector khác nhau

| Nói là                         | Tên trong code  | Nguồn                                         | Nghĩa                                                                        |
| ------------------------------ | --------------- | --------------------------------------------- | ---------------------------------------------------------------------------- |
| **mệnh phòng**, mức lý tưởng   | `ideal`         | `workspace_type_elements`, `Source = "Ideal"` | phòng loại này **nên** ra sao                                                |
| mức lý tưởng **theo mục đích** | `adjustedIdeal` | `normalize(ideal + Σ delta_intent)`           | ← **MỤC TIÊU thật sự dùng để chấm**                                          |
| **nền phòng**                  | `interior`      | cùng bảng, `Source = "Interior"`              | phòng loại này **thường** ra sao ⇒ đi vào `current`, **không** phải mục tiêu |

`delta` lấy từ `work_purpose_element_modifiers` theo `WorkPurpose` user khai, **được phép âm**.
Loại phòng chưa seed `Interior` → nền = phân bố đều `0.2` mỗi hành (để không hành nào bằng 0).

Muốn một hành duy nhất cho tiêu đề thì đó là `adjustedIdeal.Dominant()`.

⚠️ **`dominantNeed` KHÔNG phải mệnh phòng.** Nó là `gap.Dominant()` — hành phòng **thiếu nhất**, tức
thứ cần **mua thêm**. Hai khái niệm này lệch nhau thường xuyên: phòng mệnh Kim mà đã thừa Kim thì
`dominantNeed` là một hành khác hẳn.

---

## 3. "Mệnh workspace + user" — ba đại lượng, phải nói rõ cái nào

### 3a. `current` — mệnh user nằm TRONG hiện trạng phòng *(§12 + §17 + §19)*

Cách trộn **mặc định**, và là cái đi vào mọi con số trên màn hình phòng:

```
m[e]    = 3·interior[e]                          ← INTERIOR_PRIOR_VOTES
        + k·Σ (tag user khai) vᵢ·wᵢ[e]           ← mỗi tag ≈ 1 phiếu; k = min(1, TAG_VOTES_CAP / Σ phiếu tag)  (v3.5)
        + phiếuChủNhân · personalVector[e]        ← PERSON_PRESENCE_VOTES_<scope>
        + Σ (sản phẩm ĐÃ GIAO) voteWeight·vector[e]
current = normalize( m^α )                        ← α = EVIDENCE_SATURATION_ALPHA = 0.60
```

Mệnh chủ nhân là **một nguồn phiếu**, ngang hàng nền phòng và tag — nên nó **loãng dần** khi user
khai thêm tag thật. Không phải tỉ trọng % cố định.

**Phiếu của một tag** = `Σ weight` các dòng `element_input_map` của code đó (`RawSum(...).L1()`), *không*
phải hằng 1 — "≈ 1 phiếu" chỉ đúng khi seed ở `weightScale = 1.0`. **Tổng phiếu tag có trần** (v3.5,
`TAG_VOTES_CAP = 5`): vượt trần thì *mọi* tag nhân cùng hệ số `k = cap / Σ` — tag là nguồn duy nhất không
có trần, nên không cap thì 20 tag đè nền (3) + chủ nhân (3) + sản phẩm (1/món) về gần 0. Cap 5 = tag tối đa
ngang *nền + chủ nhân*; đây là sửa §12 có chủ đích ("bằng chứng lấn át" → "bằng chứng tối đa ngang prior").
`EvidenceCount`/`Confidence` vẫn đếm bằng chứng thật; response có `tagVotesScale` = `k` để FE ghi chú.
Bất biến tỉ lệ của §17 chỉ còn đúng dưới trần. ADR [`current-tag-votes-cap-v3.5.md`](adr/current-tag-votes-cap-v3.5.md). **Phiếu của một sản phẩm** đặt
trong phòng = `Σ weight` các code `DecorItem` gắn cho sản phẩm, **không gắn thì mặc định `1.0` cứng
trong code** (`PlacedProductVectorBuilder.Build` và bản sao ở `RecommendationService.GetProductFitAsync`).
**Không có tham số `scoring_params` nào riêng cho phiếu sản phẩm** — xem §7.2.

`α` là nén tương phản (luật lũy thừa Stevens): khai 100 tag một hành thì hành đó **không** áp đảo
tuyến tính nữa. `α = 1` là tuyến tính. Nén **bất biến theo tỉ lệ** — nhân đôi mọi phiếu ra đúng cùng
`current` (khác `log`, và đó là lý do chọn nó thay `log`).

⚠️ Chỉ hàng **đã giao** vào `current`. Hàng đang giao chỉ vào `previewCurrent`.

### 3b. `d` = `combinedDirection` — cái THẬT SỰ chấm điểm *(§9/§10)*

```
gap = adjustedIdeal − current              (Σ=0; dương = thiếu, âm = thừa)
ĝ   = gap / (|gap|₁ / 2)                   mỗi trục ∈ [−1,+1]
ô   = (profile_nghề − 0.2) / (|·|₁ / 2)    mỗi trục ∈ [−1,+1]; ô[e] = min(ô[e], 0) khi e khắc mệnh
d   = (1 − Wp − Wo)·ĝ + Wp·r + Wo·ô
```

Chia **nửa** chuẩn L1 vì `gap` có Σ=0 nên nửa dương đúng bằng `|gap|₁/2`; chia cả `|gap|₁` thì điểm
kẹt trần `±0.5`. `ô` dựng cùng cách từ `δ = profile − 0.2` (cũng Σ=0) nên ba số hạng cùng thang.

**Nghề nghiệp là trục THỨ BA** (v3.4 / N3, ADR [`occupation-product-fit-v1.md`](adr/occupation-product-fit-v1.md)),
không còn bẻ `r` như P5/N1. `r` là điểm quan hệ với mệnh **nguyên bản**. `Wo` = `OCCUPATION_WEIGHT`
— **một** tham số cho mọi scope và cả hai luồng, kẹp `Wo ≤ 1 − Wp` ở luồng phòng; seed **0.20** (bật 19/09,
`0` = kill-switch). Nghề `OTHER` (hồ sơ 0.2 đều) ⇒ `ô = null` ⇒ trục tự tắt. Hành khắc mệnh bị
chặn về ≤ 0 trong `ô` — nghề đổi mức *ưa thích*, không đổi được bản mệnh.

### 3c. `T` = `personalTarget` — CHỈ hiển thị, ĐÃ BỎ khỏi radar

```
T = (1 − Wp)·adjustedIdeal + Wp·personalVector
```

Không dùng chấm điểm (lý do ở §1: `personalVector` không âm). Và từ khi mệnh chủ nhân đã thật sự nằm
trong `current` thì vẽ thêm `T` là **đếm ảnh hưởng bản mệnh hai lần** — nên lớp vàng trên radar phòng
hiện là **"Phần của bạn"**: phần đóng góp của chủ nhân **bên trong** chính lớp `Hiện tại`, Σ = đúng
`sharePercent` của chủ nhân, **không** phải 1.

### `Wp` — `personalWeight`, quyết định 3b nặng nhẹ bao nhiêu

| Scope | Tham số | Mặc định |
|---|---|---|
| Private | `PERSONAL_WEIGHT_PRIVATE` | `0.30` — v3.5 hạ từ 0.50: ở 0.50 mệnh gánh nửa điểm, sản phẩm phòng đã thừa vẫn "Phù hợp" (§7.4) |
| Shared | `PERSONAL_WEIGHT_SHARED` | `0.20` — v3.5 hạ từ 0.30 |
| Public | `PERSONAL_WEIGHT_PUBLIC` | `0.00` — không gian chung không neo vào mệnh của riêng ai |

`Wp = 0` khi user **chưa có ngày sinh**, bất kể scope. Lúc đó `d ≡ ĝ`, trục cá nhân tắt hoàn toàn.

---

## 4. "% tương thích" — vòng tròn cạnh tên workspace trên FE

```
compatibilityPercent = round( 100 · (1 − |gap|₁ / 2) )        gap = adjustedIdeal − current
```

`adjustedIdeal` và `current` đều Σ=1 nên `|gap|₁ ∈ [0, 2]` ⇒ kết quả tự nằm trong `[0, 100]`,
**không** cần clamp. `|gap|₁/2` chính là **khối lượng phải chuyển** để biến `current` thành
`adjustedIdeal` (total variation distance) — nên 100% = trùng khít, 0% = hai vector không chung một
hành nào.

| | |
|---|---|
| BE | `WorkspaceProfileService.GetElementAnalysisAsync` |
| Response | `WorkspaceElementAnalysisResponse.compatibilityPercent` |
| FE | `CompatibilityRing` trong `features/users/pages/ProfileWorkspace.tsx` |
| Bản có hàng đang giao | `previewCompatibilityPercent` (cùng công thức, `current` → `previewCurrent`) |

⚠️ **Đây KHÔNG phải "% hợp bản mệnh".** Phía **mục tiêu** chỉ bẻ theo **mục đích làm việc**; bản mệnh
user nằm ở phía **`current`**. Hệ quả có thật và dễ gây thắc mắc: chủ phòng mệnh Kim, phòng đã thừa
Kim ⇒ phiếu của chủ nhân **đẩy `current` xa `adjustedIdeal` hơn** ⇒ **% tương thích giảm**. Đọc nó là
*"phòng đang giống mức lý tưởng của loại phòng + mục đích bao nhiêu"*.

---

## 5. Điểm của MỘT sản phẩm

```
score          = round( clamp( d·p − penalties, −1, +1 ), 3 )
displayPercent = (score + 1) / 2 · 100
```

`p` = vector sản phẩm (Σ=1). `d·p` là tích trong — cùng dấu nghĩa là sản phẩm bù đúng hướng.

⚠️ `p` **không nên thuần một hành** (`1.000`). Ngũ hành gán hành cho vật qua nhiều kênh (chất liệu, màu,
hình, công năng) nên vật thật luôn có hành chủ + hành phụ; `p` thuần chỉ ra khi khai thiếu kênh, và khi đó
`d·p = d[e]` chạm biên ±1 ⇒ điểm 100 % / 0 % — engine nói quá lời. Seeder demo cảnh báo
(`DemoProductFengShuiSync.Audit`); sản phẩm tạo tay thì khai thêm chất liệu đế/chậu/dây, màu, hình.

| Trừ điểm | Tham số | Khi nào |
|---|---|---|
| khắc bản mệnh (hành trội) | `USER_CONFLICT_PENALTY` | hành trội của sản phẩm khắc mệnh |
| khắc bản mệnh (phần phụ) | `MINOR_CLASH_PENALTY × clashShare` | §18 — hành trội **không** khắc, nhưng phần còn lại có; `clashShare` = Σ tỉ trọng các hành khắc mệnh |
| hướng đặt | `DIRECTION_PENALTY` | hướng đặt xấu theo Bát Trạch |
| lệch cảm hứng không gian | `VIBE_MISMATCH_PENALTY` / `VIBE_UNKNOWN_PENALTY` | phong cách sản phẩm ≠ phong cách phòng |

`MIN_SCORE_THRESHOLD` lọc bỏ sản phẩm trước khi xếp hạng.

**Vật mang theo người** (`ProductPlacement.Carry`) đi nhánh riêng: mục tiêu là **dụng thần**, `Wp`
không áp (mục tiêu vốn đã 100% cá nhân), **không** xét phòng lẫn hướng đặt. Từ v3.6 nó **không còn là tích
trong** (ADR [`personal-need-v3.6.md`](adr/personal-need-v3.6.md)):

```
needCover = Σ_e min(n̂[e], p[e])      phần nhu cầu được phủ — cùng phép đo với compatibilityPercent (1 − |a−b|₁/2)
avoidHit  = Σ_{e ∈ kỵ} p[e]          phần sản phẩm rơi vào kỵ thần
personal  = needCover − avoidHit      ∈ [−1, 1]; khớp hoàn hảo = 1 (100 %), toàn kỵ = −1 (0 %)
blended   = (1 − Wo)·personal + Wo·(ô·p)
```

Lý do đổi: `n̂·p` với hai vector Σ=1 không âm kẹt trần `max(n̂) = 0.6` — vật khớp hoàn hảo chỉ 76 %, không bao
giờ "Rất hợp"; và vật toàn kỵ thần vẫn "trung tính". `MINOR_CLASH_PENALTY` giữ nhưng bỏ qua hành đã nằm trong
kỵ (không trừ hai lần). Waterfall có hai dòng: `PERSONAL_NEED_SCORE` (+phủ) và `PERSONAL_AVOID_SCORE` (−kỵ).

**"Sản phẩm này hợp nghề X bao nhiêu %"** (trang sản phẩm, không cần đăng nhập) = `ô · p` với `ô` thô
(không có mệnh để chặn), qua cùng `displayPercent` — chính là `value` của dòng `OCCUPATION_SCORE` trong
waterfall. Endpoint `GET /api/products/{id}/occupation-fit`. Luật theo `ProductPlacement` nằm trong **bảng** `PlacementPolicy`
(`ScoringModels.cs`) — thêm luật là thêm một dòng bảng, không rải `switch`.

---

## 6. Tra nhanh: câu tiếng Việt → tên trong code

| Nói | Là |
|---|---|
| mệnh tôi / bản mệnh | `destinyElement` (1 hành) |
| vector mệnh | `personalVector` (Σ=1, không âm) |
| hành này hợp/khắc mệnh tôi bao nhiêu | `r[e]` (có dấu) |
| tôi đang cần hành gì (vật mang theo) | `personalNeed` / dụng thần |
| tôi nên tránh hành gì (vật mang theo) | `PersonalTarget.Avoid` / kỵ thần (v3.6) |
| mệnh phòng / mức lý tưởng | `ideal` → **`adjustedIdeal`** |
| nền phòng | `interior` (3 phiếu, vào `current`) |
| phòng đang thế nào | `current` |
| phòng thiếu gì | `gap` (Σ=0) · `dominantNeed` = `gap.Dominant()` |
| thiếu bao nhiêu, đã chuẩn hoá | `ĝ` = `normalizedGap` |
| trọng số bản mệnh | `Wp` = `personalWeight` |
| mệnh phòng trộn mệnh user, để **chấm điểm** | **`d`** = `combinedDirection` |
| mệnh phòng trộn mệnh user, trong **hiện trạng** | phiếu chủ nhân trong **`current`** |
| mệnh phòng trộn mệnh user, để **vẽ** (đã bỏ) | `T` = `personalTarget` |
| hệ thống đang ưu tiên bù hành nào | `priorityVector` = `normalize(max(d, 0))` |
| % tương thích (vòng tròn cạnh tên phòng) | `compatibilityPercent` |
| điểm sản phẩm | `score` ∈ [−1,+1] · `displayPercent` ∈ [0,100] |
| nghề tôi cần hành gì | `ô` = `occupationDirection` (có dấu, đã chặn khắc mệnh) · hồ sơ gốc Σ=1 ở `occupation_element_profiles` |
| trọng số nghề | `Wo` = `OCCUPATION_WEIGHT` |
| sản phẩm hợp nghề bao nhiêu | `OCCUPATION_SCORE` = `ô · p` · trang sản phẩm: `GET /products/{id}/occupation-fit` |
| phiếu của một tag | `Σ weight` code đó trong `element_input_map` (`CurrentContribution.Votes`) |
| phiếu của sản phẩm đặt trong phòng | `PlacedProductVector.VoteWeight` = `Σ weight` code `DecorItem`, mặc định `1.0` — **chưa có tham số riêng** |
| nén tương phản | `α` = `EVIDENCE_SATURATION_ALPHA` |
| phần của bạn (lớp vàng radar) | đóng góp của chủ nhân trong `current` |

---

## 7. Bản đồ công thức — từ điểm tổng xuống từng số: hiện ở đâu, code ở đâu

Đọc từ trên xuống: mỗi dòng dưới là một số hạng của dòng trên. Cột **FE** là chỗ con số đó *thật sự*
được vẽ; cột **code** là nơi nó được tính (BE) — sửa công thức thì sửa ở đó, không sửa ở FE.

### 7.1 Công thức tổng (luồng phòng, `Desk`/`Living`, `ScoringFormulaVersions.Current = 3.5`)

```
score = round( clamp( blended − P_user − P_dir − P_vibe , −1, +1 ), 3 )          RecommendationScorer.ScoreOne
blended = (1 − Wp − Wo)·(ĝ·p)  +  Wp·(r·p)  +  Wo·(ô·p)                          ── 3 số hạng CỘNG
          └── GAP_SCORE ──┘      └ PERSONAL_SCORE ┘  └ OCCUPATION_SCORE ┘        ScoreComponentCodes

ĝ  = gap / (|gap|₁ / 2)            gap = adjustedIdeal − current                  ElementDirection.ForWorkspaceGap
r  = ruleScore(mệnh, e)            bảng feng_shui_rules                           ScoringContext.RuleScoreOf
ô  = clampKhắcMệnh( (profile_nghề − 0.2) / (|·|₁/2) )                             OccupationAxis.Build
p  = vector sản phẩm Σ=1                                                          ProductVectorProvider.Build

current = normalize( m^α ),  m[e] = 3·interior[e]                                 WorkspaceVectorBuilder.BuildCurrentBreakdown
                                  + k·Σ_tag votes_tag · v_tag[e]    k = min(1, TAG_VOTES_CAP/Σ)
                                  + votes_chủNhân · personalVector[e]
                                  + Σ_sp   voteWeight_sp · p_sp[e]     (chỉ hàng đã giao)
adjustedIdeal = normalize( ideal + Σ delta_mụcĐích )                              WorkspaceVectorBuilder.ApplyIntent
```

Carry (v3.6): `blended = (1 − Wo)·(Σ min(n̂,p) − Σ_{kỵ} p) + Wo·(ô·p)` (`PERSONAL_NEED_SCORE` + `PERSONAL_AVOID_SCORE` + `OCCUPATION_SCORE`), không `P_dir`,
không phòng — `ElementDirection.ForPersonalNeed`.

### 7.2 Từng số hạng — nguồn, tham số, hiện ở đâu

| # | Đại lượng | Tính ở (BE) | Tham số `scoring_params` | Trả về qua | FE vẽ ở |
|---|---|---|---|---|---|
| 1 | `score` / `displayPercent` | `RecommendationScorer.ScoreOne` → `ScoreSingle` | — | `ProductFitResponse.score`, `RecommendationItem.score` | `ScoreBadge` (trang sản phẩm, card gợi ý); `tierFor` chia mức |
| 2 | `blended` + 3 số hạng `GAP_SCORE` / `PERSONAL_SCORE` / `OCCUPATION_SCORE` (value × weight) | `RecommendationScorer.BuildComponents` | `PERSONAL_WEIGHT_<scope>`, `OCCUPATION_WEIGHT` | `breakdown.components[]` | `ScoreWaterfall` (bảng thác nước) — mỗi dòng = một số hạng, tổng = `blended` |
| 3 | `P_user` (khắc mệnh) | `ScoreOne` — `UserConflictPenalty` khi hành trội khắc, `MinorClashPenalty × clashShare` khi phần phụ khắc | `USER_CONFLICT_PENALTY`, `MINOR_CLASH_PENALTY` | `breakdown.penalties[]` (`applied=false` vẫn trả) | `ScoreWaterfall` dòng trừ; `ClashBadge` cạnh `ScoreBadge`; `ConflictResolutionBanner` |
| 4 | `P_dir` (hướng đặt) | `ScoreOne`, theo `PlacementPolicy.DirectionMode` | `DIRECTION_PENALTY` | `breakdown.penalties[]` | `ScoreWaterfall` |
| 5 | `P_vibe` (lệch cảm hứng) | `ScoreOne` | `VIBE_MISMATCH_PENALTY`, `VIBE_UNKNOWN_PENALTY`, `VIBE_FILTER_HARD` | `breakdown.penalties[]` | `ScoreWaterfall` |
| 6 | `d` = `combinedDirection`, `priorityVector = normalize(max(d,0))` | `ElementDirection.ForWorkspaceGap` / `WithOccupation` | `Wp`, `Wo` | `breakdown.vectors.combinedDirection`, `.priorityVector` | `ProductFitPanel` "hệ thống đang ưu tiên bù hành nào"; `PersonalWeightControls` kéo `Wp` mô phỏng lại **ở FE** bằng `lib/breakdown.ts::combinedDirection` (cùng công thức, không gọi API) |
| 7 | `ĝ` = `normalizedGap` | `ElementDirection.ForWorkspaceGap` | — | `breakdown.vectors.normalizedGap` | `ElementBars` biến thể fit — nhãn "Bù tốt"/"Thêm thừa" do `ProductFitPanel` gán theo dấu `gap[e]` và `GAP_THRESHOLD` |
| 8 | `r` = `ruleScoreVector` | `ScoringContext.RuleScoreOf` → `FengShuiRuleSeeder` (25 luật) | admin sửa `feng_shui_rules` | `breakdown.vectors.ruleScore` | `PersonalFitPanel` / `ProductFitPanel` thanh có dấu "hợp/khắc mệnh" |
| 9 | `ô` = `occupationDirection` (đã chặn) / `occupationRawDirection` | `OccupationAxis.Build` | `OCCUPATION_WEIGHT` + bảng `occupation_element_profiles` | `breakdown.vectors.occupationDirection`, `breakdown.occupation{code,nameVi,weight}` | `OccupationDirectionPanel` (thanh có dấu, nhãn đỏ "khắc mệnh" khi bị chặn) |
| 10 | `ô·p` **không** mệnh, **không** phòng (mặt A) | `OccupationService.GetProductFitAsync` | — (hồ sơ nghề) | `GET /products/{id}/occupation-fit` → `fits[]` | `OccupationFitChips` (top 3, fill = %) + `OccupationFitList` |
| 11 | `p` = vector sản phẩm | `ProductVectorProvider.Build` (override → DecorItem → Material/Color → primary/secondary) | `MATERIAL_SHARE`, `COLOR_SHARE`, `FALLBACK_PRIMARY/SECONDARY` | `ProductFitResponse.productVector`, `product.elements` | `ElementBars` "Sản phẩm"; popover `OccupationFitChips` cột "Sản phẩm" |
| 12 | `ideal`, `adjustedIdeal` | `WorkspaceVectorBuilder.BuildIdeal` / `ApplyIntent` | bảng `workspace_type_elements` (Ideal), `work_purpose_element_modifiers` | `gap[].ideal`, `.adjustedIdeal`; `WorkspaceElementAnalysisResponse` | Radar lớp **"Mức lý tưởng"** (`ElementRadarChart`) |
| 13 | `current` | `WorkspaceVectorBuilder.BuildCurrentBreakdown` (một hàm cho radar phòng, engine xếp hạng, trang fit) | `INTERIOR_PRIOR_VOTES`, `PERSON_PRESENCE_VOTES_<scope>`, `EVIDENCE_SATURATION_ALPHA`, **`TAG_VOTES_CAP`**, `SELF/SUPPORT/CHILD_SHARE` | `gap[].current`; `contributions[]`, `evidenceCount`, `confidence` | Radar lớp **"Hiện tại"**; tooltip từng trục = `contributions` chia theo `ShareOf`; **"Phần của bạn"** = phần chủ nhân **bên trong** "Hiện tại" |
| 14 | phiếu tag | `BuildCurrentBreakdown` bước 2 — `Σ weight` code trong `element_input_map` | `weightScale` **lúc seed** + admin `PUT /admin/scoring/element-input-tags/{kind}/{code}` | `contributions[].votes` | tooltip radar (`%` theo nguồn); admin `ElementInputTagRow` |
| 15 | phiếu sản phẩm đã đặt | `PlacedProductVectorBuilder.Build` — `Σ weight DecorItem`, **mặc định `1.0` cứng** | **không có** | `contributions[]` (`source = Product`), `placements[]` | tooltip radar; `WorkspacePlacementSection` |
| 16 | `previewCurrent` / `previewGap` | `RecommendationService.GetProductFitAsync` — `BuildCurrentBreakdown(placed ∪ {sản phẩm đang xem, voteWeight})`; phòng: `previewCurrent` = tính cả hàng **đang giao** | như #13, #15 | `gap[].previewCurrent`, `WorkspaceElementAnalysisResponse.previewCurrent` | Radar nét đứt **"Xem trước"** (trang sản phẩm: "nếu thêm sản phẩm này"; trang phòng: "hàng đang giao") |
| 17 | `compatibilityPercent` | `WorkspaceProfileService.GetElementAnalysisAsync` — `100·(1 − |gap|₁/2)` | — | `compatibilityPercent`, `previewCompatibilityPercent` | `CompatibilityRing` cạnh tên phòng (`ProfileWorkspace`) |
| 18 | insight lời khuyên | `SpaceInsightBuilder` từ `gap` + `contributions` | `GAP_THRESHOLD` (FE) | `insights[]` | `SpaceInsightList` |
| 19 | `n̂` = dụng thần + kỵ thần (Carry) | `PersonalTargetBuilder` (Tứ Trụ → fallback Nạp Âm); `BaTuCalculator.UnfavorableElementCodes` | `CARRY_PRIMARY/SECONDARY_SHARE` | `PersonalFitResponse.personalNeedVector`, `.personalAvoidElements`, `breakdown.vectors.personalNeed` | `PersonalFitPanel` (thẻ dụng thần + chip kỵ, ✕ trên trục, tooltip phủ/kỵ) |
| 20 | `needCover`, `avoidHit` (Carry, v3.6) | `RecommendationScorer.NeedCover` / `AvoidHit` | — | `breakdown.components[]` mã `PERSONAL_NEED_SCORE` / `PERSONAL_AVOID_SCORE` | `ScoreWaterfall` — hover ra `min(cần, cấp)` và `−cấp[kỵ]` theo hành |

### 7.3 Vì sao "khai nhiều tag thì sản phẩm nhích không đáng kể" — và nút vặn nằm ở đâu

`current` là **trung bình theo phiếu** (rồi nén `α`). Sản phẩm đang xem vào `previewCurrent` với đúng
**1 phiếu** (không gắn `DecorItem`) — cùng cỡ với *một* tag. Phòng 8 tag + nền 3 + chủ nhân 3 = 14 phiếu
⇒ sản phẩm chiếm 1/15 khối lượng thô, radar nhích cỡ 6 điểm % trên trục nó bù. Đó là **thiết kế** (§12:
bằng chứng lấn át, một món đồ không thể đảo hiện trạng một căn phòng đầy đồ), nhưng cỡ phiếu của sản
phẩm hiện **không chỉnh được ở runtime** vì:

1. **Không có tham số riêng** — `1.0m` viết cứng ở **hai** chỗ (`PlacedProductVectorBuilder.Build` và
   bản chép trong `RecommendationService.GetProductFitAsync`). Đổi phải sửa code + ra bản mới.
2. Nút gián tiếp duy nhất là gắn `DecorItem` cho sản phẩm (phiếu = `Σ weight` code) — nhưng hiện
   **0 sản phẩm** gắn, và weight ấy dùng chung với tag phòng cùng tên nên không thể "tăng riêng cho sản phẩm".

⚠️ **Bẫy dữ liệu đang có thật (dev DB, 2026-09-19):** `seed-data/element-input-map.json` đặt
`weightScale: 8.0` (commit `9fdf853`, 2026-09-03) trong khi seeder **không** cập nhật weight của dòng đã
có ⇒ tag seed trước 09-03 nặng `≤ 1` phiếu, tag seed sau nặng **8** phiếu (`Laptop`, `OfficeChair` = 8 phiếu
mỗi cái). Phòng "Bàn học gỗ" 8 tag = **22 phiếu**, sản phẩm 1 ⇒ nhích ~4 điểm %. Nền phòng `3` và chủ nhân
`3` phiếu được cân trên giả định **tag ≈ 1 phiếu**; scale 8 phá cân đó cho *mọi* nguồn, không riêng sản phẩm.
**Đã sửa** bằng migration `20260919070344_ElementInputMapWeightScaleRevert` (`weight/8` cho mọi dòng `> 1.0`,
`weightScale` về `1.0`, gỡ luôn 7 dòng `BUDGET_*` mồ côi trong `scoring_params`). Từ đây tag ≈ 1 phiếu như §3a
giả định.

### 7.4 Điểm sản phẩm và đồ thị có thể nói KHÁC nhau ở đâu — audit 2026-09-19

Vòng sinh/khắc, nạp âm, điểm quan hệ `r`, `personalVector`, chặn khắc mệnh của `ô` — **đúng** (kiểm lại
`FengShuiCalculator.Generates/Controls/GetRelation/DefaultScore`, `OccupationAxis.ClampAgainstDestiny`).
Cái lệch không nằm ở ngũ hành mà ở chỗ **điểm và đồ thị đo hai thứ khác nhau**:

| Đồ thị / con số | Đo cái gì | Có mệnh không | Có nghề không |
|---|---|---|---|
| Radar "Mức lý tưởng" vs "Hiện tại", `compatibilityPercent`, nhãn "Bù tốt / Thêm thừa" | **phòng** so với mức lý tưởng của loại phòng + mục đích | mệnh chủ nhân nằm ở phía `current` (một nguồn phiếu), **không** ở phía mục tiêu | không |
| `score` / `ScoreBadge` / `displayPercent` | `d·p` với `d = (1−Wp−Wo)·ĝ + Wp·r + Wo·ô` — **phòng + mệnh + nghề** trộn theo trọng số | có, `Wp` | có, `Wo` |
| Radar lớp "Ưu tiên của bạn" (`priorityVector`) | phần dương của chính `d` | có | có |
| Chip "Hợp với nghề" (trang sản phẩm) | `ô·p` **thô** — chỉ nghề | không | chỉ nghề |

Ba ca lệch có thật, đo trên phòng "Bàn học gỗ" (chủ nhân Kim, Private, `Wp = 0.5` — **trước** v3.5):

1. **"61% Phù hợp" nhưng radar "Thêm thừa", `compat` 88 → 86.** Sản phẩm 100% Kim: `ĝ·p = −0.57`
   (phòng thừa Kim) nhưng `r·p = +1.0` (tỷ hòa mệnh Kim) ⇒ `blended = 0.5·(−0.57) + 0.5·1.0 = +0.21`.
   Ở Private, **một nửa** điểm là mệnh, nên mệnh thắng phòng. Đây là *thiết kế* v3.1 (phòng riêng = cá
   nhân), không phải bug — nhưng cùng màn hình, nhãn "Thêm thừa" (phòng) và lớp "Ưu tiên của bạn" (có
   Kim vì `d[Kim] = +0.21 > 0`) nói ngược nhau. **Đã sửa ở v3.5**: `PERSONAL_WEIGHT_PRIVATE` 0.5 → **0.3** cho
   cùng sản phẩm ra `−0.10` = 45% "Trung tính", khớp radar; Thổ 78 → 74, Thủy 61 → 69 vẫn "Phù hợp".
2. **Điểm là HƯỚNG, không phải LƯỢNG.** `ĝ·p` chỉ hỏi "sản phẩm nghiêng về hành phòng thiếu không", không
   hỏi "thêm vào thì phòng gần lý tưởng hơn bao nhiêu". Sản phẩm 1 phiếu thì hai câu gần trùng; tăng phiếu
   sản phẩm (hoặc cap phiếu tag) thì ca *bù quá tay* xuất hiện: Thủy 2 phiếu vẫn 59% nhưng `compat`
   88.8 → 88.9 (đã vượt mức lý tưởng 15%). Muốn triệt để thì đổi `gapScore` sang
   `(|gap|₁ − |previewGap|₁)` chuẩn hoá — chưa làm, ghi để không ai tưởng là bug hiển thị.
3. **Chip "Hợp nghề 88%" nhưng waterfall không có dòng nghề.** *(đã hết từ 19/09 — `OCCUPATION_WEIGHT = 0.20`; còn xảy ra khi user chưa khai nghề)* `OCCUPATION_WEIGHT = 0`
   ⇒ trục nghề tắt hoàn toàn trong `score`, trong khi chip trang sản phẩm là mặt A không phụ thuộc `Wo`.
   Hai con số **cố ý** không cùng thang (chip có footnote "không thay cho hợp bản mệnh"), nhưng chừng
   nào `Wo = 0` thì nghề chỉ là nhãn, không phải điểm.

Phép thử nhanh khi nghi "đi ngược": mở `ScoreWaterfall` — nếu dòng `GAP_SCORE` âm mà tổng dương thì mệnh
(hoặc nghề) đang gánh; nếu `GAP_SCORE` dương mà tổng âm thì `penalties[]` đang trừ. Radar chỉ vẽ phần
`GAP_SCORE`.

---

## 8. Muốn đổi công thức thì mở file nào

Đường dẫn tính từ gốc repo backend `FengDeskAI/` (FE ghi rõ `FengDeskAI_FE/`). Quy tắc chung: đổi
**số** → `scoring_params` (API admin, không cần deploy); đổi **cách tính** → file `Engine/`, nâng
`ScoringFormulaVersions.Current`, chạy lại golden set, ghi ADR.

### 8.1 Số — đổi không cần code

| Muốn đổi | Ở đâu |
|---|---|
| `Wp`, `Wo`, số phiếu nền/chủ nhân, `α`, mọi penalty, `MATERIAL/COLOR_SHARE`, `FALLBACK_*`, `MIN_SCORE_THRESHOLD` | `PUT /api/admin/scoring/params/{code}` · seed `seed-data/scoring-params.json` · default trong code **phải khớp seed**: `src/FengDeskAI.Application/Features/CustomerCare/Engine/ScoringModels.cs` (`ScoringParameters`) |
| điểm quan hệ `r[e]` (25 luật) | bảng `feng_shui_rules`; seed `src/FengDeskAI.Infrastructure/Persistence/Seeding/FengShuiRuleSeeder.cs`; mặc định `FengShuiCalculator.DefaultScore` |
| tag → hành (phiếu của tag, vector của sản phẩm gắn `DecorItem`) | `PUT /api/admin/scoring/element-input-tags/{kind}/{code}` · seed `seed-data/element-input-map.json` (**đừng** đổi `weightScale`) |
| mức lý tưởng / nền phòng theo loại | `PUT /api/admin/scoring/workspace-type-elements` · seed `seed-data/workspace-type-elements.json` |
| bẻ mục tiêu theo mục đích (`delta_intent`) | `PUT /api/admin/scoring/purpose-modifiers` · seed `seed-data/work-purpose-modifiers.json` |
| hồ sơ nghề Σ=1 | `PUT /api/admin/scoring/occupations/{code}/profile` · seed `seed-data/occupation-element-profiles.json` |
| vector một sản phẩm | override tay trên sản phẩm (`IsVectorOverridden`) hoặc `product_element_inputs` |

### 8.2 Cách tính — đổi phải code

| Muốn đổi | File | Hàm |
|---|---|---|
| vòng sinh/khắc, nạp âm, `personalVector`, hành theo hướng | `src/FengDeskAI.Application/Features/CustomerCare/Engine/FengShuiCalculator.cs` | `Generates`, `Controls`, `GetNapAmElement`, `GetRelation`, `DefaultScore`, `BuildPersonalVector` |
| `current` (phiếu, nén `α`, breakdown), `ideal`, `adjustedIdeal`, vector sản phẩm 3 tầng | `…/Engine/ElementVectorBuilders.cs` | `WorkspaceVectorBuilder.BuildCurrentBreakdown`, `ApplyIntent`, `ProductVectorProvider.Build` |
| gom 4 vector phòng thành `gap` | `…/Engine/WorkspaceElementAnalyzer.cs` | `Analyze` |
| `ĝ`, `d` (cách trộn ba trục) | `…/Engine/ElementDirection.cs` | `ForWorkspaceGap`, `ForPersonalNeed`, `WithOccupation` |
| `ô` (chuẩn hoá hồ sơ nghề, chặn khắc mệnh), luật Σ=1 | `…/Engine/OccupationAxis.cs`, `…/Engine/OccupationProfileRules.cs` | `Build`, `ClampAgainstDestiny`, `Validate` |
| `score`: tích, penalty, thành phần waterfall, facts tiếng Việt | `…/Engine/RecommendationScorer.cs` | `ScoreOne`, `BuildComponents`, `DescribePersonalAffinity`, `DescribeOccupationAffinity` |
| luật theo `ProductPlacement` (xét hướng? xét phòng? xung đột mệnh?) | `…/Engine/ScoringModels.cs` | **bảng** `PlacementPolicy` — thêm dòng, không rải `switch` |
| dụng thần (Carry) | `…/Engine/PersonalTargetBuilder.cs`, `BaTuCalculator.cs`, `LunarCalendarConverter.cs` | |
| phiếu của sản phẩm đã đặt / đang xem trước | `…/Engine/PlacedProductVectorBuilder.cs` **và** `…/Services/RecommendationService.cs` (`GetProductFitAsync`, đoạn `voteWeight`) — hai chỗ, phải sửa cả hai | |
| `compatibilityPercent` | `src/FengDeskAI.Application/Features/Workspace/Services/WorkspaceProfileService.cs` | `GetElementAnalysisAsync` |
| "sản phẩm hợp nghề X bao nhiêu %" (mặt A) | `…/Services/OccupationService.cs` | `GetProductFitAsync` |
| câu insight / diễn giải hành | `…/Engine/SpaceInsightBuilder.cs`, `…/Engine/ElementSemantics.cs` | |
| nạp ngữ cảnh chấm (DOB, scope, nghề, rule, params) cho 4 luồng | `…/Services/RecommendationService.cs` | `GenerateAsync`, `GetProductFitAsync`, `GetPersonalFitAsync`, `LoadOccupationAsync` |
| đóng dấu phiên bản công thức | `src/FengDeskAI.Domain/Entities/CustomerCare/ScoringFormulaVersions.cs` | `Current` |
| chạy lại sau khi đổi | `tests/FengDeskAI.UnitTests/RecommendationGoldenSetTests.cs` (bắt buộc), `OccupationScoringTests.cs`, `RecommendationScorerTests.cs`, `ScoreBreakdownTests.cs`, `PlacedProductCurrentTests.cs`; API: `tests/FengDeskAI.ApiTests/Endpoints/CustomerCareFlowTests.cs`, `ScoringConfigFlowTests.cs`, `WorkspaceFlowTests.cs` | |

### 8.3 FE — chỗ *vẽ* và chỗ *tính lại* (phải khớp BE)

| Gì | File (`FengDeskAI_FE/src/features/recommendation/…`) |
|---|---|
| bản sao `d = (1−Wp−Wo)·ĝ + Wp·r + Wo·ô` cho slider `Wp` (không gọi API) — **đổi cách trộn ở BE thì đổi cả đây** | `lib/breakdown.ts` (`combinedDirection`, `simulateScore`) |
| `displayPercent`, ngưỡng "Bù tốt/Thêm thừa" (`GAP_THRESHOLD`), 5 tông màu | `components/element-vector/constants.ts` |
| mức "Rất hợp / Phù hợp / Trung tính / Cân nhắc" | `components/element-vector/ScoreBadge.tsx` (`tierFor`) |
| radar (lý tưởng / hiện tại / xem trước / phần của bạn / ưu tiên của bạn) | `components/element-vector/ElementRadarChart.tsx` |
| waterfall | `components/element-vector/ScoreWaterfall.tsx` |
| nhãn từng hành cho một sản phẩm trong phòng | `components/element-vector/ProductFitPanel.tsx` (`toFitBarRows`) |
| thanh có dấu nghề | `components/element-vector/OccupationDirectionPanel.tsx` |
| chip / danh sách hợp nghề (mặt A) | `components/element-vector/OccupationFitChips.tsx`, `OccupationFitList.tsx` |
| vòng % tương thích của phòng | `FengDeskAI_FE/src/features/users/pages/ProfileWorkspace.tsx` (`CompatibilityRing`) |
| kiểu dữ liệu response | `types/recommendation.d.ts` |
