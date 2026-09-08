# 18 — Recommendations

[← Mục lục](./README.md)

Controller: `RecommendationsController` · Route gốc: `/api/recommendations` · **Toàn bộ `[Authorize]`** — user chỉ truy cập gợi ý của chính mình.

Gợi ý vật phẩm phong thủy: **engine .NET chấm điểm deterministic**, **AI chỉ diễn giải**.

Có **hai luồng gợi ý** tách biệt, chọn theo `ProductPlacement` của sản phẩm:

| Luồng | Endpoint | Chấm theo | Placement được xét |
|---|---|---|---|
| **Workspace** | `POST /api/recommendations` | Gap ngũ hành của **phòng** | `Desk`, `Living` |
| **Cá nhân** | `POST /api/recommendations/personal` | Dụng thần / bản mệnh của **người** | `Carry` |

`Consumable` (nhang, nến, muối) **không vào bất kỳ luồng gợi ý nào** — vẫn tìm & mua bình thường qua [Products](./02-products.md).

> ⚙️ Cấu hình engine (tham số, map ngũ hành, modifier…) do admin quản qua [Scoring Config](./25-scoring-config.md).
> 📐 Thiết kế: [recommendation-scoring-v3](../adr/recommendation-scoring-v3.md) · [product-placement-personal-recommendation](../adr/product-placement-personal-recommendation.md) · [vibe-soft-scoring](../adr/vibe-soft-scoring.md)

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| POST | `/api/recommendations` | Authenticated | Tạo phiên gợi ý **cho một workspace** |
| POST | `/api/recommendations/personal` | Authenticated | Tạo phiên gợi ý **vật phẩm mang theo người** |
| GET | `/api/recommendations/{id}` | Authenticated | Lấy lại phiên gợi ý đã lưu (cả hai loại) |
| GET | `/api/recommendations/fit` | Authenticated | Độ phù hợp của 1 sản phẩm × 1 workspace (trang chi tiết sản phẩm) |
| GET | `/api/recommendations/fit/personal` | Authenticated | Độ phù hợp của 1 sản phẩm với **bản mệnh** user — không cần workspace (vật mang theo người) |

---

## Cách engine chấm điểm

Mệnh của **phòng**, của **người** và của **sản phẩm** đều quy về **vector 5 hành** (`element_kim/moc/thuy/hoa/tho`).

### Luồng workspace

1. **Vector phòng** = `Ideal` (theo loại phòng) → **ApplyIntent** (bẻ theo `WorkPurpose`, bảng `work_purpose_element_modifiers`) → so với **Current** (màu/vật liệu user khai, hoặc Interior mặc định).
2. **Gap** = `adjustedIdeal − current` → phòng **thiếu** (gap > 0) hay **thừa** (gap < 0) hành nào.
3. **Điểm khớp phòng** `ĝ·p` = `gap · productVector / (|gap|₁ / 2)`. Bơm vào hành thiếu → dương; bơm vào hành đã thừa → âm. Miền **`[−1, +1]`**.
4. **Điểm hợp mệnh** `r·p` = `Σ productVector[e] × ruleScore(mệnh, e)` — bảng `feng_shui_rules`, **có dấu** (tỷ hòa +1.0 … bị khắc −1.0).
5. **Trộn theo `WorkspaceScope`**:

```
d     = (1 − Wp)·ĝ + Wp·r          // vector hướng tổng hợp, mỗi trục ∈ [−1, +1]
score = clamp( productVector · d − userPenalty − dirPenalty − vibePenalty , −1, 1 )
```

| `WorkspaceScope` | `Wp` (`PERSONAL_WEIGHT_*`) — **giá trị đích** | Seed hiện tại |
|---|---:|---:|
| `Private` | 0.50 | **0.00** |
| `Shared` | 0.30 | **0.00** |
| `Public` | 0.00 | 0.00 |

