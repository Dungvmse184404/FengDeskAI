# 25 — Scoring Config (Admin engine v3)

[← Mục lục](./README.md)

Controller: `ScoringConfigController` · Route gốc: `/api/admin/scoring` · **Toàn bộ `[Authorize(Policy = ManagerOrAbove)]`**.

Quản trị cấu hình engine chấm điểm gợi ý **v3**: tham số điểm, map ngũ hành (màu/vật liệu/hình khối), modifier theo mục đích, và vector lý tưởng/nội thất theo loại phòng. Sửa được dữ liệu **không cần đổi code**.

---

## 📋 Bảng endpoint

| Method | Path | Mô tả |
|--------|------|-------|
| GET | `/api/admin/scoring/params` | Danh sách tham số engine |
| PUT | `/api/admin/scoring/params/{code}` | Sửa 1 tham số |
| GET | `/api/admin/scoring/element-inputs` | Map (màu/vật liệu/hình) → hành |
| PUT | `/api/admin/scoring/element-inputs` | Thêm/sửa 1 dòng map |
| DELETE | `/api/admin/scoring/element-inputs/{id}` | Xóa 1 dòng map |
| GET | `/api/admin/scoring/element-input-tags` | **Tag gộp theo (kind, code)** — màn quản trị dùng cái này |
| PUT | `/api/admin/scoring/element-input-tags/{kind}/{code}` | Sửa trọn 1 tag: nhãn + phân bổ hành + phạm vi |
| DELETE | `/api/admin/scoring/element-input-tags/{kind}/{code}` | Xóa toàn bộ hành của 1 tag |
| GET | `/api/admin/scoring/purpose-modifiers` | Modifier Intent theo mục đích |
| PUT | `/api/admin/scoring/purpose-modifiers` | Thêm/sửa 1 modifier |
| DELETE | `/api/admin/scoring/purpose-modifiers/{id}` | Xóa 1 modifier |
| GET | `/api/admin/scoring/workspace-type-elements` | Vector Ideal/Interior theo loại phòng |
| PUT | `/api/admin/scoring/workspace-type-elements` | Thêm/sửa 1 dòng vector |
| DELETE | `/api/admin/scoring/workspace-type-elements/{id}` | Xóa 1 dòng vector |
| GET | `/api/occupations` | **Public** — danh sách nghề đang bật (không kèm hồ sơ) cho màn hồ sơ |
| GET | `/api/admin/scoring/occupations` | Danh sách nghề **kèm hồ sơ ngũ hành Σ=1** |
| POST | `/api/admin/scoring/occupations` | Thêm nghề mới |
| PUT | `/api/admin/scoring/occupations/{code}` | Sửa tên/mô tả/trạng thái (KHÔNG đụng hồ sơ) |
| PUT | `/api/admin/scoring/occupations/{code}/profile` | **Ghi đè trọn gói hồ sơ ngũ hành** (Σ share = 1) — chỗ chuyên gia sửa số |
| DELETE | `/api/admin/scoring/occupations/{code}` | Xóa nghề (chặn khi còn user đang chọn) |

---

## Tham số engine — `scoring_params`

`data` = mảng `ScoringParamDto`: `{ id, code, value, description }`.
**PUT** body (`UpsertScoringParamRequest`): `{ "value": 0.3, "description": "..." }`.

### Dựng vector

| Code | Default | Ý nghĩa |
|------|:---:|---------|
| `SELF_SHARE` | 0.60 | Tỉ trọng **bản mệnh** trong vector mệnh cá nhân |
| `SUPPORT_SHARE` | 0.30 | Tỉ trọng hành **sinh ra** bản mệnh (mẹ) |
| `CHILD_SHARE` | 0.10 | Tỉ trọng hành bản mệnh **sinh ra** (con) |
| `MATERIAL_SHARE` | 0.60 | Tỉ trọng chất liệu khi dựng vector sản phẩm |
| `COLOR_SHARE` | 0.40 | Tỉ trọng màu/hình khi dựng vector sản phẩm |
| `FALLBACK_PRIMARY` | 0.70 | Trọng số hành chính khi backfill vector từ `product_elements` |
| `FALLBACK_SECONDARY` | 0.30 | Trọng số hành phụ khi backfill |

