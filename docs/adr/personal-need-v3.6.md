# ADR — v3.6: dụng thần có kỵ thần, và đo "phủ" thay cho tích trong (luồng Carry)

> **Status:** Implementing (2026-09-20). Chỉ chạm nhánh `ScoringTarget.PersonalNeed` (vật mang theo người).
> Luồng phòng (`ĝ`, `r`, `Wp`) và trục nghề (`ô`, `Wo`) giữ nguyên. Từ vựng: [`docs/glossary-scoring.md`](../glossary-scoring.md) §1, §5.

## 1. Vấn đề — hai cái, cùng một gốc

**1.1 Vector dụng thần chỉ có phía "cần".** `PersonalTargetBuilder` trả `n̂` Σ=1 gồm 2 hành (Tứ Trụ:
dụng .6 / hỷ .4) hoặc 3 hành (Nạp Âm: .6/.3/.1), các hành khác = 0. Tứ Trụ thực ra chia hành thành **ba
nhóm**: dụng/hỷ thần (bồi), **kỵ thần** (tránh), nhàn thần (trung tính). Engine gộp kỵ với nhàn thành 0 ⇒ vật
toàn kỵ thần được chấm *trung tính* (50 %). `MINOR_CLASH_PENALTY` chỉ vá được đúng một hành (hành khắc
*Nạp Âm mệnh*), không phải kỵ thần theo Tứ Trụ.

**1.2 Điểm bị kẹt trong 25–80 %.** `PERSONAL_NEED_SCORE = n̂·p` với hai vector Σ=1 không âm ⇒
`n̂·p ≤ max(n̂) = 0.6`; vật **khớp hoàn hảo** (p = n̂ = Thổ .6/Kim .4) chỉ được `0.36 + 0.16 = 0.52 → 76 %`,
không bao giờ tới "Rất hợp" (≥ 80 %). Sàn = 0 rồi trừ phạt. Đây là trần v3.2 §8 đã gỡ cho phòng (`ĝ` có
dấu, chuẩn hoá half-L1) nhưng chưa gỡ cho Carry. Không phải bug code — là toán của phép đo.

## 2. Quyết định

### 2.1 Kỵ thần đi cùng dụng thần

`PersonalTarget` có thêm `Avoid` (tập hành kỵ). Suy ngay từ `BaTuCalculator`, đối xứng với cách nó chọn dụng thần:

| Thân | Dụng (đã có) | **Kỵ (mới)** | Nhàn |
|---|---|---|---|
| nhược | ấn (sinh nhật chủ) .6 · tỷ kiếp (= nhật chủ) .4 | thực thương (nhật chủ sinh) · tài (nhật chủ khắc) · quan sát (khắc nhật chủ) | — |
| vượng | thực thương .6 · tài .4 | ấn · tỷ kiếp | quan sát |

Nạp Âm (thiếu giờ sinh): dụng = mệnh .6 / sinh mệnh .3 / mệnh sinh .1 (như cũ); **kỵ = hành khắc mệnh**;
còn lại nhàn. Ít thông tin hơn nên ít hành bị trừ hơn — đúng với mức chắc chắn của dữ liệu.

### 2.2 Đo "phủ" và "rơi vào kỵ" thay cho tích trong

```
needCover = Σ_e min(n̂[e], p[e])          ∈ [0, 1]   — phần nhu cầu được sản phẩm phủ (= 1 − |n̂ − p|₁/2 nếu p ⊆ dụng)
avoidHit  = Σ_{e ∈ Kỵ} p[e]              ∈ [0, 1]   — phần sản phẩm rơi vào kỵ thần
personal  = needCover − avoidHit          ∈ [−1, 1]
blended   = (1 − Wo)·personal + Wo·(ô·p)                                       (trục nghề không đổi)
score     = clamp(blended − USER_CONFLICT − MINOR_CLASH(chỉ hành khắc mệnh ∉ Kỵ) , −1, 1)
```

- `needCover` là cùng phép đo với `compatibilityPercent` của phòng (`1 − |a − b|₁/2`, total variation) —
  một cách hiểu cho cả hai màn hình: **"phủ được bao nhiêu phần nhu cầu"**. Khớp hoàn hảo = 1 (100 %), cấp
  thừa một hành không được cộng thêm (`min`), giống "thêm thừa" bên phòng.
- `avoidHit` là **phần** của vật là kỵ thần — đọc thẳng: "43 % vật này là hành bạn nên tránh".
- Hai số hạng tách thành hai dòng waterfall (`PERSONAL_NEED_SCORE` +, `PERSONAL_AVOID_SCORE` −) để user thấy
  từng hành: `min(n̂, p)` theo hành, `−p` theo hành kỵ. Trọng số cả hai = `1 − Wo`.