> ⚠️ **Seed cả ba = 0.00** — trục cá nhân đang **TẮT**, điểm hiện là 100% nhu cầu phòng. Bật bằng
> `PUT /api/admin/scoring/params/PERSONAL_WEIGHT_PRIVATE` (và `_SHARED`) sau khi đối chiếu golden set;
> không cần deploy. Đừng đọc cột "giá trị đích" như thể nó đang chạy.

`Wp = 0` **bắt buộc** khi user chưa có `dateOfBirth` → điểm = 100% nhu cầu phòng. Đây cũng là công tắc
TẮT của v3.1/v3.2: ở `Wp = 0` luật khắc bản mệnh rơi trọn về v3 (`Private` loại cứng), **trừ**
`Public` — không gian chung không lọc cũng không phạt theo bản mệnh một người.

### Luồng cá nhân (`Carry`)

Vector mục tiêu là **thứ NGƯỜI đang cần**, không phải phòng:

| Dữ liệu có | Nguồn | Ghi trong `personalTarget.source` |
|---|---|---|
| Ngày sinh **+ giờ sinh** | **Dụng thần Tứ Trụ** (`CARRY_PRIMARY_SHARE` / `CARRY_SECONDARY_SHARE`) | `TuTru` |
| Chỉ ngày sinh | **Nạp Âm** (bản mệnh 0.60 / hành sinh 0.30 / hành được sinh 0.10) | `NapAm` |
| Không có ngày sinh | — | ❌ trả `422` kèm hướng dẫn, **không chấm bừa** |

Điểm = `target · productVector / |target|₁` — **cùng thang `[-1, 1]`** với luồng workspace.
`Wp` **không** áp cho luồng này: mục tiêu đã 100% cá nhân, trộn thêm là tính hai lần.

### Luật theo `ProductPlacement`

| Placement | Vector mục tiêu | Hướng đặt | Lọc khắc mệnh | Lọc vibe theo `WorkPurpose` |
|---|---|---|---|---|
| `Desk` *(mặc định)* | Gap phòng | mềm: `DIRECTION_PENALTY` + `placementHint` | theo `WorkspaceScope` | có |
| `Living` (cây) | Gap phòng | **không xét** (đặt theo ánh sáng) | theo `WorkspaceScope` | có |
| `Carry` | Dụng thần người | **không xét** | **cứng, bất kể scope** | **không** |
| `Consumable` | — | — | — | — *(loại khỏi cả hai luồng)* |

### Trừ điểm & lưới an toàn

| Điều kiện | Tham số |
|---|---|
| Sản phẩm **có** vibe nhưng lệch mục đích phòng | `VIBE_MISMATCH_PENALTY` |
| Sản phẩm **chưa khai** vibe nào | `VIBE_UNKNOWN_PENALTY` *(nhẹ hơn — thiếu dữ liệu ≠ lệch thật)* |
| Hành trội khắc bản mệnh — `Wp = 0` (scope `Shared`/`Public`) | `USER_CONFLICT_PENALTY` **đầy đủ** |
| Hành trội khắc bản mệnh — `Wp > 0` | `USER_CONFLICT_PENALTY × Wp` *(`PersonalConflictMode.Scaled`)* |
| Mọi hướng hợp vật phẩm đều bị chắn (cửa / WC / góc tối) | `DIRECTION_PENALTY` |

- **`VIBE_FILTER_HARD`** ≥ 0.5 → lệch vibe bị **loại thẳng** (hành vi v3). < 0.5 → chuyển sang **trừ điểm mềm**.
- **`MIN_SCORE_THRESHOLD`** → cắt theo **điểm tổng** thay vì theo từng thuộc tính. Mặc định `−1.0` = không cắt.

Điểm cuối kẹp `[-1, 1]`, sắp giảm dần, lấy `topN`.

> Mệnh nhập tay `workspaceProfile.fengShuiElement` là **legacy**, engine **không dùng**.

### Giải thích điểm — `breakdown` *(v3.2)*