### Vật phẩm mang theo người (`ProductPlacement.Carry`)

| Code | Default | Ý nghĩa |
|------|:---:|---------|
| `CARRY_PRIMARY_SHARE` | 0.60 | Tỉ trọng **dụng thần chính** khi dựng vector mục tiêu cá nhân |
| `CARRY_SECONDARY_SHARE` | 0.40 | Tỉ trọng dụng thần phụ |

> Chỉ áp cho luồng [`POST /api/recommendations/personal`](./18-recommendations.md). Có giờ sinh → dụng thần **Tứ Trụ**; thiếu giờ sinh → fallback **Nạp Âm** (dùng `SELF/SUPPORT/CHILD_SHARE`).

### Trừ điểm

| Code | Default | Ý nghĩa |
|------|:---:|---------|
| `USER_CONFLICT_PENALTY` | **0.60** | Phạt khi hành trội sản phẩm khắc mệnh user (không gian dùng chung) |
| `DIRECTION_PENALTY` | **0.30** | Phạt khi mọi hướng hợp vật phẩm đều bị chắn |
| `VIBE_MISMATCH_PENALTY` | **0.40** | Phạt khi sản phẩm **có** vibe nhưng không khớp mục đích phòng |
| `VIBE_UNKNOWN_PENALTY` | **0.10** | Phạt khi sản phẩm **chưa khai** vibe — thiếu dữ liệu, nhẹ hơn lệch thật |

### Trục cá nhân (v3.1)

| Code | Seed | Ý nghĩa |
|------|:---:|---------|
| `PERSONAL_WEIGHT_PRIVATE` | **0.30** | Tỉ trọng `personalScore` ở không gian `Private` — v3.5 hạ từ 0.50 (migration `ScoringParamsV35`) |
| `PERSONAL_WEIGHT_SHARED` | **0.20** | Ở không gian `Shared` (phòng khách, bếp, phòng họp) — v3.5 hạ từ 0.30 |
| `PERSONAL_WEIGHT_PUBLIC` | 0.00 | Ở không gian `Public` (lễ tân, khu mở) — **luôn 0** |

```
score = (1 − Wp)·gapScore + Wp·personalScore − dirPenalty − vibePenalty
```

`Wp = 0` (seed) → giữ nguyên hành vi trước v3.1: hard-filter khắc mệnh + `USER_CONFLICT_PENALTY` **đầy đủ**.
`Wp > 0` → xung khắc tính có dấu trong `personalScore` (từ `feng_shui_rules`), **bỏ hard-filter cho đồ
đặt trong phòng**, và `USER_CONFLICT_PENALTY` **co giãn theo `Wp`** (`PersonalConflictMode.Scaled`, v3.2):

```
userPenalty = USER_CONFLICT_PENALTY × Wp     // 0.60 × 0.50 = 0.30 ở phòng Private
```

> **4 penalty đã nhân đôi ở v3.2** vì miền `gapScore` nở từ ±0.5 lên ±1.0 (mẫu số đổi thành `|gap|₁/2`).
> Nhân đôi để giữ đúng **tỉ lệ tương đối** cũ, không phải để phạt nặng hơn.
> Xem [score-explainability-v3.2.md §8](../adr/score-explainability-v3.2.md).

> ⚠️ **`Carry` không theo luật trên.** Vật mang trên người khắc mệnh thì **loại thẳng ở mọi mức `Wp`**
> (`PersonalConflictMode.AlwaysHard`) — nó áp sát người cả ngày, không có "phòng" để pha loãng.
> Chỉ `Desk`/`Living` mới chuyển sang trừ điểm mềm. Phòng `Public` thì không lọc cũng không phạt.

> `Wp` cũng bị ép về 0 khi user **chưa có ngày sinh** — không có mệnh thì không có gì để trộn.
> Chi tiết: [personalized-recommendation-v3.1.md](../adr/personalized-recommendation-v3.1.md)

### Phiếu dựng hiện trạng phòng (v3.2 §12)

`current` của một căn phòng là trung bình có trọng số của các **nguồn**, đo bằng **phiếu**:

```
current = normalize( Σᵢ vᵢ · wᵢ )
```

