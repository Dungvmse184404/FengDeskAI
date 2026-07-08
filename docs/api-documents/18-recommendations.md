# 18 — Recommendations

[← Mục lục](./README.md)

Controller: `RecommendationsController` · Route gốc: `/api/recommendations` · **Toàn bộ `[Authorize]`** — user chỉ truy cập gợi ý của chính mình.

Gợi ý sản phẩm phong thủy cho một workspace của user: **engine .NET v3 chấm điểm**, **AI diễn giải**.

> ⚙️ Cấu hình engine (tham số, map ngũ hành, modifier…) do admin quản qua [Scoring Config](./25-scoring-config.md).

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| POST | `/api/recommendations` | Authenticated | Tạo phiên gợi ý cho workspace |
| GET | `/api/recommendations/{id}` | Authenticated | Lấy lại phiên gợi ý đã lưu |
| GET | `/api/recommendations/fit` | Authenticated | Độ phù hợp của 1 sản phẩm × 1 workspace (trang chi tiết sản phẩm) |

---

## Cách engine v3 chấm điểm (tóm tắt)

Mệnh của phòng **và** của sản phẩm đều được biểu diễn bằng **vector 5 hành** (`element_kim/moc/thuy/hoa/tho`).

1. **Vector phòng** = `Ideal` (lý tưởng theo loại phòng) → **ApplyIntent** (bẻ theo mục đích làm việc, `work_purpose_element_modifiers`) → so với **Current** (hiện trạng: màu/vật liệu user khai, hoặc Interior mặc định).
2. **Gap** = `adjustedIdeal − current` → phòng đang **thiếu** (gap > 0) hay **thừa** (gap < 0) hành nào.
3. **Điểm khớp** mỗi sản phẩm = mức nó bù đúng Gap (`gap · productVector / |gap|`). Sản phẩm bơm vào hành thiếu → điểm dương; bơm vào hành đã thừa → điểm âm.
4. **Lọc & phạt:**
   - *Intent filter (hard):* sản phẩm phải có vibe khớp mục đích, nếu không bị loại.
   - *User constraint:* hành trội sản phẩm khắc bản mệnh user → **loại** nếu phòng `Private`, **trừ điểm** (`USER_CONFLICT_PENALTY`) nếu `Shared/Public`.
   - *Directional Validation:* nếu mọi hướng hợp vật phẩm đều bị chắn (cửa/WC/góc tối) → trừ `DIRECTION_PENALTY`; luôn kèm **gợi ý hướng đặt** (`placementHint`).
5. Điểm cuối kẹp `[-1, 1]`, sắp giảm dần, lấy `topN`.

> Mệnh nhập tay `workspaceProfile.fengShuiElement` là **legacy**, engine v3 **không dùng**.

---

## POST `/api/recommendations`
**Request body** (`GenerateRecommendationRequest`)
```json
{ "workspaceProfileId": "guid", "topN": 8 }
```
| Field | Ghi chú |
|-------|---------|
| `workspaceProfileId` | Workspace đã lưu (xem [Workspace Profiles](./16-workspace-profiles.md)) |
| `topN` | Số sản phẩm gợi ý; mặc định 8, kẹp 1..20 |

## GET `/api/recommendations/{id}`
Lấy lại phiên gợi ý đã lưu (`Gap` = null khi đọc lại phiên cũ).

**Response `data`** = `RecommendationResponse`:
```json
{
  "id": "guid",
  "customerElement": "Hoa", "kuaNumber": 3, "kuaGroup": "East",
  "personalWeight": 1.0,
  "status": "Completed", "summary": "...",
  "gap": {
    "elements": [
      { "element": "Thuy", "ideal": 0.30, "current": 0.00, "gap": 0.30 },
      { "element": "Kim",  "ideal": 0.10, "current": 0.36, "gap": -0.26 }
    ]
  },
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
> `status`: `Scored | Completed | Failed`.
> Field mới v3: **`gap`** (phòng thiếu/thừa hành gì) và **`placementHint`** (gợi ý hướng đặt) trong mỗi item.
> `customerElement/kuaNumber/kuaGroup` vẫn trả để hiển thị hồ sơ; riêng điểm v3 chỉ dùng **mệnh** (không dùng Kua).

---

## GET `/api/recommendations/fit?productId={guid}&workspaceProfileId={guid}`

Độ phù hợp phong thủy của **đúng một** sản phẩm với **một** workspace — dùng cho trang chi tiết sản phẩm. Khác `POST /api/recommendations` (chấm `topN` rồi **loại bỏ** sản phẩm không đạt): endpoint này **KHÔNG loại** — luôn trả điểm + lý do, kể cả khi xung mệnh hay lệch vibe (chỉ phản ánh vào `score`/`cautionFacts`, không ẩn kết quả). Không lưu phiên (không tạo `Recommendation`), không gọi AI diễn giải.

| Query param | Ghi chú |
|---|---|
| `productId` | Sản phẩm cần xem — phải active + đã gắn thuộc tính phong thủy, không thì `404` |
| `workspaceProfileId` | Workspace phải thuộc user hiện tại, không thì `404` |

**Response `data`** = `ProductFitResponse`:
```json
{
  "productId": "guid",
  "workspaceProfileId": "guid",
  "score": 0.62,
  "matchFacts": ["Bù năng lượng hành Thuy đang thiếu của phòng."],
  "cautionFacts": ["Hành Hoa khắc bản mệnh Kim — trừ điểm (không gian riêng tư)."],
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
| `cautionFacts` | Luôn xuất hiện thay vì loại bỏ — vd xung bản mệnh dù ở phòng `Private` (khác hành vi lọc của `POST /api/recommendations`) |
| `productVector` | Vector ngũ hành của **sản phẩm** (Σ=1) — đặt cạnh `gap` để FE vẽ "sản phẩm cấp gì" vs "phòng đang cần gì" |
| `gap` | 5 hành, cùng shape với [element-analysis](./16-workspace-profiles.md) |

---

[← Workspace Types](./17-workspace-types.md) · [Tiếp: Reviews →](./19-reviews.md)