`GET /fit` và `GET /fit/personal` trả kèm `breakdown` để FE nói được **con số đến từ đâu**:

> ⚠️ `POST /api/recommendations` (danh sách) **chưa** trả `breakdown` cho từng item — sẽ thêm khi FE
> dùng tới danh sách. Hai endpoint `fit` là nơi màn hình đang thật sự hiển thị điểm.

```jsonc
"breakdown": {
  "formulaVersion": "3.2", "target": "WorkspaceGap", "placement": "Living", "displayPercent": 90,
  "components": [
    { "code": "GAP_SCORE",      "labelVi": "Khớp nhu cầu của phòng", "value": 0.600, "weight": 0.50, "contribution": 0.300, "reasonVi": "…" },
    { "code": "PERSONAL_SCORE", "labelVi": "Hợp bản mệnh của bạn",   "value": 1.000, "weight": 0.50, "contribution": 0.500, "reasonVi": "…" }
  ],
  "penalties": [
    { "code": "USER_CONFLICT_PENALTY", "labelVi": "Khắc bản mệnh",     "value": 0.000, "applied": false, "reasonVi": "…" },
    { "code": "DIRECTION_PENALTY",     "labelVi": "Hướng hợp bị chắn", "value": 0.000, "applied": false },
    { "code": "VIBE_MISMATCH_PENALTY", "labelVi": "Lệch vibe mục đích","value": 0.000, "applied": false }
  ],
  "rawScore": 0.800, "clamped": false, "score": 0.800,
  "personalWeight": { "value": 0.50, "code": "PERSONAL_WEIGHT_PRIVATE", "scope": "Private", "reasonVi": "Phòng riêng tư — ưu tiên bản mệnh chủ nhân ngang với nhu cầu phòng." },
  "vectors": {
    "product":         [ { "element": "Moc", "value": 1.00 }, … ],
    "normalizedGap":     [ … ],   // ĝ = gap / (|gap|₁/2), mỗi trục ∈ [−1,+1]
    "ruleScore":         [ … ],   // r' = điểm quan hệ ĐÃ tính nghề nghiệp — null khi trục cá nhân tắt
    "baseRuleScore":     null,    // r TRƯỚC delta nghề — null khi nghề nghiệp không áp
    "occupationShift":   null,    // r' − r — lớp radar nghề nghiệp, null khi nghề nghiệp không áp
    "combinedDirection": [ … ],   // d = (1−Wp)·ĝ + Wp·r' — thứ thật sự nhân với productVector
    "priorityVector":    [ … ],   // normalize(max(d, 0)) — lớp radar "Ưu tiên của bạn", Σ=1
    "personalNeed":      null     // chỉ luồng Carry
  },
  "occupation": null,           // null khi nghề nghiệp không đổi được gì — xem mục dưới
  "conflictResolution": {       // null khi không có xung khắc
    "roomNeed": "Kim", "destiny": "Moc", "bridge": "Thuy",
    "reasonVi": "Phòng đang thiếu Kim, nhưng Kim khắc bản mệnh Mộc của bạn. Hệ thống ưu tiên vật hành Thủy — Kim sinh Thủy, Thủy sinh Mộc — bù cho phòng mà vẫn nuôi bản mệnh."
  }
}
```

**Bất biến** (khoá bằng unit test `SCORE-BD-01`, quét >500 tổ hợp):
```
Σ components[i].contribution                             == blended
productVector · combinedDirection                        ≈  blended    (sai số chia decimal < 1e-9)
blended − userPenalty − dirPenalty − vibePenalty         == rawScore
round(clamp(rawScore, −1, 1), 3)                         == score
```

`displayPercent` = `(clamp(score) + 1) / 2 × 100`, BE tính sẵn để không lệch cách làm tròn với FE.

### Nghề nghiệp bẻ `r` *(v3.2 §11 — P5)*