- `MINOR_CLASH_PENALTY` giữ, nhưng **bỏ qua hành đã nằm trong Kỵ** — một hành không bị trừ hai lần.
  `USER_CONFLICT_PENALTY` (hành trội khắc mệnh, `AlwaysHard` ở Carry) giữ nguyên.
- `n̂` (Σ=1, không âm) vẫn trả ở `vectors.normalizedGap` cho radar; thêm `personalAvoidElements`.

### 2.3 Con số — cùng user (mệnh Kim, Tứ Trụ: dụng Thổ .6 / Kim .4, kỵ Thủy · Mộc · Hỏa), 8 vật Carry trong DB

| Vật | p | cũ `n̂·p − clash` | **mới** `phủ − kỵ` | % cũ → mới |
|---|---|---|---|---|
| Tỳ hưu bạc | Kim .70 Thổ .30 | 0.460 | 0.700 − 0 = **0.700** | 73 → **85** |
| Vòng thạch anh tím | Thổ .60 Hỏa .20 Kim .20 | 0.440 − 0.120 | 0.800 − 0.200 = **0.600** | 66 → **80** |
| Tượng Tỳ Hưu đồng | Kim .58 Thổ .30 Hỏa .12 | 0.412 − 0.072 | 0.700 − 0.120 = **0.580** | 67 → **79** |
| Charm hồ lô đồng | Kim .57 Hỏa .25 Mộc .18 | 0.227 − 0.152 | 0.400 − 0.433 = **−0.033** | 54 → **48** |
| Charm obsidian | Thủy .56 Kim .24 Thổ .20 | 0.216 | 0.440 − 0.560 = **−0.120** | 61 → **44** |
| Móc khóa gỗ | Mộc .60 Thổ .40 | 0.240 | 0.400 − 0.600 = **−0.200** | 62 → **40** |
| Vòng gỗ sưa đỏ | Mộc .60 Kim .20 Hỏa .14 Thổ .06 | 0.116 − 0.084 | 0.260 − 0.740 = **−0.480** | 52 → **26** |
| *(giả) khớp đúng Thổ .6 Kim .4* | | 0.520 | **1.000** | 76 → **100** |
| *(giả) 100 % Thủy* | | 0.000 | **−1.000** | 50 → **0** |

Thứ hạng đổi theo đúng hướng: hai vật gỗ (toàn tài/thực thương của nhật chủ Kim nhược) rơi xuống "Cân nhắc";
vật Kim/Thổ lên "Rất hợp". Với fallback Nạp Âm (kỵ chỉ có Hỏa) cùng bộ vật ra 56–95 % — mềm hơn vì biết ít hơn.

## 3. Thay đổi

| Lớp | Việc |
|---|---|
| `Engine/FengShuiCalculator.cs` | `GetControllingElement(e)` (hành khắc `e`) — đảo của `Controls` |
| `Engine/BaTuCalculator.cs` | `BaTuChart.UnfavorableElements/Codes` (kỵ thần) theo bảng §2.1 |
| `Engine/PersonalTargetBuilder.cs` | `PersonalTarget.Avoid` + `AvoidElements` (tên VN); Nạp Âm: `{khắc mệnh}` |
| `Engine/ScoringModels.cs` | `ScoringContext.PersonalAvoid`; `ScoreComponentCodes.PersonalAvoidScore`; `ScoreBreakdown.PersonalAvoidElements` |
| `Engine/RecommendationScorer.cs` | nhánh `PersonalNeed`: `needCover`/`avoidHit` thay `n̂·p`; 2 thành phần; `ClashShare` bỏ hành ∈ Kỵ; facts mới |
| `Domain/.../ScoringFormulaVersions.cs` | `V36 = "3.6"` |
| DTO / mapping | `ScoreBreakdownResponse.personalAvoidElements`; `PersonalFitResponse.personalAvoidElements`; `PersonalTargetResponse.avoidElements` |
| `Services/RecommendationService.cs` | truyền `Avoid` vào context (2 chỗ: `GeneratePersonalAsync`, `GetPersonalFitAsync`) |
| Test | `PersonalNeedV36Tests` (bảng §2.3 + biên 0/100 + Nạp Âm + không trừ hai lần); sửa ca cũ của nhánh Carry |
| FE | `ScoreWaterfall`: phép tính `min(n̂,p)` / `−p`; `PersonalFitPanel`: chip kỵ thần, ✕ trên trục kỵ, tooltip "phủ / kỵ" |
| Docs | glossary §1/§5/§7, api 18/25, ARD, README ADR |

Không đổi: luồng phòng, trục nghề, `PlacementPolicy`, golden set phòng (byte-identical).
