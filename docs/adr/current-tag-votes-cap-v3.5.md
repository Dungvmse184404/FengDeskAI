# ADR — v3.5: cap phiếu tag trong `current` + hạ trọng số bản mệnh ở phòng riêng

> **Status:** **Implemented (2026-09-19)** — BE + FE + test + migration `ScoringParamsV35` xong; golden set byte-identical,
> unit 363/363, API workspace/scoring/customer-care 102/103 (DEF-12 pre-existing). Nối tiếp v3.2 §12/§17/§19 (mô hình phiếu) và
> v3.1 §3.2 (`PERSONAL_WEIGHT_<scope>`). Từ vựng: [`docs/glossary-scoring.md`](../glossary-scoring.md) §3a, §7.

## 1. Vấn đề

### 1.1 Tag không có trần, nên đè mọi nguồn khác

`current` là trung bình theo phiếu rồi nén `α` (glossary §3a):

```
m[e] = 3·interior[e] + votes_chủNhân·pv[e] + Σ_tag votes_tag·v_tag[e] + Σ_sp voteWeight·p_sp[e]
```

Nền phòng (3) và chủ nhân (3/2/0) là số cố định; sản phẩm đặt vào phòng 1 phiếu/món; **tag là nguồn duy
nhất tăng không giới hạn**. Phòng "Bàn học gỗ" (dev DB, 8 tag, Private): 3 + 3 + 8 = 14 phiếu — sản phẩm
đang xem trước chiếm 1/15 khối lượng thô, radar "xem trước" nhích ~6 điểm % trên trục nó bù. Khai 20 tag thì
1/27, gần như không nhìn thấy. Đây là hệ quả trực tiếp của triết lý §12 *"bằng chứng lấn át suy đoán"* —
đúng cho câu "phòng đang thế nào", nhưng làm sản phẩm (thứ shop bán) và bản mệnh (thứ user quan tâm) biến mất
khỏi đồ thị khi user chăm khai.

> Bug dữ liệu `weightScale = 8.0` (tag seed sau 09-03 nặng 8 phiếu) đã sửa riêng bằng migration
> `20260919070344_ElementInputMapWeightScaleRevert` — ADR này tính trên thang tag ≈ 1 phiếu **sau** sửa.

### 1.2 Ở phòng riêng, mệnh thắng phòng ⇒ điểm ngược đồ thị

`Wp = 0.5` (Private) nghĩa là **một nửa** điểm là `r·p` (quan hệ với bản mệnh), không liên quan phòng. Cùng
phòng trên, chủ nhân mệnh Kim, sản phẩm 100 % Kim:

| | `ĝ·p` (phòng) | `r·p` (mệnh) | `blended` | hiển thị | radar / compat |
|---|---|---|---|---|---|
| `Wp = 0.5` | −0.57 | +1.0 | **+0.21** | **61 % "Phù hợp"** | "Thêm thừa", 88 → 86 ⬇ |
| `Wp = 0.3` | −0.57 | +1.0 | **−0.10** | 45 % "Trung tính" | khớp |

Cùng màn hình: badge nói hợp, nhãn từng hành nói thừa, lớp "Ưu tiên của bạn" (phần dương của `d`) vẫn có Kim.
Không phải bug tính (glossary §7.4 — vòng sinh/khắc, `r`, `ô` đều đúng) mà là **trọng số cho mệnh quá lớn so
với nhiệm vụ chính của engine là bù phòng**.

## 2. Quyết định

### 2.1 `TAG_VOTES_CAP = 5` — trần cho **tổng** phiếu tag

```
tagVotes = Σ_tag votes_tag                          (votes_tag = Σ weight code trong element_input_map)
k        = tagVotes > CAP ? CAP / tagVotes : 1
m[e]     = 3·interior[e] + votes_chủNhân·pv[e] + k·Σ_tag votes_tag·v_tag[e] + Σ_sp voteWeight·p_sp[e]
```

- Cap theo **Σ phiếu**, không theo *số* tag: admin giảm weight một tag thì phiếu giảm theo (đã là chủ đích ở
  §12) và cap vẫn nhất quán. Với tag = 1.0 thì hai cách trùng: 7 tag ⇒ mỗi tag 5/7.
- Áp **đều** cho mọi tag (một hệ số `k`), không ưu tiên loại nào — tỉ lệ *giữa các tag* giữ nguyên, nên tooltip
  "tag nào chiếm bao nhiêu %" trên radar không đổi thứ tự.