```
r'[e] = clamp(r[e] + delta[e] · OCCUPATION_SHARE, −1, 1)
r'[e] = min(r'[e], −0.1)   khi hành e KHẮC bản mệnh
```

Khi nghề có tác động, breakdown trả thêm:

```json
"occupation": {
  "code": "IT", "nameVi": "CNTT / Lập trình",
  "share": 0.30, "shareCode": "OCCUPATION_SHARE",
  "reasonVi": "Nghề CNTT / Lập trình nâng Thuy và hạ Hoa. Nghề nghiệp chỉ đổi mức ƯA THÍCH, không đổi bản mệnh: hành đang khắc mệnh vẫn ở lại phía âm."
}
```

| Field | Ghi chú |
|---|---|
| `vectors.occupationShift` | `r' − r`, **đo SAU khi chặn**. Vẽ lớp radar nghề nghiệp từ đây |
| `vectors.baseRuleScore` | `r` gốc — để FE so được "trước/sau nghề nghiệp" |
| `occupation.share` | `OCCUPATION_SHARE` đang áp |

⚠️ `occupationShift` **không bằng** `delta × share`. Ở hành khắc bản mệnh nó nhỏ hơn hẳn vì bị chặn —
và đó chính là con số phải hiện: một lớp radar nói *"nghề của bạn nâng Kim"* trong khi Kim vẫn khắc
mệnh là nói dối bằng đồ hoạ.

**`occupation = null` khi** user chưa khai nghề · nghề chưa được chuyên gia nhập delta ·
`OCCUPATION_SHARE = 0` · delta chỉ trỏ vào những hành đang khắc mệnh nên bị chặn sạch. Cả bốn đều
nghĩa là *nghề nghiệp không đổi gì*, nên BE trả `null` thay vì một khối có `shift` toàn 0.

⚠️ **Luồng `Carry` không chịu tác động** — nhánh dụng thần không dựng `r` nên không có chỗ để bẻ.

Vì có đủ `ĝ`, `r` và `personalWeight.value`, **FE tự dựng lại `d` và `priorityVector` ở mọi mức `Wp`** —
slider mô phỏng trọng số cá nhân không cần gọi lại API.

⚠️ `breakdown` **không** được gửi cho LLM — model đọc số thô sẽ bịa lại phép tính. Tool AI vẫn chỉ nhận `matchFacts` / `cautionFacts`.

> **`GET /api/recommendations/fit/personal`** *(v3.2, mới)* — bản `fit` cho vật mang theo người. Xem mục riêng bên dưới.

---

## POST `/api/recommendations`

**Request body** (`GenerateRecommendationRequest`)
```json
{ "workspaceProfileId": "guid", "topN": 8, "aspiration": "Wealth" }
```
| Field | Ghi chú |
|-------|---------|
| `workspaceProfileId` | **Bắt buộc.** Workspace đã lưu (xem [Workspace Profiles](./16-workspace-profiles.md)) |
| `topN` | Mặc định 8, kẹp 1..20 |
| **`aspiration`** | `Wealth` \| `Career` \| `Health` \| `Relationship` \| `Study`. **Lọc ứng viên theo thẻ đã duyệt TRƯỚC khi chấm**, và chọn hướng đặt theo cung Bát Trạch tương ứng. Bỏ trống = không lọc. **Không lưu trên user** — là tham số của phiên |

Trả `422` khi không còn ứng viên nào sau lọc.

## POST `/api/recommendations/personal`

Gợi ý vật phẩm **mang theo người** — không cần workspace.

**Request body** (`GeneratePersonalRecommendationRequest`)
```json
{ "topN": 8, "aspiration": "Health" }
```

| Điều kiện | Kết quả |
|---|---|
| User **chưa có** `dateOfBirth` | `422` kèm hướng dẫn bổ sung ngày sinh |
| Có `dateOfBirth`, **chưa có** `birthTime` | Chấm theo **Nạp Âm**, `personalTarget.note` nhắc bổ sung giờ sinh |