| Code | Seed | Ý nghĩa |
|------|:---:|---------|
| `INTERIOR_PRIOR_VOTES` | **3.00** | Phiếu của **nền phòng theo loại**. Phòng chưa khai tag nào vẫn có hiện trạng hợp lý |
| `PERSON_PRESENCE_VOTES_PRIVATE` | **3.00** | Phiếu của **bản mệnh chủ nhân** ở phòng `Private` — ngang nền phòng |
| `PERSON_PRESENCE_VOTES_SHARED` | **2.00** | Ở phòng `Shared` — nhẹ hơn, vì phòng còn của người khác |
| `PERSON_PRESENCE_VOTES_PUBLIC` | **0.00** | Ở phòng `Public` — **không tính**. Lễ tân không thuộc về ai |

Mỗi tag user khai bỏ vào Σ `weight` của code (chuẩn **1 phiếu**), mỗi sản phẩm đã đặt bỏ vào `voteWeight`.

| Code | Seed | Ý nghĩa |
|------|:---:|---------|
| `TAG_VOTES_CAP` | **5.00** | v3.5 — trần cho **tổng** phiếu tag: `Σ > cap` ⇒ mọi tag nhân `cap / Σ`. Tag là nguồn duy nhất không có trần; 5 = tối đa ngang nền + chủ nhân ở Private. `≤ 0` = tắt. Response trả `tagVotesScale` |

**Vì sao là phiếu chứ không phải tỉ trọng %:** nền phòng và bản mệnh đều là **prior** — suy đoán khi chưa
có dữ liệu. Chúng phải **loãng dần** khi user khai thêm tag thật. Tỉ trọng cố định thì khai 20 tag mà prior
vẫn giữ nguyên phần của nó — ngược với "bằng chứng lấn át suy đoán".

| Số tag user khai | Phần của nền phòng (3 phiếu) | Phần của chủ nhân (3 phiếu) |
|---:|---:|---:|
| 0 | 50.0% | 50.0% |
| 6 | 25.0% | 25.0% |
| 20 | 11.5% | 11.5% |

⚠️ **Hai họ tham số này song song, không thay thế nhau.** `PERSON_PRESENCE_VOTES_*` định lượng bản mệnh
trong **hiện trạng phòng** (đổi `current` → đổi `gap` → đổi radar lẫn điểm). `PERSONAL_WEIGHT_*` định
lượng bản mệnh khi **chấm một sản phẩm** (`d = (1−Wp)·ĝ + Wp·r`). Đặt cả hai về 0 mới là tắt hẳn bản mệnh.

⚠️ **Phần của chủ nhân vào `current` dùng cho CẢ radar lẫn gap chấm điểm.** Nếu chỉ vào radar thì hình
và điểm nói hai chuyện khác nhau về cùng một căn phòng.

> `confidence` **giảm** khi bật phần chủ nhân: prior vào mẫu số nhưng không vào tử số. Bàn học
> 70.0% → 53.8%. Đúng bản chất: phòng vẫn chưa được khai thêm bằng chứng nào.

> Đặt `PERSON_PRESENCE_VOTES_PRIVATE` quá cao là nguy hiểm: 8 phiếu ở phòng chưa khai tag nào cho
> chủ nhân **72.7%** hiện trạng — hệ thống sẽ đi bù cho chính bản mệnh thay vì cho căn phòng.
> Xem [score-explainability-v3.2.md §12](../adr/score-explainability-v3.2.md).

### Nghề nghiệp — trục thứ ba *(v3.4 · N3)*

| Code | Seed | Ý nghĩa |
|------|:---:|---------|
| `OCCUPATION_WEIGHT` | **0.20** | `Wo` — phần nghề chiếm trong hướng chấm điểm. **MỘT** tham số cho cả luồng phòng lẫn Carry, mọi scope. **Seed 0 = kill-switch**: trục nghề không dựng ⇒ `d` byte-identical v3.3 dù hồ sơ đã có trong DB. Đích **0.20** sau golden set |

```
δ  = profile − 0.2                 hồ sơ Σ=1 trừ phân bố đều ⇒ Σδ = 0, cùng hình dạng gap phòng
ô  = δ / (|δ|₁ / 2)                mỗi trục ∈ [−1, +1] — chuẩn hoá nửa-L1 y hệt ĝ
ô[e] = min(ô[e], 0)                khi hành e KHẮC bản mệnh (chỉ khi biết mệnh)

d  = (1 − Wp − Wo)·ĝ + Wp·r + Wo·ô    (phòng)   · Wo kẹp ≤ 1 − Wp
d  = (1 − Wo)·n̂ + Wo·ô                (Carry)
```

