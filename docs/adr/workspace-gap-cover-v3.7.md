# ADR — v3.7: trục phòng và trục nghề đo "phủ nhu cầu" thay cho tích trong

> **Status:** Implementing (2026-09-30). Chạm **hai** trục: `ScoringTarget.WorkspaceGap` (Desk/Living) và
> trục nghề `ô` ở cả ba mặt (A/B/C của ADR `occupation-product-fit-v1`).
> Nhánh Carry (`PersonalNeed`) đã đổi từ v3.6 và **không** bị chạm lần nữa; trục bản mệnh `r·p` giữ
> nguyên tích trong (lý do ở §2.4). Từ vựng: [`docs/glossary-scoring.md`](../glossary-scoring.md) §1, §5.

## 1. Vấn đề

`gapScore = ĝ · p` với

- `ĝ = gap / (|gap|₁/2)`, `gap = adjustedIdeal − current`, Σ`gap` = 0
  ⇒ **`Σ ĝ⁺ = 1` đúng bằng 1, `Σ|ĝ⁻| = 1` đúng bằng 1, hai phía KHÁC giá đỡ**;
- `p` Σ=1, không âm.

Vì `p` là một **simplex**, `ĝ·p` chính là **trung bình có trọng số** của `ĝ` tại chỗ khối lượng sản phẩm
nằm. Trung bình không bao giờ vượt phần tử lớn nhất trong đám lấy trung bình, và `ĝ⁺` chỉ có **một đơn
vị nhu cầu để chia** cho các hành đang thiếu. Ghép lại:

```
gapScore  ≤  max_e ĝ[e]  ≤  1        và đồng thời
gapScore  ≤  max_e p[e]              ← TRẦN NẰM Ở CHÍNH SẢN PHẨM
```

Trần của một sản phẩm, **trên mọi căn phòng có thể tồn tại**, bằng đỉnh khai báo ngũ hành của chính nó.

### 1.1 Con số

Sản phẩm thật trên production — *Cây Phát Tài Thủy Sinh*, `Kim .44 · Thủy .36 · Mộc .20`.
Phòng **hợp nhất có thể có** với nó: thiếu đúng ba hành đó (`Kim .5 / Thủy .3 / Mộc .2`), thừa Hỏa · Thổ.

```
ĝ·p = 0.50×0.44 + 0.20×0.20 + 0.30×0.36 = 0.368     →  68.4 %
```

Cùng lúc đó *Tượng Tỳ Hưu* khai độc `Kim 1.000` chạm `ĝ·p = 1.000 → 100 %`.

### 1.2 Hệ quả nghiệp vụ

Engine **thưởng cho việc khai thiếu**. Trên production, 4/22 sản phẩm nhánh Desk/Living khai thuần một
hành; chúng luôn xếp trên các sản phẩm được mô tả kỹ, không phải vì hợp phòng hơn mà vì ít thông tin hơn.
Hạng "Rất hợp" (≥ 80 %) gần như bất khả thi với sản phẩm khai đủ.

Đây **đúng là trần mà v3.6 đã gỡ cho nhánh Carry** (`n̂·p ≤ max n̂ = 0.6`). Luồng phòng là nhánh duy nhất
bị bỏ lại.

## 2. Quyết định

```
cover(d)  = Σ_e min(d⁺[e], p[e])      ∈ [0, 1]   — phần nhu cầu được sản phẩm phủ
over(d)   = Σ_e min(d⁻[e], p[e])      ∈ [0, 1]   — phần sản phẩm đổ vào hành ĐÃ dư / nên tránh
score(d)  = cover(d) − over(d)        ∈ [−1, 1]

gapScore        = score(ĝ)
occupationScore = score(ô)                                    ← v3.7 cũng đổi, xem §2.3
blended         = (1 − Wp − Wo)·gapScore + Wp·(r·p) + Wo·occupationScore
```

Ẩn dụ: nhu cầu của phòng là một dãy **cốc**, cốc hành `e` có dung tích `ĝ⁺[e]`, tổng dung tích đúng 1.0.
Sản phẩm là 1.0 lít nước chia sẵn theo hành. `min` = rót quá thì **tràn, không tính thêm** (đúng khái niệm
"thêm thừa" vốn đã có ở nhánh phòng), chưa đủ nước thì chỉ tính phần rót được. Mỗi hành có cốc **riêng**
thay vì tranh nhau trong một phép trung bình chung.