> Phiên cá nhân **không gọi AI microservice** diễn giải → `status = "Scored"`, `summary = null`. LLM chat tự diễn giải từ `matchFacts` / `cautionFacts`.

## GET `/api/recommendations/{id}`

Lấy lại phiên đã lưu, **cả hai loại** (`gap` = null khi đọc lại phiên cũ hoặc phiên `PersonalCarry`).

**Response `data`** = `RecommendationResponse`:
```json
{
  "id": "guid",
  "kind": "Workspace",
  "customerElement": "Hoa", "kuaNumber": 3, "kuaGroup": "East",
  "personalWeight": 1.0,
  "status": "Completed", "summary": "...",
  "gap": {
    "elements": [
      { "element": "Thuy", "ideal": 0.30, "current": 0.00, "gap": 0.30 },
      { "element": "Kim",  "ideal": 0.10, "current": 0.36, "gap": -0.26 }
    ]
  },
  "personalTarget": null,
  "note": null,
  "items": [{
    "productId": "guid", "productName": "...", "price": 120000, "imageUrl": "https://...",
    "score": 0.92, "rank": 1,
    "matchFacts": ["Bù năng lượng hành Thuy đang thiếu của phòng."],
    "cautionFacts": [],
    "placementHint": "Hãy đặt vật phẩm này ở hướng Bắc của phòng để kích hoạt năng lượng tốt nhất.",
    "explanation": "..."
  }]
}
```

| Field | Ghi chú |
|---|---|
| **`kind`** | `Workspace` \| `PersonalCarry` — FE dựa vào đây để render đúng loại phiên |
| `status` | `Scored` \| `Completed` \| `Failed`. Phiên `PersonalCarry` dừng ở `Scored` |
| `gap` | Chỉ có ở phiên `Workspace` |
| **`personalTarget`** | Chỉ có ở phiên `PersonalCarry` — xem dưới |
| **`note`** | Non-null khi engine phải **BỎ bộ lọc `aspiration`** vì chưa sản phẩm nào được duyệt thẻ đó. Danh sách vẫn có kết quả (không trả rỗng), nhưng AI/FE **phải nói lại** cho người dùng |
| `placementHint` | Phiên `PersonalCarry` và sản phẩm `Living` trả câu gợi ý **không kèm hướng la bàn** |
| ⚠️ `personalWeight` | **LEGACY engine v2.** Giữ trong response REST để không phá FE, nhưng **đã bị bỏ khỏi payload gửi LLM** (model đọc được sẽ diễn giải sai) |
| `customerElement/kuaNumber/kuaGroup` | Hiển thị hồ sơ; điểm số **không dùng Kua** |

**`personalTarget`** (chỉ phiên `PersonalCarry`):
```json
{
  "source": "TuTru",
  "elements": ["Thủy", "Kim"],
  "note": "Dụng thần theo Tứ Trụ (Thân nhược, nhật chủ Giáp hành Mộc)."
}
```

---

## GET `/api/recommendations/fit?productId={guid}&workspaceProfileId={guid}`

Độ phù hợp phong thủy của **đúng một** sản phẩm với **một** workspace — dùng cho trang chi tiết sản phẩm. Khác `POST /api/recommendations` (chấm `topN` rồi **loại bỏ** sản phẩm không đạt): endpoint này **KHÔNG bao giờ loại** — luôn trả điểm + lý do, kể cả khi xung mệnh, lệch vibe, hay sản phẩm thuộc placement không dành cho phòng (chỉ phản ánh vào `score`/`cautionFacts`). Không lưu phiên, không gọi AI.

| Query param | Ghi chú |
|---|---|
| `productId` | Sản phẩm phải active + đã gắn thuộc tính phong thủy, không thì `404` |
| `workspaceProfileId` | Workspace phải thuộc user hiện tại, không thì `404` |