Ví dụ: Tài chính = Kim 0.5 · Thủy 0.3 · Thổ 0.1 · Hỏa 0.05 · Mộc 0.05 ⇒ `ô` = Kim **+0.75**, Thủy +0.25,
Thổ −0.25, Hỏa/Mộc −0.375. Sản phẩm 100% Kim ⇒ `ô·p = 0.75` ⇒ **88%**.

⚠️ **Nghề không đổi được bản mệnh.** Hành `BiKhac` bị chặn về ≤ 0 trong `ô`, và mọi penalty
(`USER_CONFLICT_PENALTY`, `MINOR_CLASH_PENALTY`…) đi đường `GetRelation`/`personalVector`, không đọc `ô`.
Mệnh Mộc làm Tài chính thì Kim vẫn khắc — nghề chỉ không được **cộng** vào Kim.

`OTHER` = 0.2 đều ⇒ `|δ|₁ = 0` ⇒ trục tự tắt, không cần luật riêng.

> **Hồ sơ seed SẴN bản nháp** (`seed-data/occupation-element-profiles.json`, 8 nghề) — khác P5 để trống.
> Trọng số đã **bật 0.20** (P6.6, 2026-09-19) sau khi soát golden set với 8 hồ sơ (`OccupationGoldenSetTests`).
> FINANCE/CONSTRUCT do người ra đề soạn; 6 nghề còn lại là nháp, **cần chuyên gia duyệt** trước khi nâng weight.

**Tắt khẩn cấp:** `PUT /api/admin/scoring/params/OCCUPATION_WEIGHT { value: 0 }` — `d` byte-identical v3.3, không cần deploy.

#### Ghi đè hồ sơ ngũ hành

`PUT /api/admin/scoring/occupations/{code}/profile`

```json
{ "entries": [ { "element": "Kim", "share": 0.50 }, { "element": "Thuy", "share": 0.30 },
               { "element": "Tho", "share": 0.10 }, { "element": "Hoa", "share": 0.05 }, { "element": "Moc", "share": 0.05 } ] }
```

| Quy tắc | |
|---|---|
| **Thay thế toàn bộ** | Hành có trong DB mà thiếu ở body coi như 0 và bị **xóa** — hồ sơ là một phân bố trọn vẹn, sửa lẻ là phá Σ=1 |
| Mỗi hành 1 lần | Trùng hành → 400 |
| `share ∈ [0, 1]` | Ngoài miền → 400 |
| **Σ share = 1 ± 0.001** | Lệch → 400 kèm tổng thực tế. Rỗng `[]` = xóa hồ sơ (nghề về "chưa có hồ sơ", engine bỏ qua) |
| `share = 0` | Bị bỏ qua, không lưu dòng rác |

### Phần khắc mệnh không trội của vật mang theo người *(v3.2 §18)*

| Code | Seed | Ý nghĩa |
|------|:---:|---------|
| `MINOR_CLASH_PENALTY` | **0.60** | Phạt phần hành khắc bản mệnh **không phải hành trội**, theo đúng tỉ trọng. `0` = tắt |

```
clashShare  = Σ product[e]  với mọi e mà GetRelation(mệnh, e) == BiKhac
userPenalty = MINOR_CLASH_PENALTY × clashShare
```

**Chỉ áp cho luồng `Carry`.** Nhánh dụng thần dùng vector Σ=1 **không âm** nên không có trục nào mang
dấu trừ, còn bộ lọc xung khắc lại chỉ so hành TRỘI - một vòng tay `Kim 0.50 / Thủy 0.30 / Hỏa 0.20`
đeo cho người mệnh Kim lọt qua cả hai và **20% Hỏa biến mất không dấu vết**.

Luồng phòng KHÔNG áp: ở đó `d = (1−Wp)·ĝ + Wp·r` với `r` có dấu đã trừ phần khắc theo tỉ trọng rồi,
thêm nữa là đếm hai lần.