Cùng ví dụ §1.1:

```
cover = min(.50,.44) + min(.20,.20) + min(.30,.36) = 0.44 + 0.20 + 0.30 = 0.94
over  = 0
gapScore = 0.94                                     →  0.368 → 0.94
đóng góp vào điểm cuối (Wg = 0.5): 0.184 → 0.470    →  +14.3 điểm phần trăm hiển thị
```

### 2.1 Ba tính chất

**(a) Sản phẩm thuần một hành: điểm KHÔNG đổi, chính xác tuyệt đối.**
Với `p = e_k`: `cover = ĝ⁺[k]`, `over = ĝ⁻[k]`, một trong hai luôn bằng 0
⇒ `cover − over = ĝ[k] = ĝ·p`. Mô phỏng 48 000 cặp (4 000 phòng ngẫu nhiên × 12 vector sản phẩm thật):
`max |Δ|` trên nhóm thuần = **0.000000**. 4 sản phẩm khai thiếu trên prod không xê dịch một ly — đây là
đổi công bằng cho nhóm khai đủ, không phải dịch chuyển cả thang điểm.

**(b) Miền `[−1, 1]` tự nhiên, `clamp` thành lưới an toàn thừa.**
`supp(ĝ⁺) ∩ supp(ĝ⁻) = ∅` và `Σp = 1` ⇒ `cover + over ≤ 1` ⇒ `|cover − over| ≤ 1`.

**(c) Cùng hình dạng với v3.6** (`needCover − avoidHit`). Khác biệt duy nhất: phía trừ bên Carry là một
**tập** (kỵ thần, có/không) nên là `Σ_{e∈kỵ} p`, còn phía trừ bên phòng có **mức độ** (`ĝ⁻`) nên dùng `min`
cả hai vế.

### 2.3 Vì sao trục nghề đi theo — và vì sao ban đầu nó là `ô·p`

`ô` **không** được thiết kế riêng. ADR `occupation-product-fit-v1` §2.2 dựng nó **cố ý giống hệt `ĝ`**:

```
o  = hồ sơ nghề, Σ=1        (chuyên gia phát biểu bằng PHÂN BỐ, vd FINANCE: Kim .50 Thủy .30 Thổ .10 Hỏa .05 Mộc .05)
δ  = o − 0.2                Σ=0  — "lệch so với mức đều", cùng hình dạng gap phòng
ô  = δ / (|δ|₁ / 2)         chuẩn hoá NỬA-L1 — y hệt ĝ ⇒ Σô⁺ = 1, Σ|ô⁻| = 1
```

Lý do ghi trong ADR đó: để `ô·p` **cùng thang** với `ĝ·p` và `r·p` nên ba số hạng cộng thẳng được vào nhau,
và để `OCCUPATION_SCORE` là **một con số duy nhất** ở cả ba mặt ("hợp nghề 88 %" trên chip trang sản phẩm
= dòng "Hợp nghề của bạn" trong waterfall). Tích trong không được chọn vì nó đúng cho nghề — nó được
**chép** từ trục phòng, vào lúc trục phòng còn được tin là đúng.

Cùng cấu trúc thì cùng lỗi: `Σô⁺ = 1` + `Σp = 1` ⇒ `ô·p` là trung bình có trọng số ⇒ trần lại rơi về
`max p[e]`. Sửa mỗi trục phòng thì 20 % điểm (`Wo`) vẫn kẹt trần cũ, và hạng "Rất hợp" vẫn khó đến mức
gần như bất khả thi với sản phẩm khai đủ hành. Vì vậy v3.7 đổi **cả hai**, cùng một hàm
(`NeedCover` / `Overfill`), cùng một câu văn (`DescribeCoverMatch`).

**Mặt A phải đổi cùng lúc.** `OccupationService.ToFitRow` (chip `GET /api/products/{id}/occupation-fit`)
tính lại cho riêng nó; đổi một bên thì chính bất biến "một con số duy nhất" của ADR nghề bị phá.

**Sau `ClampAgainstDestiny`, `Σô⁺` có thể < 1** (hành nghề cần nhưng khắc mệnh bị chặn về 0). Đúng chủ ý:
sức chứa "cốc nghề" giảm đúng phần nghề không được phép kéo — cùng ý với ghi chú "không chuẩn hoá lại sau
clamp" ở `OccupationAxis`, và `ClampNoteVi` vẫn nói ra. Miền `[−1, 1]` không bị ảnh hưởng.