**Response `data`** = `ProductFitResponse`:
```json
{
  "productId": "guid",
  "workspaceProfileId": "guid",
  "score": 0.62,
  "matchFacts": ["Bù năng lượng hành Thuy đang thiếu của phòng."],
  "cautionFacts": [
    "Hành Hoa khắc bản mệnh Kim — trừ điểm (không gian riêng tư).",
    "Đây là vật phẩm mang theo người — điểm dưới đây chấm theo phòng, chỉ mang tính tham khảo."
  ],
  "placementHint": "Hãy đặt vật phẩm này ở hướng Bắc của phòng để kích hoạt năng lượng tốt nhất.",
  "gap": [
    { "element": "Thuy", "ideal": 0.20, "adjustedIdeal": 0.30, "current": 0.00, "gap": 0.30 },
    { "element": "Kim",  "ideal": 0.15, "adjustedIdeal": 0.10, "current": 0.36, "gap": -0.26 }
  ],
  "productVector": [
    { "element": "Thuy", "value": 0.70 },
    { "element": "Moc", "value": 0.30 }
  ],
  "breakdown": { /* xem "Giải thích điểm" ở trên */ },
  "contributions": [
    { "source": "Interior", "label": "Nền phòng theo loại", "sharePercent": 60.00, "elements": [ … ] },
    { "source": "Tag", "label": "Bàn gỗ", "sharePercent": 20.00, "inputKind": "Material", "inputCode": "wood", "elements": [ … ] }
  ],
  "evidenceCount": 2,
  "confidence": 0.400
}
```
| Field | Ghi chú |
|-------|---------|
| `score` | ∈ `[-1, 1]` — âm nghĩa là xung khắc/lệch nhu cầu phòng, không phải sản phẩm bị lỗi |
| `cautionFacts` | Luôn xuất hiện thay vì loại bỏ — gồm cả caution về `placement` khi vật phẩm không dành cho phòng |
| `productVector` | Vector ngũ hành của **sản phẩm** (Σ=1) — đặt cạnh `gap` để FE vẽ "sản phẩm cấp gì" vs "phòng đang cần gì" |
| `gap` | 5 hành, cùng shape với [element-analysis](./16-workspace-profiles.md) |
| `breakdown` | *(v3.2)* Mọi số hạng đã tạo ra `score` + các vector để vẽ radar |
| `contributions` | Nguồn nào tạo ra `current` của phòng và chiếm bao nhiêu % — **cùng dữ liệu tooltip của [element-analysis](./16-workspace-profiles.md)**, mang sang đây để trả lời *"vì sao phòng được cho là thiếu hành đó"* chứ không chỉ *"phòng thiếu hành đó"*. KHÔNG tính sản phẩm đang xem (ảnh hưởng của nó nằm ở `previewCurrent`) |
| `evidenceCount` | Số bằng chứng THẬT (tag user khai + sản phẩm đã đặt). `0` = hiện trạng hoàn toàn suy ra từ loại phòng |
| `confidence` | `0..1` — tỉ lệ `current` đến từ dữ liệu user khai thay vì nền phòng. Thấp thì FE nên mời user khai thêm tag thay vì để họ tin vào một con số chưa có bằng chứng |

---

## GET `/api/recommendations/fit/personal?productId={guid}` *(v3.2)*

Độ phù hợp của **đúng một** sản phẩm với **bản mệnh** người dùng — dùng cho trang chi tiết vật phẩm
mang theo người (`Carry`).

**Vì sao không dùng `GET /fit`:** endpoint kia bắt buộc `workspaceProfileId` và luôn chấm theo gap của
một phòng. Vật đeo trên người không thuộc phòng nào, nên chấm nó theo phòng là sai bản chất — đó cũng
là lý do hai luồng gợi ý vốn đã tách theo `ProductPlacement`.

| Query param | Ghi chú |
|---|---|
| `productId` | Sản phẩm phải active + đã gắn thuộc tính phong thủy, không thì `404` |