⚠️ Hành **trội** khắc mệnh vẫn đi đường cũ (`AlwaysHard` loại thẳng), không rơi vào công thức tỉ
trọng - chuyển hết sang tỉ trọng sẽ giảm phạt của vật 50% Hỏa từ 0.60 xuống 0.30, tức nới lỏng đúng
nhóm cần phạt nặng nhất.

Dòng phạt trong breakdown mang mã riêng `MINOR_CLASH_PENALTY`, nhãn **"Khắc bản mệnh (phần phụ)"** và
câu giải thích nêu đích danh hành nào, bao nhiêu phần trăm.

### Nén tương phản khi dựng `current` *(v3.2 §17)*

| Code | Seed | Ý nghĩa |
|------|:---:|---------|
| `EVIDENCE_SATURATION_ALPHA` | **0.60** | Số mũ nén: `current = normalize(khối lượng^α)`. `1.0` = tắt (tuyến tính như trước) |

```
m[e]    = Σᵢ vᵢ · wᵢ[e]        // khối lượng thô theo PHIẾU — §12 giữ nguyên
current = normalize( m^α )
```

Sửa tật: khai 12 tag Mộc thì Mộc chiếm ~60% hiện trạng và nuốt gần hết bốn hành còn lại. Cảm nhận về
số lượng vốn không tuyến tính (**định luật luỹ thừa Stevens**) — mức dịch tương đối bằng `α · Δm/m`,
tỉ lệ với thay đổi TƯƠNG ĐỐI:

| Mộc đang có | Thêm 2 phiếu | Cảm nhận đổi |
|---:|---:|---:|
| 10 phiếu | +20% | +11.6% |
| 100 phiếu | +2% | +1.2% |

**Bộ hãm MỀM, không phải trần cứng.** Một hành cần **2–3 lần** số tag mới đạt cùng mức áp đảo (Mộc đạt
60%: 13 tag ở α=1, 41 tag ở α=0.6), và thứ tự giữa các hành luôn được giữ.

⚠️ Áp lên **TỔNG của từng hành**, sau khi cộng hết mọi nguồn — không áp riêng cho tag hay riêng cho
nền phòng. Nén một nhóm rồi cộng với nhóm chưa nén là cộng hai hệ đơn vị (`phiếu` vs `phiếu^α`), làm
mất bất biến tỉ lệ, và **đẩy bản mệnh chủ nhân LÊN** chứ không xuống. Chi tiết ở
[§17.4](../adr/score-explainability-v3.2.md).

Hệ quả: nén ép tương phản **giữa các hành**, còn tỉ lệ **giữa các nguồn trong cùng một hành** không
đổi — nên `PERSON_PRESENCE_VOTES_*`/`INTERIOR_PRIOR_VOTES` vẫn là lever đúng để chỉnh sức nặng prior.

> `confidence` tính trên phiếu **thô**, α không chạm tới: nó đo *có bao nhiêu bằng chứng*, không đo
> *nhìn thấy đậm tới đâu*.

### Cờ điều khiển (kill-switch)

| Code | Seed | Ý nghĩa |
|------|:---:|---------|
| `VIBE_FILTER_HARD` | **1.00** | `≥ 0.5` → lệch vibe bị **loại thẳng** (hành vi v3). `< 0.5` → chuyển sang trừ điểm mềm bằng 2 tham số trên |
| `MIN_SCORE_THRESHOLD` | **−1.00** | Cắt theo **điểm tổng** thay vì theo từng thuộc tính. `−1.0` = không cắt. Nâng lên `0.0` khi đã tắt `VIBE_FILTER_HARD` |

> `scoring_params.value` là `decimal` nên cờ được encode thành số. Bật/tắt qua endpoint này, **không cần deploy**.
> Xem [vibe-soft-scoring.md](../adr/vibe-soft-scoring.md) cho quy trình rollout.

> ⚠️ Thiếu row nào → engine dùng **default trong code**, không lỗi.

## Tham số nào đang THỰC SỰ chạy — trace 2026-09-19

Đường đi chung của mọi tham số: `scoring_params` → `ScoringParameters.FromRows` (25/25 code đều map, code
thiếu dùng default trong code) → property → nơi đọc. Bảng dưới là **nơi đọc thật** và **điều kiện để giá trị
có tác dụng**; "ngủ" = code có đọc nhưng với dữ liệu/cấu hình hiện tại nhánh đó không bao giờ chạy.

