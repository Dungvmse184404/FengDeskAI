# 16 — Workspace Profiles

[← Mục lục](./README.md)

Controller: **`WorkspaceController`** · Route gốc: `/api/workspace` · **Toàn bộ `[Authorize]`** — user chỉ thao tác trên profile của chính mình.

Hồ sơ không gian làm việc — mô tả bàn/phòng/hướng/phong cách... dùng làm input cho engine gợi ý phong thủy. Profile mặc định được dùng khi user không chỉ định.

Ngoài CRUD, controller này còn ôm **luồng AI intake** (mô tả tự do / ảnh / giọng nói → tự điền form) và **luồng đặt sản phẩm đã mua vào phòng**.

> **Lưu ý field engine:**
> - `fengShuiElement` (mệnh nhập tay) là **legacy**, engine **không dùng** — mệnh phòng tính bằng vector ngũ hành từ loại phòng + màu/vật liệu.
> - `inputs` (màu/vật liệu/hình khối thực tế của phòng → dựng `currentVector`) **ĐÃ lộ ra** ở cả request lẫn response. Quy ước null-vs-rỗng: `inputs = null` → **giữ nguyên**; `inputs = []` → **xoá hết**.
> - ⚠️ `entrance_direction`, `toilet_direction`, `dark_directions` (hướng bị chắn → Directional Validation) **vẫn CHƯA có ô nhập** ở request/UI — chỉ tồn tại ở tầng DB/engine, nên `DIRECTION_PENALTY` gần như không bao giờ kích hoạt. Xem `docs/ard/architecture-core/06-doc-debt.md`.

---

## 📋 Bảng endpoint — **18 endpoint**

### CRUD hồ sơ

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/workspace` | Authenticated | Danh sách profile của tôi |
| GET | `/api/workspace/default` | Authenticated | Profile mặc định |
| GET | `/api/workspace/{id}` | Authenticated | Chi tiết profile |
| GET | `/api/workspace/{id}/element-analysis` | Authenticated | Vector ngũ hành phòng (thiếu/thừa hành gì) |
| POST | `/api/workspace` | Authenticated | Tạo profile |
| PUT | `/api/workspace/{id}` | Authenticated | Cập nhật profile |
| PATCH | `/api/workspace/{id}/set-default` | Authenticated | Đặt làm mặc định |
| DELETE | `/api/workspace/{id}` | Authenticated | Xóa profile |

### AI intake (mô tả tự do / ảnh / giọng nói)

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| POST | `/api/workspace/parse-description` | **CustomerOnly** | Bắt đầu job parse — trả `operationId` **ngay**, không chờ LLM |
| GET | `/api/workspace/parse-description/{operationId}` | **CustomerOnly** | Poll kết quả job (fallback khi lỡ mất event realtime; hết hạn → `404`) |
| GET | `/api/workspace/speech-config` | **CustomerOnly** | Cấu hình STT (tắt → FE fallback Web Speech) |
| POST | `/api/workspace/transcriptions` | **CustomerOnly** | Ghi âm → text (multipart, field `file`) |
| POST | `/api/workspace/images` | Authenticated | Upload ảnh phòng để đính kèm vào intake |
| GET | `/api/workspace/element-inputs` | Authenticated | Vocabulary màu/vật liệu/hình khối/vật trang trí |
| POST | `/api/workspace/element-inputs/classify` | **CustomerOnly** | Tag tự đặt tên → hành + weight (AI, có chuẩn hoá deterministic) |

> Job chạy nền qua `WorkspaceIntakeQueue` → `WorkspaceIntakeWorker`; tiến độ phát realtime qua SignalR group `ai-op-{operationId}`.
> Rate-limit policy `workspace-intake` áp cho `parse-description`, `transcriptions`, `element-inputs/classify`.

### GET `/api/workspace/element-inputs` — từ vựng tag hiện trạng
`data` = `ElementInputVocabularyResponse`. Mỗi tag trả **`code` để lưu** và **`labelVi` để hiển thị**
(FE không được hiện `code` kỹ thuật ra UI — chính `labelVi` là dẫn chứng dùng lại trong radar và 3 dòng nhận định).
```json
{
  "colors":     [{ "code": "Brown", "labelVi": "Nâu" }],
  "materials":  [{ "code": "Wood",  "labelVi": "Gỗ" }],
  "shapes":     [{ "code": "Round", "labelVi": "Bo tròn" }],
  "decorItems": [{ "code": "Stove", "labelVi": "Bếp nấu" }]
}
```

### POST `/api/workspace/element-inputs/classify`
`data` = `ClassifyElementInputResponse` — `labelVi` chính là **chữ user vừa gõ**, đã lưu vào `element_input_map`
để lần sau hiển thị và diễn giải đúng tên đó.
```json
{ "code": "LacquerPainting", "labelVi": "tranh sơn mài",
  "elements": [{ "element": "Hoa", "weight": 0.5 }, { "element": "Moc", "weight": 0.5 }] }
