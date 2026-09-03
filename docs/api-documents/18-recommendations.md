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

---

## Cách engine chấm điểm

Mệnh của **phòng**, của **người** và của **sản phẩm** đều quy về **vector 5 hành** (`element_kim/moc/thuy/hoa/tho`).

### Luồng workspace

1. **Vector phòng** = `Ideal` (theo loại phòng) → **ApplyIntent** (bẻ theo `WorkPurpose`, bảng `work_purpose_element_modifiers`) → so với **Current** (màu/vật liệu user khai, hoặc Interior mặc định).
2. **Gap** = `adjustedIdeal − current` → phòng **thiếu** (gap > 0) hay **thừa** (gap < 0) hành nào.
3. **Điểm khớp** = `gap · productVector / |gap|₁`. Bơm vào hành thiếu → dương; bơm vào hành đã thừa → âm.

### Luồng cá nhân (`Carry`)

Vector mục tiêu là **thứ NGƯỜI đang cần**, không phải phòng:

| Dữ liệu có | Nguồn | Ghi trong `personalTarget.source` |
|---|---|---|
| Ngày sinh **+ giờ sinh** | **Dụng thần Tứ Trụ** (`CARRY_PRIMARY_SHARE` / `CARRY_SECONDARY_SHARE`) | `TuTru` |
| Chỉ ngày sinh | **Nạp Âm** (bản mệnh 0.60 / hành sinh 0.30 / hành được sinh 0.10) | `NapAm` |
| Không có ngày sinh | — | ❌ trả `422` kèm hướng dẫn, **không chấm bừa** |

Điểm = `target · productVector / |target|₁` — **cùng thang `[-1, 1]`** với luồng workspace.

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
| Hành trội khắc bản mệnh (scope `Shared/Public`) | `USER_CONFLICT_PENALTY` |
| Mọi hướng hợp vật phẩm đều bị chắn (cửa / WC / góc tối) | `DIRECTION_PENALTY` |

- **`VIBE_FILTER_HARD`** ≥ 0.5 → lệch vibe bị **loại thẳng** (hành vi v3). < 0.5 → chuyển sang **trừ điểm mềm**.
- **`MIN_SCORE_THRESHOLD`** → cắt theo **điểm tổng** thay vì theo từng thuộc tính. Mặc định `−1.0` = không cắt.

Điểm cuối kẹp `[-1, 1]`, sắp giảm dần, lấy `topN`.

> Mệnh nhập tay `workspaceProfile.fengShuiElement` là **legacy**, engine **không dùng**.

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
  ]
}
```
| Field | Ghi chú |
|-------|---------|
| `score` | ∈ `[-1, 1]` — âm nghĩa là xung khắc/lệch nhu cầu phòng, không phải sản phẩm bị lỗi |
| `cautionFacts` | Luôn xuất hiện thay vì loại bỏ — gồm cả caution về `placement` khi vật phẩm không dành cho phòng |
| `productVector` | Vector ngũ hành của **sản phẩm** (Σ=1) — đặt cạnh `gap` để FE vẽ "sản phẩm cấp gì" vs "phòng đang cần gì" |
| `gap` | 5 hành, cùng shape với [element-analysis](./16-workspace-profiles.md) |

---

## Tool AI tương ứng

| Tool | Endpoint phía sau | `topN` |
|---|---|---|
| `recommend_products` | `POST /api/recommendations` | kẹp **3..5**, mặc định 4 |
| `recommend_personal_items` | `POST /api/recommendations/personal` | kẹp **3..5**, mặc định 4 |

Cả hai tool (và `search_products`) đều nhận param `aspiration`. Prompt hướng dẫn model **hỏi lại** khi mục tiêu người dùng có vẻ liên quan nhưng chưa rõ — dạng note nhẹ, không dùng "MUST".

Tool chat kẹp `topN` hẹp hơn REST (8/20 cho FE grid): trong hội thoại danh sách dài làm câu trả lời loãng và model hay bịa thêm sản phẩm. `recommend_products` cho phép bỏ trống `workspaceProfileId` → dùng workspace `IsDefault`.

---

[← Workspace Types](./17-workspace-types.md) · [Tiếp: Reviews →](./19-reviews.md)