| Code | Đọc ở | Điều kiện có tác dụng | Trạng thái dev DB |
|---|---|---|---|
| `SELF/SUPPORT/CHILD_SHARE` | `FengShuiCalculator.BuildPersonalVector` ← `PersonPresenceBuilder`, `PersonalTargetBuilder` (fallback Nạp Âm), 4 chỗ `RecommendationService`, `WorkspaceProfileService` | user có DOB | **chạy** (3/5 user có DOB) |
| `MATERIAL_SHARE`, `COLOR_SHARE` | `ProductVectorProvider.Build` tầng 2 (+ `ProductVectorApplier`/`ProductVectorService` khi seller lưu) | sản phẩm có `product_element_inputs`, không override | **chạy** (13/13 sản phẩm) |
| `FALLBACK_PRIMARY/SECONDARY` | `ProductVectorProvider.Build` tầng 3 | sản phẩm **không** có input, không override | ngủ (0 sản phẩm), sẵn sàng khi seller tạo hàng chưa khai chất liệu |
| `PERSONAL_WEIGHT_PRIVATE/SHARED` | `ResolvePersonalWeight` → `ScoringContext.PersonalWeight` → `RecommendationScorer` (`wp` trong `blended`, `ElementDirection.ForWorkspaceGap`, và **nhân vào `USER_CONFLICT_PENALTY`** khi `Scaled`); `WorkspaceProfileService` cho `personalDirection` (hiển thị) | DOB **và** scope Private/Shared **và** placement Desk/Living | **chạy** (2 phòng Private, 2 Shared, chủ có DOB). ⚠️ DB test từng ở `0.000` (seed v3.1 kill-switch, đổi seed không migration) — `ScoringParamsV35` kéo cả `0.000` lẫn `0.500` về 0.30 |
| `TAG_VOTES_CAP` | `BuildCurrentBreakdown` bước 2a qua 5 call site | Σ phiếu tag > cap | **chạy** (phòng "Bàn học gỗ" 8 tag ⇒ `k = 0.625`) |
| `PERSONAL_WEIGHT_PUBLIC` | như trên | scope Public | ngủ theo thiết kế (= 0) |
| `USER_CONFLICT_PENALTY` | `ScoreOne` bước 2b | hành trội sản phẩm khắc mệnh; Public → không áp; Private/Shared có DOB → **× Wp** (`Scaled`); không DOB → mức đầy (`ByScope`); Carry → mức đầy | **chạy**; lưu ý hạ `Wp` 0.5 → 0.3 kéo phạt hiệu dụng 0.30 → 0.18 |
| `MINOR_CLASH_PENALTY` | `ScoreOne` 2b nhánh else | chỉ `Carry` (`PersonalNeed`), hành trội không khắc nhưng phần phụ khắc | **chạy** ở luồng personal |
| `DIRECTION_PENALTY` | `ValidateDirection` | `PlacementPolicy.Direction == Soft` (chỉ **Desk**) và mọi hướng hợp đều bị chắn (`dark_directions`/cửa/WC) | **chạy** cho Desk, không bao giờ cho Living/Carry |
| `VIBE_FILTER_HARD` | `ScoreOne` 2a | `TargetVibe(purpose)` có (Office/Study/…) và `policy.UsePurposeVibe` | **chạy** = 1.0 ⇒ lệch vibe bị **loại thẳng** ở Rank; Fit không loại, không trừ |
| `VIBE_MISMATCH_PENALTY`, `VIBE_UNKNOWN_PENALTY` | cùng chỗ | **chỉ khi `VIBE_FILTER_HARD < 0.5`** | **ngủ** — seed 1.0 nên hai số này chưa từng vào điểm; đổi chúng không có tác dụng gì cho tới khi hạ cờ |
| `MIN_SCORE_THRESHOLD` | `ScoreOne` sau clamp, chỉ `Rank` | `score < ngưỡng` | **ngủ** theo thiết kế (−1.0 = không cắt) |
| `CARRY_PRIMARY/SECONDARY_SHARE` | `PersonalTargetBuilder` nhánh Tứ Trụ | user có **giờ sinh** (`birth_time`) | chạy khi có giờ sinh; thiếu giờ → fallback Nạp Âm dùng `SELF/SUPPORT/CHILD` |
| `INTERIOR_PRIOR_VOTES`, `EVIDENCE_SATURATION_ALPHA` | `BuildCurrentBreakdown` qua 5 call site (`RecommendationService` ×3, `WorkspaceProfileService` ×2) | luôn | **chạy** |
| `PERSON_PRESENCE_VOTES_PRIVATE/SHARED` | `PersonPresenceBuilder.Build` → `BuildCurrentBreakdown` | DOB và scope | **chạy** |
| `PERSON_PRESENCE_VOTES_PUBLIC` | như trên | scope Public | ngủ theo thiết kế (= 0) |
| `OCCUPATION_WEIGHT` | `LoadOccupationAsync` (≤ 0 ⇒ null), `OccupationWeightFor(Wp)`, `OccupationAxis.Build` | > 0 **và** user có `occupation_id` **và** hồ sơ nghề Σ=1 | **chạy** từ 19/09 (seed 0.20) — nhưng 0/5 user dev đã khai nghề nên chưa có phiên nào đi qua |