- Mục tiêu = **dụng thần** (Tứ Trụ, fallback Nạp Âm) — `PersonalTargetBuilder`.
- `Wp` **không** áp: mục tiêu đã 100% cá nhân, trộn thêm là tính hai lần.
- Mẫu số **giữ `|target|₁`**, không chia đôi như luồng phòng — `target` Σ=1 và không âm nên không có
  "hai nửa" để chia (§8.1).
- **Không bao giờ loại** (hợp đồng Fit): khắc bản mệnh chỉ vào `score` + `cautionFacts`.
- Thiếu `dateOfBirth` → **`422`** kèm hướng dẫn, **không chấm bừa**: ở đây không còn gap của phòng để
  dựa vào, thiếu căn cứ cá nhân là điểm mất hết ý nghĩa.
- Sản phẩm không phải `Carry` vẫn chấm được, nhưng có caution nói rõ đang chấm theo bản mệnh.

**Response `data`** = `PersonalFitResponse`:
```jsonc
{
  "productId": "guid",
  "score": 0.80,
  "matchFacts": ["Mang hành Moc — đúng hành bản mệnh bạn cần được bồi."],
  "cautionFacts": [],
  "placementHint": "Vật phẩm mang theo người — tác dụng đi theo bản mệnh của bạn, không phụ thuộc hướng đặt trong phòng.",
  "breakdown": {
    "target": "PersonalNeed",
    "components": [
      { "code": "PERSONAL_NEED_SCORE", "labelVi": "Hợp dụng thần của bạn", "value": 0.800, "weight": 1.000, "contribution": 0.800, "reasonVi": "…" }
    ],
    "personalWeight": null,
    "vectors": { "personalNeed": [ … ], "normalizedGap": [ … ], "ruleScore": null, "combinedDirection": [ … ] }
  },
  "personalNeedVector": [ { "element": "Moc", "value": 0.60 }, { "element": "Thuy", "value": 0.40 } ],
  "productVector": [ { "element": "Moc", "value": 1.00 } ],
  "destinyElement": "Moc",
  "destinyLabelVi": "Moc — Đại Lâm Mộc (1988)"
}
```

⚠️ **Waterfall của luồng này chỉ có MỘT thành phần** — FE phải dùng component riêng, không tái sử dụng
panel của luồng phòng (ở đó có hai thành phần + trọng số `Wp`). Radar cũng khác: chỉ `personalNeedVector`
+ `productVector`, **không** có lớp "Mức lý tưởng"/"Hiện tại" vì không có phòng.

---

## Tool AI tương ứng

| Tool | Endpoint phía sau | `topN` |
|---|---|---|
| `recommend_products` | `POST /api/recommendations` | kẹp **3..5**, mặc định 4 |
| `recommend_personal_items` | `POST /api/recommendations/personal` | kẹp **3..5**, mặc định 4 |

`search_products` *(v3.2, Q10)* nhận thêm param **`placement`** (`Desk` | `Living` | `Carry` |
`Consumable`), **mặc định `Desk`** khi model không truyền. Không có mặc định thì "vòng tay Kim" và
"đèn muối" rơi vào cùng một rổ, trong khi hai thứ đó đi **hai luồng chấm điểm khác nhau**; `Desk` là
nhóm đông nhất nên ít gây bất ngờ nhất với câu hỏi chung chung.

Cả hai tool (và `search_products`) đều nhận param `aspiration`. Prompt hướng dẫn model **hỏi lại** khi mục tiêu người dùng có vẻ liên quan nhưng chưa rõ — dạng note nhẹ, không dùng "MUST".

Tool chat kẹp `topN` hẹp hơn REST (8/20 cho FE grid): trong hội thoại danh sách dài làm câu trả lời loãng và model hay bịa thêm sản phẩm. `recommend_products` cho phép bỏ trống `workspaceProfileId` → dùng workspace `IsDefault`.

---

[← Workspace Types](./17-workspace-types.md) · [Tiếp: Reviews →](./19-reviews.md)
