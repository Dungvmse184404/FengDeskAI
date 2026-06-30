# 18 — Recommendations

[← Mục lục](./README.md)

Controller: `RecommendationsController` · Route gốc: `/api/recommendations` · **Toàn bộ `[Authorize]`** — user chỉ truy cập gợi ý của chính mình.

Gợi ý sản phẩm phong thủy cho một workspace của user: **engine .NET chấm điểm**, **AI diễn giải**.

---

## 📋 Bảng endpoint

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| POST | `/api/recommendations` | Authenticated | Tạo phiên gợi ý cho workspace |
| GET | `/api/recommendations/{id}` | Authenticated | Lấy lại phiên gợi ý đã lưu |

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
Lấy lại phiên gợi ý đã lưu.

**Response `data`** = `RecommendationResponse`:
```json
{
  "id": "guid", "customerElement": "Hoa", "kuaNumber": 3, "kuaGroup": "East",
  "personalWeight": 1.0, "status": "Completed", "summary": "...",
  "items": [{
    "productId": "guid", "productName": "...", "price": 120000, "imageUrl": "https://...",
    "score": 0.92, "rank": 1,
    "matchFacts": ["Hành Mộc tương sinh mệnh Hỏa"],
    "cautionFacts": [],
    "explanation": "..."
  }]
}
```
> `status`: `Scored | Completed | Failed`.

---

[← Workspace Types](./17-workspace-types.md) · [Tiếp: Reviews →](./19-reviews.md)