Tóm lại, với cấu hình hiện tại **2 tham số không có tác dụng dù đổi giá trị**: `VIBE_MISMATCH_PENALTY`,
`VIBE_UNKNOWN_PENALTY` (bị `VIBE_FILTER_HARD = 1.0` che). Ba tham số
`= 0` theo thiết kế (`*_PUBLIC`, `MIN_SCORE_THRESHOLD = −1`). Còn lại đều đang chạy. Bảy dòng `BUDGET_*` từng
nằm trong DB không được code nào đọc — đã gỡ (migration `ElementInputMapWeightScaleRevert`).

## Map ngũ hành — `element_input_map`

`data` = `ElementInputMapDto`: `{ id, inputKind, inputCode, labelVi, visibility, element, weight }`.
**PUT** body (`UpsertElementInputMapRequest`):
```json
{ "inputKind": "Material", "inputCode": "Wood", "labelVi": "Gỗ", "element": "Moc", "weight": 1.0 }
```
| Field | Ghi chú |
|-------|---------|
| `inputKind` | enum `ElementInputKind`: `Color` / `Material` / `Shape` / `DecorItem` |
| `inputCode` | mã bất biến, vd `Red`, `Wood`, `SaltRock`, `Sphere` |
| `labelVi` | **nhãn tiếng Việt user nhìn thấy** (picker, tooltip radar, 3 dòng nhận định). Bỏ trống khi PUT = giữ nhãn cũ; có giá trị = áp cho **mọi hành** của cùng code |
| `element` | `Kim/Moc/Thuy/Hoa/Tho` |
| `weight` | đóng góp vào hành (mặc định 1.0). Một `(kind, code)` có thể trải nhiều hành |

> Dùng chung cho cả phòng (`workspace_profile_inputs`) và sản phẩm (`product_element_inputs`).

### Tag gộp — `element-input-tags` (dùng cho màn quản trị)

Một **tag** = 1 cặp `(inputKind, inputCode)`, gồm nhiều row (mỗi hành 1 row). Admin nên sửa theo tag
chứ không sửa từng row rời — nếu không, `labelVi` của cùng 1 code dễ lệch nhau giữa các hành.

**GET** `?kind=&visibility=&isUserCreated=` → mảng `ElementInputTagDto`:
```json
{
  "inputKind": "DecorItem", "inputCode": "Stove", "labelVi": "Bếp nấu",
  "visibility": "Public", "createdBy": null, "isUserCreated": false, "isPending": false,
  "contributions": [{ "id": "guid", "element": "Hoa", "weight": 1.0 }],
  "totalWeight": 1.0, "updatedAt": "..."
}
```
**PUT** body (`UpdateElementInputTagRequest`) — mọi field đều optional, bỏ trống = giữ nguyên:
```json
{ "labelVi": "Bếp nấu", "visibility": "Public",
  "contributions": [{ "element": "Hoa", "weight": 0.7 }, { "element": "Tho", "weight": 0.3 }] }
```
| Field | Ghi chú |
|-------|---------|
| `labelVi` | Áp cho **mọi hành** của tag |
| `contributions` | **Thay thế** toàn bộ phân bổ. Hành có trong DB mà thiếu ở đây sẽ bị **xóa**. Mỗi hành chỉ khai 1 lần, `weight > 0` |
| `visibility` | `Pending` / `Personal` / `Public` — xem mục dưới |
| `totalWeight` (GET) | Σ weight = số **"phiếu"** tag bỏ vào vector hiện trạng phòng. Chuẩn **1.0**; khác 1.0 = tag nặng/nhẹ hơn tag khác (hợp lệ, nhưng FE cảnh báo) |