```

### Đặt sản phẩm đã mua vào phòng

| Method | Path | Quyền | Mô tả |
|--------|------|-------|-------|
| GET | `/api/workspace/placements/purchasable` | Authenticated | Sản phẩm đã mua đủ điều kiện đặt phòng |
| PUT | `/api/workspace/{id}/placements` | Authenticated | Đặt / chuyển sản phẩm sang phòng này (idempotent) |
| DELETE | `/api/workspace/{id}/placements/{orderItemId}` | Authenticated | Gỡ sản phẩm khỏi phòng |

> `element-analysis` tính **lúc đọc**, không lưu DB: `current`/`gap` chỉ tính hàng **đã giao**; `previewCurrent`/`previewGap` tính cả hàng **đang giao**.

---

## GET `/api/workspace` · `/default` · `/{id}`
`data` = `WorkspaceProfileResponse`:
```json
{
  "id": "guid", "userId": "guid", "name": "Bàn làm việc nhà",
  "workspaceTypeId": "guid", "locationType": "Home", "styleCode": "Minimal",
  "lighting": "Natural", "deskType": "Sitting",
  "deskOrientation": "East", "roomFacingDirection": "South",
  "workPurpose": "Office", "fengShuiElement": "Moc", "deskArea": 120,
  "isDefault": true,
  "completenessPercent": 75,
  "missingFieldHints": ["Thêm diện tích mặt bàn để lọc vật phẩm vừa kích thước"],
  "inputs": [{ "inputKind": "Material", "inputCode": "Wood" }],
  "createdAt": "...", "updatedAt": "..."
}
```

## GET `/api/workspace/{id}/element-analysis`
Phân tích ngũ hành của một workspace **không cần chạy cả phiên recommendation** — để FE hiển thị "phòng của bạn đang thiếu/thừa hành gì". Dùng đúng công thức vector với engine chấm điểm (`Gap = adjustedIdeal − current`).

`data` = `WorkspaceElementAnalysisResponse`:
```json
{
  "workspaceProfileId": "guid",
  "dominantNeed": "Thuy",
  "compatibilityPercent": 63,
  "evidenceCount": 3,
  "confidence": 0.5,
  "totalVotes": 11,
  "elements": [
    { "element": "Thuy", "ideal": 0.20, "adjustedIdeal": 0.30, "current": 0.04, "gap":  0.26 },
    { "element": "Moc",  "ideal": 0.25, "adjustedIdeal": 0.25, "current": 0.24, "gap":  0.01 },
    { "element": "Hoa",  "ideal": 0.15, "adjustedIdeal": 0.10, "current": 0.36, "gap": -0.26 }
  ],
  "contributions": [
    { "source": "Interior", "label": "Nền phòng theo loại", "sharePercent": 27.27, "votes": 3,
      "elements": [{ "element": "Hoa", "percent": 8.18 }, { "element": "Kim", "percent": 8.18 }] },
    { "source": "Person", "label": "Bạn — mệnh Kim", "sharePercent": 18.18, "votes": 2,
      "elements": [{ "element": "Kim", "percent": 10.91 }, { "element": "Tho", "percent": 5.45 }] },
    { "source": "Tag", "label": "Bếp nấu", "sharePercent": 9.09, "votes": 1, "inputKind": "DecorItem", "inputCode": "Stove",
      "elements": [{ "element": "Hoa", "percent": 16.67 }] },
    { "source": "Product", "label": "Tượng gốm Bát Tràng", "sharePercent": 9.09, "votes": 1, "productId": "guid",
      "elements": [{ "element": "Tho", "percent": 16.67 }] }
  ],
  "personalDirection": {
    "personalWeight": 0.50,
    "personalWeightCode": "PERSONAL_WEIGHT_PRIVATE",
    "scope": "Private",
    "reasonVi": "Phòng riêng tư — 50% điểm đến từ bản mệnh của bạn, phần còn lại từ nhu cầu của phòng.",
    "destinyElement": "Moc",
    "destinyLabelVi": "Moc — Đại Lâm Mộc (1988)",
    "personalVector":    [ { "element": "Kim", "value": 0.600 }, { "element": "Tho", "value": 0.300 }, { "element": "Thuy", "value": 0.100 }, … ],
    "personalTarget":    [ { "element": "Kim", "value": 0.271 }, { "element": "Hoa", "value": 0.274 }, … ],
    "normalizedGap":     [ { "element": "Hoa", "value": 0.815 }, … ],
    "ruleScore":         [ { "element": "Kim", "value": 1.000 }, { "element": "Hoa", "value": -1.000 }, … ],
    "combinedDirection": [ { "element": "Hoa", "value": 0.270 }, … ],
    "priorityVector":    [ { "element": "Hoa", "value": 0.528 }, … ],
    "conflictResolution": null
  },
  "insights": {
    "case": "Imbalanced",
    "lines": [
      { "kind": "trait",  "title": "Đặc tính không gian", "text": "Đối với không gian Kitchen dùng để nấu ăn, hành Hỏa và Thổ trội hơn sẽ thuận lợi hơn — ..." },
      { "kind": "status", "title": "Hiện trạng", "text": "Hiện tại, do có bếp nấu và đỏ, hành Hỏa đang chiếm ưu thế và lấn át hành Thổ ..." },
      { "kind": "action", "title": "Gợi ý cân bằng", "text": "Đặt thêm gốm sứ, đá tự nhiên, tông nâu vàng (Thổ) để hút bớt tính Hỏa đang dư thừa." }
    ]
  }
}
```
| Field | Kiểu | Ghi chú |
|-------|------|---------|
| `workspaceProfileId` | guid | Profile được phân tích (phải thuộc user, không có → `404`) |
| `dominantNeed` | enum `FengShuiElement` | Hành có `gap` dương lớn nhất (thiếu nhiều nhất) |
| `compatibilityPercent` | int | `100 × (1 − Σ\|gap\| / 2)` |
| `evidenceCount` | int | Số bằng chứng thật (tag + sản phẩm đã giao). **0 = mọi con số suy ra từ nền loại phòng** |
| `confidence` | decimal | `0..1` — tỉ lệ `current` đến từ dữ liệu user khai thay vì nền phòng |
| `totalVotes` | decimal | Tổng phiếu mọi nguồn — mẫu số của mọi `sharePercent`; FE mô phỏng lại được "chủ nhân nặng N phiếu thì phòng ra sao" mà không gọi lại API |
| `contributions[].votes` | decimal | Số **phiếu** của nguồn — đơn vị gốc của mô hình. FE hiện "3 phiếu" thay vì "27%": phiếu ổn định, còn % đổi mỗi lần khai thêm tag |
| `personalDirection` | object \| null | *(v3.2)* Trục cá nhân của căn phòng — xem bảng riêng bên dưới |
| `elements[]` | array | 5 hành, sắp **giảm dần theo `gap`** (thiếu nhất → thừa nhất) |

### `personalDirection` — lớp "Ưu tiên của bạn" trên radar *(v3.2 §10.3)*

Trả lời: ***"sau khi tính bản mệnh của bạn, phòng này nên ưu tiên bù hành nào"***.

**Không phụ thuộc sản phẩm** — `ĝ` đến từ gap của phòng, `r` đến từ bản mệnh — nên nó thuộc về màn hình
phân tích phòng, không phải màn hình một sản phẩm. BE dùng đúng `ElementDirection` mà engine chấm điểm
dùng, nên đa giác ở đây và đa giác trong `breakdown` của một sản phẩm không thể lệch nhau.

| Field | Ghi chú |
|---|---|
| `personalWeight` | `Wp` đang áp cho phòng này |
| `personalWeightCode` | Dòng `scoring_params` đã quyết định con số đó |
| `reasonVi` | Vì sao là con số đó — dùng cho tooltip của chip |
| `destinyLabelVi` | vd `"Moc — Đại Lâm Mộc (1988)"`, năm là năm **ÂM lịch** |
| `personalVector` | Vector bản mệnh Σ=1: mệnh 60% · hành **sinh ra** mệnh 30% · hành mệnh **sinh ra** 10% (`SELF/SUPPORT/CHILD_SHARE`) |
| `normalizedGap` | `ĝ = gap / (\|gap\|₁/2)`, mỗi trục ∈ `[−1,+1]` |
| `ruleScore` | `r[e] = ruleScore(bản mệnh, e)`, **CÓ DẤU**, từ `feng_shui_rules` |
| `combinedDirection` | `d = (1−Wp)·ĝ + Wp·r` — **thứ chấm điểm sản phẩm**, KHÔNG phải thứ vẽ radar |
| `priorityVector` | `normalize(max(d, 0))`, Σ=1 — "đang ưu tiên BÙ hành nào". **Không vẽ chồng** lên radar phòng: nó chỉ trải trên các trục còn dương nên luôn nhọn hơn hai lớp kia, so trực tiếp là so sai. Để dành cho radar phụ |
| `conflictResolution` | §13 — phòng thiếu đúng hành khắc bản mệnh; `null` khi không có xung khắc |

Vì có đủ `ĝ`, `r` và `personalWeight`, **FE tự dựng lại `d` ở mọi mức `Wp`** mà không gọi lại API.

> ⚠️ Không còn `personalTarget`. Lớp vàng trên radar phòng nay là **phần đóng góp thật của chủ
> nhân**, đọc từ `contributions[]` (`source: "Person"`), nhãn ghi **số phiếu**. Xem mục dưới.

#### Chủ nhân phòng là một NGUỒN trong `current` *(v3.2 §12)*

`current` dựng theo mô hình **phiếu**:

```
current = normalize( Σᵢ vᵢ · wᵢ )
```

| Nguồn | phiếu |
|---|---|
| Nền phòng | `INTERIOR_PRIOR_VOTES` = 3 |
| Mỗi tag user khai | Σ weight của code, thường 1 |
| **Chủ nhân phòng** | `PERSON_PRESENCE_VOTES_*` = **3 / 2 / 0** theo scope |
| Mỗi sản phẩm đã đặt | `voteWeight`, mặc định 1 |

Chủ nhân xuất hiện thành **một dòng trong `contributions[]`** với `source: "Person"` — nên tooltip
"Đến từ" vẫn cộng ra 100% và FE vẽ được "phần của bạn" ngay từ dữ liệu đó.

**Dùng phiếu chứ không phải tỉ trọng %:** bản mệnh là *prior* nên nó phải **loãng dần** khi user khai
thêm tag, đúng cách nền phòng cư xử. Tỉ trọng cố định thì khai 20 tag thật mà bản mệnh vẫn giữ nguyên
phần của nó — ngược triết lý "bằng chứng lấn át suy đoán".

**`confidence` GIẢM khi thêm chủ nhân**: chủ nhân vào mẫu số nhưng không vào tử số (nó không phải bằng
chứng user khai về căn phòng). Bàn học 70.0% → 53.8%.

⚠️ Chủ nhân vào `current` dùng cho **CẢ** radar **lẫn** gap chấm điểm — nếu chỉ vào radar thì hình và
điểm nói hai chuyện khác nhau về cùng một căn phòng.

### Lớp vàng "Phần của bạn" vẽ thế nào

Đọc thẳng từ dòng `source: "Person"` trong `contributions[]` — không tính lại gì:

```
phần của bạn[e] = contributions[Person].elements[e].percent / 100
```

Nó luôn **nằm trong** lớp `Hiện tại`, vì nó đúng là một phần của `current`. Nhãn là **số phiếu**
(`contributions[].votes`), không phải %.

Slider trên FE mô phỏng "nếu tôi nặng N phiếu thì sao". Vì chủ nhân là một **số hạng trong tổng**
chứ không phải một lớp vẽ đè, mô phỏng phải dựng lại **cả `current`**, không chỉ lớp vàng:

```
khác[e] = current[e]·totalVotes − votes·personalVector[e]      // Σ = totalVotes − votes
mới[e]  = (khác[e] + N·personalVector[e]) / (totalVotes − votes + N)
```

`N = votes` trả về đúng `current` ban đầu. `personalVector` ở `personalDirection` chính là vector BE dùng
cho nguồn `Person` (cùng `BuildPersonalVector`), nên tách → ghép lại không lệch số.

⚠️ Mô phỏng chỉ để xem thử, không đổi cấu hình. Những thứ đọc `gap` (dấu trục, chip Thừa/Thiếu)
phải đọc cùng một `current` với đa giác xanh — không thì chip nói về phòng thật còn hình nói về phòng
giả định. Ba dòng nhận định là văn bản do BE sinh nên vẫn ở mức thật; FE nói rõ điều đó khi đang xem thử.

**Chủ nhân nặng dần đổi căn phòng tới đâu** — số thật của "Nhà bếp chính" (`Shared`, 6 tag, chủ nhân mệnh Kim):

| Phiếu chủ nhân | Kim | Mộc | Thủy | Hỏa | Thổ | Phần của bạn |
|---:|---:|---:|---:|---:|---:|---:|
| 0 | 26.9% | 19.0% | 2.3% | 27.2% | 24.6% | 0% |
| **2** *(seed `Shared`)* | **32.9%** | 15.5% | 3.7% | 22.3% | 25.5% | **18.2%** |
| 8 | 42.5% | 10.1% | 5.9% | 14.4% | 27.1% | 47.1% |

Lớp vàng **to nhỏ theo phiếu nhưng không đổi hình** — đúng, vì hướng của nó luôn là `personalVector`
(60/30/10 quanh bản mệnh), chỉ có độ lớn trong hỗn hợp là đổi. Lớp xanh thì đổi **cả hình**: ở 8 phiếu,
`gap[Mộc]` lật dấu từ −0.5 (thừa) sang +4.9 (thiếu) — chip của Mộc đổi trạng thái theo.

| Kỳ vọng thường gặp | Thực tế |
|---|---|
| Kéo phiếu lên thì lớp **Hiện tại** đứng yên | **Không.** Chủ nhân *nằm trong* `current`, nên đổi phiếu là đổi cả hình xanh — phần của họ to lên **và** mọi nguồn khác loãng đi vì mẫu số lớn hơn |
| Lớp vàng phải đổi hình khi kéo phiếu | **Không.** Hướng của nó là `personalVector`, cố định theo bản mệnh. Chỉ **độ lớn** trong hỗn hợp đổi — to nhỏ chứ không biến dạng là đúng |
| Phòng nhiều tag thì phần của bạn vẫn vậy | **Không.** Phiếu cố định nhưng mẫu số lớn dần → phần của bạn **loãng đi**. Đúng ý đồ: bằng chứng thật lấn át suy đoán |
| Phòng `Public` vẫn có lớp vàng | **Không.** `PERSON_PRESENCE_VOTES_PUBLIC = 0` → không có dòng `Person` nào |

**`personalDirection` = `null` khi** phòng `Public` · user chưa có `dateOfBirth` · `PERSONAL_WEIGHT_*` đang
là 0. Cả ba đều dẫn tới `Wp = 0`, mà khi đó `d ≡ ĝ` nên trục cá nhân không nói thêm được gì.

> ⚠️ Hai tham số **song song, không thay thế nhau**: `PERSON_PRESENCE_VOTES_*` định lượng bản mệnh
> *trong hiện trạng phòng*; `PERSONAL_WEIGHT_*` định lượng bản mệnh *khi chấm một sản phẩm*.

---

| `elements[].ideal` | decimal | Vector lý tưởng theo loại phòng (Σ=1) |
| `elements[].adjustedIdeal` | decimal | Ideal đã bẻ theo mục đích làm việc (Σ=1) |
| `elements[].current` | decimal | Hiện trạng phòng (Σ=1) — xem công thức bên dưới |
| `elements[].gap` | decimal | `adjustedIdeal − current`: **+ thiếu, − thừa** (Σ=0) |
| `contributions[]` | array | Nguồn tạo nên `current`, sắp **giảm dần theo `sharePercent`** |
| `contributions[].source` | enum | `Interior` (nền phòng) · `Tag` (user khai) · `Product` (sản phẩm đã đặt) · `Person` (bản mệnh chủ nhân) |
| `contributions[].label` | string | Nhãn tiếng Việt — tag lấy `label_vi`, sản phẩm lấy tên sản phẩm |
| `contributions[].sharePercent` | decimal | % nguồn này chiếm trong toàn bộ `current` (tổng mọi nguồn = 100) |
| `contributions[].elements[].percent` | decimal | Phần nguồn này góp cho 1 hành. **Σ mọi nguồn trên 1 hành = `current` × 100** |
| `insights.lines[].kind` | enum | `trait` (đặc tính loại phòng) · `status` (hiện trạng + nguyên do) · `action` (đề xuất) |

### Công thức `current` (v3.2)
```
current = normalize( Interior × 3  +  Chủ nhân × (3|2|0)  +  Σ tag (1 phiếu/tag)  +  Σ sản phẩm (× voteWeight) )
```
- **Nền phòng luôn có mặt** như prior 3 phiếu → không bao giờ có hành = 0 dù user khai ít tag.
  Càng nhiều tag thì prior càng mờ đi (3 tag = 50/50 với nền).
- Vector `Interior` được seed sao cho phòng **chỉ chọn loại, chưa khai gì** đạt `compatibilityPercent` ≈ **70%**
  — còn dư địa để tag/sản phẩm cải thiện, thay vì mặc định đã ~90%.
- Loại phòng chưa seed `Interior` → nền = phân bố đều `0.2` (vẫn không có hành = 0).
- **Chủ nhân phòng cũng là một nguồn** — `Private` 3 phiếu, `Shared` 2, `Public` 0. Phòng riêng của bạn
  thì bản mệnh của bạn là hiện trạng thật; phòng họp của cả công ty thì không.
- `confidence` = (phiếu tag + phiếu sản phẩm) / `totalVotes` — **chủ nhân và nền phòng đều là prior**, chỉ
  vào mẫu số. Thêm chủ nhân làm độ tin cậy **giảm**, đúng bản chất: phòng vẫn chưa được khai thêm gì.

> Workspace không gắn `workspaceTypeId` → `ideal` rỗng (toàn 0), `current` dùng nền mặc định — không lỗi.

## POST `/api/workspace`
**Request body** (`CreateWorkspaceProfileRequest`)
```json
{
  "name": "Bàn làm việc nhà",
  "locationType": "Home",
  "workspaceTypeId": "guid",
  "styleCode": "Minimal",
  "lighting": "Natural",
  "deskType": "Sitting",
  "deskOrientation": "East",
  "roomFacingDirection": "South",
  "workPurpose": "Office",
  "fengShuiElement": "Moc",
  "deskArea": 120,
  "isDefault": true
}
```
| Field | Kiểu | Ghi chú |
|-------|------|---------|
| `locationType` | enum `LocationType` | `Home`/`Office`/`Cafe`/`Studio`/`Other` |
| `workspaceTypeId` | guid? | Bỏ trống → coi như riêng tư (weight 1.0) |
| `styleCode` | string | Mã từ [Styles](./05-styles.md) |
| `lighting` | enum `LightingType` | |
| `deskType` | enum `DeskType` | |
| `deskOrientation`, `roomFacingDirection` | enum `CompassDirection` | |
| `workPurpose` | enum `WorkPurpose` | |
| `fengShuiElement` | enum `FengShuiElement` | `Kim/Moc/Thuy/Hoa/Tho` |
| `deskArea` | int | cm² |

## PUT `/api/workspace/{id}`
**Request body** (`UpdateWorkspaceProfileRequest`) — giống create nhưng **không có** `isDefault`.

## PATCH `/api/workspace/{id}/set-default`
Đặt profile làm default (tự bỏ default ở profile khác cùng user).

## DELETE `/api/workspace/{id}`
Xóa profile.

> Enum chi tiết: xem [Appendix](./99-appendix-models.md).

---

[← Stores](./15-stores.md) · [Tiếp: Workspace Types →](./17-workspace-types.md)