- Liên tục tại `tagVotes = CAP` (k = 1), không có bậc thang.
- **Sản phẩm đã đặt không vào cap**: phòng hiếm khi có > 5 món, và đó chính là nguồn ta muốn giữ hiện diện.
- Nền phòng và chủ nhân không đổi (3 / 3·2·0). Hệ quả chấp nhận rõ ràng:

| Private, 20 tag | trước | sau |
|---|---|---|
| phần tag trong `m` | 20/26 = 77 % | 5/11 = **45 %** |
| phần chủ nhân tối thiểu | → 0 khi tag ↑ | ≥ 3/11 = 27 % |
| một sản phẩm 1 phiếu thêm vào | 1/27 | 1/12 |

  Tức là §12 được **sửa thành "bằng chứng tối đa ngang nền + chủ nhân"**. Radar mô tả phòng đầy đồ kém
  trung thực hơn (một phòng 20 tag gỗ vẫn không thể > 45 % Mộc từ tag), đổi lại sản phẩm và bản mệnh luôn
  nhìn thấy được. Chấp nhận vì mục đích của màn hình là **bán đúng món**, không phải kiểm kê phòng.
- Một con số cho mọi scope (cùng lý lẽ đã chọn cho `OCCUPATION_WEIGHT`). `CAP ≤ 0` = tắt (kill-switch).
- `EvidenceCount` / `Confidence` **không** đổi: đếm số bằng chứng thật, không đếm phiếu — 7 tag vẫn là 7
  bằng chứng, chỉ *sức nặng* bị cap.
- `ShareOf` đọc `Votes` đã nhân `k` ⇒ Σ phần của mọi nguồn = `Current[e]` vẫn khít (radar xếp chồng đúng).
- Bất biến tỉ lệ của §17 (nhân đôi mọi phiếu ⇒ cùng `current`) chỉ còn đúng khi `tagVotes ≤ CAP` — ghi vào
  glossary, test `SCORE-SAT-SCALE` giữ nguyên vì nó chạy với cap tắt.

### 2.2 `PERSONAL_WEIGHT_PRIVATE` 0.50 → **0.30**, `PERSONAL_WEIGHT_SHARED` 0.30 → **0.20**

- Phòng vẫn là số hạng lớn nhất ở mọi scope (≥ 70 %); mệnh là hiệu chỉnh, không phải nửa điểm.
- Giữ bậc Private > Shared > Public (0.30 > 0.20 > 0). Public không đổi.
- Kẹp `Wo ≤ 1 − Wp` (v3.4) không bị ảnh hưởng; khi bật `Wo = 0.2` thì Private còn `0.5·ĝ + 0.3·r + 0.2·ô`.
- **Hệ quả phụ có chủ đích:** `USER_CONFLICT_PENALTY` nhân với `Wp` ở chế độ `Scaled` (v3.2 §14.2) ⇒ phạt
  khắc mệnh hiệu dụng ở Private 0.6·0.5 = 0.30 → 0.6·0.3 = **0.18**. Nhất quán với lý do hạ `Wp` (mệnh là
  hiệu chỉnh); muốn phạt nặng hơn thì nâng `USER_CONFLICT_PENALTY`, không nâng `Wp`.
- **Phát hiện khi chạy migration:** DB test có `PERSONAL_WEIGHT_PRIVATE = 0.000` — seed v3.1 kill-switch, sau đó
  seed đổi lên 0.50 mà không có migration nên môi trường seed trước vẫn 0 (API test chạy với trục cá nhân
  TẮT tới 2026-09-19). `ScoringParamsV35` canh **cả** `0.000` lẫn `0.500` để mọi môi trường về 0.30.
- Đây là đổi **số**, không đổi công thức — không cần version mới cho riêng nó; nhưng vì đi cùng 2.1 nên gộp
  một migration.

### 2.3 Con số sau khi đổi — phòng "Bàn học gỗ", chủ nhân Kim, Private, sản phẩm 1 phiếu

| sản phẩm 100 % | trước (`Wp .5`, không cap) | sau (`Wp .3`, cap 5) | radar |
|---|---|---|---|
| Kim | 61 % Phù hợp | **42 % Trung tính** | Thêm thừa, compat 88.8 → 86.2 |
| Thủy | 61 % Phù hợp | 67 % Phù hợp | Bù tốt, 88.8 → 91.3 |
| Thổ | 78 % Phù hợp | 74 % Phù hợp | 88.8 → 90.7 |
| Hỏa | 0 % Cân nhắc | 0 % Cân nhắc | 88.8 → 85.1 |