> Tag nào cũng có nhiều row (mỗi hành 1 row). `visibility` của tag = **mức thấp nhất** trong các row,
> tránh tag nửa công khai nửa riêng tư khi dữ liệu bị sửa tay ngoài API này.

> ⚠️ Xóa tag: `workspace_profile_inputs` đang trỏ tới code đó sẽ **lặng lẽ mất** khỏi vector phòng
> (không lỗi). Với tag đã phổ biến, nên chuyển sang **`Personal`** thay vì xóa.

### Phạm vi hiển thị `visibility` — tag do user tự tạo

Tag user gõ ở bước intake (`POST /api/workspace/element-inputs/classify`) được lưu với
`visibility = Pending` và `created_by = <user>`.

| Giá trị | Ý nghĩa | Ai thấy trong picker |
|---------|---------|----------------------|
| `Pending` | User vừa tạo, admin **chưa xem** | Người tạo |
| `Personal` | Admin **đã xem**, quyết định giữ riêng | Người tạo |
| `Public` | Tag chính thức (seed, admin thêm, hoặc đã duyệt) | Mọi user |

> `Pending` và `Personal` **hiển thị giống hệt nhau**. Tách ra để **hàng đợi duyệt (= `Pending`)
> luôn rút được về 0** — nếu chỉ có cờ boolean thì tag admin đã xem nhưng cố ý không duyệt sẽ nằm
> lại hàng đợi vĩnh viễn và badge đếm mất ý nghĩa.

Bộ lọc chỉ áp ở **tầng khám phá**, không áp ở **tầng sử dụng** — nếu lọc cả tầng sử dụng thì tag
riêng của user sẽ biến mất khỏi radar của chính họ:

| Tầng | Nguồn | Lọc |
|------|-------|-----|
| Picker hiện trạng + prompt AI intake | `GET /api/workspace/element-inputs`, `WorkspaceIntakeService` | `ElementInputMap.IsVisibleTo(userId)` = `Public \|\| createdBy == me` |
| Form gắn tag sản phẩm (vendor) | `TaxonomyService.GetElementInputCodesAsync` | `IsPublic` |
| Resolver / chấm điểm / validate khi lưu workspace | `ElementInputResolver`, `ResolveValidInputsAsync` | **không lọc** |

Quy tắc khám phá nằm **một chỗ duy nhất** ở `ElementInputMap.IsVisibleTo(Guid)` — service gọi lại
đó thay vì tự viết điều kiện, tránh lệch nhau.

Migration `20260904030000_ElementInputApproval` backfill toàn bộ row đang có `= 'Public'` → không đổi hành vi dữ liệu cũ.

## Modifier Intent — `work_purpose_element_modifiers`

`data` = `WorkPurposeModifierDto`: `{ id, workPurpose, element, delta }`.
**PUT** body (`UpsertWorkPurposeModifierRequest`):
```json
{ "workPurpose": "Study", "element": "Thuy", "delta": 0.10 }
```
Bẻ vector lý tưởng theo mục đích. `delta` **có thể âm**.

## Vector loại phòng — `workspace_type_elements`

`data` = `WorkspaceTypeElementDto`: `{ id, workspaceTypeId, source, element, weight }`.
**PUT** body (`UpsertWorkspaceTypeElementRequest`):
```json
{ "workspaceTypeId": "guid", "source": "Ideal", "element": "Moc", "weight": 0.5 }
```
| Field | Ghi chú |
|-------|---------|
| `source` | `Ideal` (vector lý tưởng cần đạt) hoặc `Interior` (hiện trạng nội thất mặc định) |
| `weight` | trọng số hành trong bộ; mỗi `(type, source)` nên Σ≈1 |

---

[← Dev Tools](./24-dev-tools.md) · [Tiếp: Appendix →](./99-appendix-models.md)