### 2.4 Vì sao `r·p` (bản mệnh) ở LẠI là tích trong

`r[e]` là **điểm quan hệ** của từng hành với bản mệnh (tỷ hòa +1.0 … bị khắc −1.0), lấy từ `feng_shui_rules`.
Nó **không** là một ngân sách Σ=1 bị chia nhỏ: hai hành cùng tỷ hòa thì cả hai đều `+1.0`, nên sản phẩm
trải đều trên chúng vẫn được `+1.0`. Không có trần `max p[e]` nào ở đây, và "trung bình độ hợp mệnh của
các hành trong vật" đúng là điều cần đo. Đổi nó sang `min` sẽ sai nghĩa.

### 2.2 Bất biến `p · d ≈ blended` không còn đúng

`CombinedDirection` `d = (1−Wp−Wo)·ĝ + Wp·r + Wo·ô` vẫn là **vector radar** ("hệ thống đang ưu tiên bù hành
nào"), nhưng `p·d` không còn dựng lại được `blended` vì số hạng phòng không còn là tích trong. Bất biến này
thực ra đã mất tính phổ quát từ v3.6 — ca test Carry còn xanh chỉ vì nó dùng sản phẩm thuần một hành
(§2.1a). v3.7 nói thẳng ra:

- **Giữ:** `Σ Components[i].Contribution == Blended` (đây mới là đường user đọc được trên `ScoreWaterfall`).
- **Thu hẹp:** `p·d ≈ blended` chỉ đúng khi **mọi trục đều là tích trong**, tức sản phẩm thuần một hành.
  `ScoreBreakdownTests.AssertConsistent` quét 500+ tổ hợp nhưng **toàn bộ** dùng `ElementVector.Single(...)`
  nên nó vẫn xanh nguyên — chính vì vậy sự thật ngược lại được ghim riêng ở `GAP-37-08`, thay vì để nó nằm
  chờ người sau tưởng radar dựng lại được điểm.

## 3. Thay đổi

| Lớp | Việc |
|---|---|
| `Engine/RecommendationScorer.cs` | `WorkspaceGap`: `gapCover`/`gapOverfill` thay `ĝ·p`. Trục nghề: `occupationCover`/`occupationOverfill` thay `ô·p`. Thêm `Overfill()`; `DescribeVectorMatch` ⇒ `DescribeCoverMatch()` cho cả hai dòng |
| `Services/OccupationService.cs` | mặt A (`ToFitRow`) đổi cùng công thức — nếu không, chip và waterfall nói hai số khác nhau |
| `Engine/ScoringModels.cs` | `ScoreBreakdown.GapCover`/`GapOverfill`/`OccupationCover`/`OccupationOverfill` (nội bộ engine + test, không ra DTO — cùng nếp với `PersonalNeedCover`) |
| `Domain/.../ScoringFormulaVersions.cs` | `V37 = "3.7"`, `Current = V37` |
| Test | `WorkspaceGapCoverV37Tests` — 11 ca: `GAP-37-01..08` (ví dụ §2, bất biến thuần-một-hành 5 hành, biên ±1, `min` ở vế trừ, breakdown, phòng không thiếu gì, `p·d ≠ blended`) + `OCC-37-01..03` (giữ nguyên 0.75 của ADR nghề, sản phẩm khai đủ không bị kéo xuống, breakdown) |
| FE | `ScoreWaterfall.ComponentCalc`: mode `"gap"` dùng chung cho `GAP_SCORE` + `OCCUPATION_SCORE` — `min(|d|, p)` có dấu, thay `d × p`. `OccupationFitChips`: sắp dòng theo số hạng mới |
| Docs | ADR này, `docs/adr/README.md`, `glossary-scoring.md` §5/§7, `api-documents/18-recommendations.md` |

**Không đổi:** `current`/`adjustedIdeal`/`ĝ`/`ô` (cách DỰNG vector, radar y nguyên — chỉ phép ĐO đổi),
`PlacementPolicy`, mọi penalty, trục `r`, toàn bộ nhánh Carry, `compatibilityPercent`,
`ClampAgainstDestiny`, `OCCUPATION_WEIGHT`.

**Golden set KHÔNG phải baseline lại.** `RecommendationGoldenSetTests` và `OccupationGoldenSetTests` dựng sản
phẩm bằng `ElementVector.Single(...)`, mà sản phẩm thuần một hành có điểm bất biến (§2.1a) ⇒ byte-identical.
Đây vừa là xác nhận bất biến, vừa là lỗ hổng: **golden set không phủ hành vi mới**, nên phần phủ nằm hoàn
toàn ở `WorkspaceGapCoverV37Tests`.

## 4. Ảnh hưởng đã đo

### 4.1 Trục phòng — 48 000 cặp (4 000 phòng ngẫu nhiên × 12 vector sản phẩm thật)

| Đại lượng | Nay | v3.7 |
|---|---|---|
| `gapScore` trung bình | +0.0007 | +0.0008 |
| độ lệch chuẩn | 0.357 | **0.446** (+25 % — tách hạng tốt hơn) |
| Đ trên sản phẩm **thuần 1 hành** | — | **0.000000** |

### 4.2 Trục nghề — mặt A, 84 cặp (12 sản phẩm × 7 nghề có hồ sơ)

Đây là con số **hiện thẳng trên chip "Hợp nghề X%"** ở trang sản phẩm, nên phải nói rõ:

| Đại lượng | Nay | v3.7 |
|---|---|---|
| % trung bình | 50.2 % | **47.1 %** |
| độ lệch chuẩn | 0.336 | **0.431** |
| sản phẩm thuần 1 hành (28 cặp) | — | `|Đ|` lớn nhất **0.000000** |
| 84 cặp | — | tăng **17** · giảm **35** · không đổi **32** |

**Trục nghề KHÔNG phải một cú nâng điểm — nó là một cú làm sắc.** `min` ở vế trừ cũng cắt sâu hơn
tích trong, và vì một nghề thường có 2–3 hành "cần" nhưng 2–3 hành "nên tránh", nhiều sản phẩm rơi vào
phía trừ hơn là phía cộng ⇒ trung bình tụt ~3 điểm %.

| | Cặp | Nay → v3.7 |
|---|---|---|
| nhích nhiều nhất | Cầu Thạch Anh Tím × `FINANCE` | 80.0 % → **97.5 %** |
| | Đèn Muối Himalaya × `SALES` | 60.0 % → 70.0 % |
| tụt nhiều nhất | Cây Trầu Bà Lá Xẻ × `IT` | 29.2 % → **8.3 %** |
| | Cây Lan Ý Mini × `CREATIVE` | 25.0 % → 5.0 % |

Mọi cặp tụt đều **đã nằm sẵn trong hạng "Cân nhắc"** (< 40 %) từ trước — không có cặp nào đang được coi
là hợp bị đẩy xuống. Đó là hệ quả đúng của phép đo: "cây trầu bà (Mộc .5 / Thổ .5) đối với nghề IT"
đúng là gần như lấp trọn phần nghề IT muốn giảm, chứ không phải "hơi lệch một chút".

### 4.3 Điểm cuối — 1 500 phòng × 12 sản phẩm, nghề bốc ngẫu nhiên (Wg 0.50 · Wp 0.30 · Wo 0.20)

| Hạng | Nay | chỉ sửa trục phòng | **v3.7 (cả hai)** |
|---|---|---|---|
| Rất hợp (≥ 80) | 0.8 % | 1.0 % | **1.1 %** |
| Phù hợp (≥ 60) | 17.9 % | 23.3 % | **22.2 %** |
| Trung tính (≥ 40) | 61.8 % | 51.0 % | **50.5 %** |
| Cân nhắc (< 40) | 19.4 % | 24.6 % | **26.2 %** |
| % trung bình | 49.9 % | 49.9 % | 49.3 % |

**Đảo thứ hạng gợi ý:** sản phẩm đứng đầu đổi ở **33.3 %** số phòng so với hiện tại (chỉ sửa trục
phòng đã là 32.7 % — trục nghề **chỉ thêm 10.6 %** đảo so với phương án chỉ-phòng, vì `Wo` chỉ có 0.20).
Phiên gợi ý đã lưu mang `formulaVersion` cũ nên không bị viết lại, nhưng phiên mới sẽ khác phiên cũ trên
cùng một phòng.

> Hình dọc của hai bảng trên: một phần lớn khối "Trung tính" (61.8 % → 50.5 %) tách ra hai phía.
> Đó chính là mục tiêu — trước đây gần 2/3 sản phẩm dồn vào một hạng duy nhất, tức điểm gần như
> không nói được gì.