Ca ngược (Kim) biến mất; thứ hạng Thổ > Thủy > Mộc > Kim > Hỏa giữ nguyên. Phòng này chỉ 8 tag nên cap
ảnh hưởng nhẹ (k = 5/8); tác dụng của cap lộ rõ ở phòng > 10 tag.

## 3. Thay đổi code

### 3.1 Engine (`src/FengDeskAI.Application/Features/CustomerCare/Engine/`)

| File | Đổi |
|---|---|
| `ScoringModels.cs` | `ScoringParamCodes.TagVotesCap = "TAG_VOTES_CAP"`; `ScoringParameters.TagVotesCap = 5.00m` (+ `FromRows`); `PersonalWeightPrivate = 0.30m`, `PersonalWeightShared = 0.20m` (default **phải khớp seed**) |
| `ElementVectorBuilders.cs` | `BuildCurrentBreakdown(..., decimal? tagVotesCap = null)`: sau bước 2 (tag) tính `tagVotes`; nếu `cap > 0 && tagVotes > cap` thì thay mỗi `CurrentContribution` tag bằng bản `Votes × cap/tagVotes` (record `with`). `BuildCurrentWithProducts` nhận và chuyền tham số. Thêm `CurrentBreakdown.TagVotesScale` (= k, 1 khi không cap) để API/FE giải thích |
| `WorkspaceElementAnalyzer.cs` | `Analyze(..., decimal? tagVotesCap = null)` chuyền xuống |
| `Domain/Entities/CustomerCare/ScoringFormulaVersions.cs` | `V35 = "3.5"`, `Current = V35` — cap đổi `current` ⇒ đổi `gap` ⇒ đổi điểm, là công thức mới |

Không có `switch` mới, không đụng `RecommendationScorer` / `ElementDirection` / `OccupationAxis`.

### 3.2 Service — 5 chỗ gọi, truyền `prms.TagVotesCap`

| File | Dòng gọi |
|---|---|
| `CustomerCare/Services/RecommendationService.cs` | `GetProductFitAsync`: `previewCurrent` và `currentBreakdown`; `LoadWorkspaceContextAsync`: `WorkspaceElementAnalyzer.Analyze(...)` |
| `Workspace/Services/WorkspaceProfileService.cs` | `GetElementAnalysisAsync`: `breakdown` và `previewCurrent` |

Năm chỗ **phải cùng giá trị** — radar phòng, radar xem trước, engine xếp hạng và trang fit đang dùng chung
một hàm chính vì lý do này (§19).

### 3.3 DTO

- `CurrentBreakdownMapping` / `WorkspaceElementAnalysisResponse` / `ProductFitResponse`: thêm
  `tagVotesScale` (decimal, 1.0 khi không cap) — FE ghi chú "N tag đang tính bằng 5 phiếu" trong tooltip radar.
  Không bắt buộc cho đúng số; là phần **giải thích** (cùng tinh thần v3.2 §9).

### 3.4 Dữ liệu

- `seed-data/scoring-params.json`: thêm `TAG_VOTES_CAP = 5.00`; sửa `PERSONAL_WEIGHT_PRIVATE = 0.30`,
  `PERSONAL_WEIGHT_SHARED = 0.20` (+ description).
- Migration `ScoringParamsV35` (mẫu `ScoringPenaltiesV32` — canh giá trị cũ để không đè hiệu chỉnh admin):
  ```sql
  UPDATE scoring_params SET value = 0.30 WHERE code = 'PERSONAL_WEIGHT_PRIVATE' AND value = 0.50 AND is_deleted = false;
  UPDATE scoring_params SET value = 0.20 WHERE code = 'PERSONAL_WEIGHT_SHARED'  AND value = 0.30 AND is_deleted = false;
  INSERT INTO scoring_params (...) SELECT 'TAG_VOTES_CAP', 5.00, ... WHERE NOT EXISTS (...);
  ```
  `Down` đảo ngược với cùng kiểu canh (`0.30 → 0.50`, `0.20 → 0.30`, xoá `TAG_VOTES_CAP`). `ScoringParamSeeder`
  chỉ chèn row thiếu nên không đủ để đổi giá trị đã có — vì thế cần migration.

### 3.5 Test

| Test | Nội dung |
|---|---|
| `UnitTests/TagVotesCapTests.cs` `CAP-01..07` | ≤ cap: byte-identical với không cap · 7 tag ⇒ mỗi tag 5/7, Σ = 5 · liên tục tại 5 · `EvidenceCount` vẫn 7 · `Σ ShareOf = Current[e]` · sản phẩm không bị cap · `cap ≤ 0` = tắt · default khớp seed (kiểu `SCORE-PARAM-03`) |
| `UnitTests/RecommendationGoldenSetTests.cs` | **không đổi**: ca dựng `Wp = 0.50m` tường minh và `Current` cho sẵn — golden set byte-identical; ghi chú ở đầu file rằng seed thật giờ là 0.30 |
| `UnitTests/EvidenceSaturationTests.cs` | không đổi (cap tắt); thêm 1 ca `SCORE-SAT-CAP`: 17 tag + cap 5 ⇒ Mộc ngập nước bị kìm thêm |
| `ApiTests/WorkspaceFlowTests.cs` | phòng 7 tag: `contributions` tag có `votes` Σ = 5, `totalVotes` = 3 + phiếu chủ nhân + 5, `evidenceCount` = 7, `tagVotesScale` = 5/7 |
| `ApiTests/ScoringConfigFlowTests.cs` | `GET params` có `TAG_VOTES_CAP`; PUT 0 ⇒ phòng 7 tag `totalVotes` về 3 + N + 7 |
| `ApiTests/CustomerCareFlowTests.cs` | ca Private đang dựa `Wp = 0.5` (nếu có) cập nhật kỳ vọng |

### 3.6 FE (`FengDeskAI_FE/src/features/recommendation/`)

- **Không đổi toán**: `lib/breakdown.ts::simulateVotes` dựng lại `current` từ `votes`/`totalVotes` BE trả về
  (đã nhân `k`), nên slider phiếu chủ nhân vẫn đúng. `combinedDirection` không dính `current`.
- `components/element-vector/RoomPersonalWeightControls.tsx`: sửa chú thích "mỗi tag 1 phiếu" → "tag ≈ 1
  phiếu, tổng tag cap 5".
- Tooltip radar (`ElementRadarChart.tsx`): khi `tagVotesScale < 1` thêm một dòng nhỏ "N tag đang tính bằng
  5 phiếu (cap)". Nhỏ, không bắt buộc.
- `types/recommendation.d.ts` + `users/types/workspace.d.ts`: thêm `tagVotesScale?: number`.

### 3.7 Docs sync

`glossary-scoring.md` (§3a công thức `k`, §7.1, §7.3 đóng, §7.4 ca 1 đóng, §8.1 tham số, phiên bản 3.5) ·
`docs/api-documents/25-scoring-config.md` (bảng tham số) · `docs/api-documents/18-recommendations.md`
(`tagVotesScale`) · `docs/ard/bounded-contexts/customer-care.md` · `docs/adr/README.md` · `seed-data/README.md`.

## 4. Đã cân nhắc và bỏ

| Phương án | Vì sao không |
|---|---|
| Cap mềm `votes = 5·(1 − e^(−n/5))` | Không giải thích được cho user bằng một câu; cap cứng "tối đa 5 phiếu" thì được |
| Cap riêng từng loại tag (Color/Material/Shape/DecorItem) | 4 tham số cho một ý; và user không phân biệt loại khi khai |
| Cap gộp cả sản phẩm đã đặt | Sản phẩm là thứ cần *giữ* hiện diện; gộp vào là tự làm loãng mục tiêu |
| Tăng phiếu sản phẩm (`PLACED_PRODUCT_VOTES = 2`) thay cho cap | Chữa triệu chứng ở một nguồn, tag vẫn đè nền + chủ nhân; giữ làm bước sau nếu cap chưa đủ |
| Giữ `Wp = 0.5`, tách badge "hợp phòng / hợp bạn" | Con số vẫn ngược, chỉ được giải thích; và UI thêm một khái niệm |
| Đổi `gapScore` sang **lượng** (`|gap|₁ − |previewGap|₁`) | Đúng nghĩa nhất, nhưng là công thức mới thật sự (đổi cách hiểu "phù hợp"); để v3.6 sau khi thấy cap + `Wp` chưa đủ |

## 5. Kế hoạch

| Bước | Việc | Ước |
|---|---|---|
| P7.1 | ✅ Param + seed + migration `ScoringParamsV35` (canh cả 0.000 lẫn 0.500); default code khớp seed | ✅ |
| P7.2 | ✅ `BuildCurrentBreakdown` cap + `TagVotesScale`; 5 call site; version 3.5 | ✅ |
| P7.3 | ✅ Unit `CAP-01..10` (`TagVotesCapTests.cs`); API `WS-19b`, `SCORE-01b`; golden set byte-identical; `SCORE-SAT-CAP` gộp vào CAP-04 | ✅ |
| P7.4 | ✅ DTO `tagVotesScale`; FE tooltip radar (`ElementRadarChart`), types, chú thích `RoomPersonalWeightControls`; docs sync | ✅ |
