# ARD — ProductPlacement & gợi ý vật phẩm mang theo người

> **Status:** Implemented (2026-08-14).
> **Tiền đề:** engine v3 (`recommendation-scoring-v3.md`) đang chạy. Thay đổi này **không đụng công thức v3** cho đồ để bàn — chỉ thêm một trục phân loại sản phẩm và một chế độ chấm điểm thứ hai.
> **Cập nhật 28/08/2026 — bỏ `Architectural`.** Giá trị này chấm y hệt `Desk` (xem §3, cột "v1 tạm"),
> tức một nhánh luật trùng lặp giữ chỗ cho tính năng "hướng bắt buộc của vật trấn yểm" ở §7 mà chưa làm.
> Enum còn **4 giá trị**; vật trấn yểm nay khai `Desk`. Muốn làm §7 thì thêm lại **kèm** cột hướng bắt buộc
> trên `products` — thêm enum suông chỉ nhân đôi nhánh test mà không đổi hành vi.
>
> **Không xung đột** với `recommendation-scoring-v4-polarity.md` (vẫn ở trạng thái Proposal): v4 thêm trục Âm/Dương chạy song song, ADR này thêm trục Placement — hai việc độc lập, ghép được.

---

## 1. Vấn đề

`RecommendProductsTool` (và `RecommendationService.GenerateAsync` phía sau) neo **toàn bộ** công thức vào một căn phòng:

| Trụ cột | Nguồn |
|---|---|
| Điểm chính | `gap = AdjustedIdeal(phòng) − Current(phòng)`, `score = gap · productVector / ‖gap‖₁` |
| Lọc intent | `TargetVibe(WorkPurpose)` của phòng |
| Directional Validation | hướng cửa/WC/góc tối của phòng |

Vật phẩm **đeo trên người, để trong ví, treo trên xe** (vòng tay, mặt dây, charm) không có phòng nào cả — cả ba trụ cột đều vô nghĩa. Ép truyền `workspaceProfileId` cho nhóm này thì đang trả lời sai câu hỏi: chấm *"vật phẩm bù hành thiếu của căn phòng"* trong khi nghiệp vụ đúng phải là *"vật phẩm bù dụng thần của NGƯỜI"*.

Đồng thời, schema **không có chỗ nào biểu diễn** loại vị trí sử dụng: `Product` chỉ có `SizeClass` (so với `DeskArea`). Hệ quả hiện tại: vòng tay vẫn lọt vào top gợi ý cho workspace kèm hint *"đặt ở hướng Đông của phòng"*.

## 2. Quyết định — một enum `ProductPlacement` phẳng

```csharp
public enum ProductPlacement { Desk, Living, Carry, Consumable }   // Architectural đã bỏ 28/08/2026
```

Cột `products.placement` — **`NOT NULL DEFAULT 'Desk'`**, lưu dạng string như `size_class`.

**Vì sao `NOT NULL DEFAULT` chứ không nullable:** Postgres tự backfill ngay trong migration, mọi truy vấn engine không phải mang nhánh `IS NULL`, và catalog hiện tại giữ nguyên hành vi cũ. Đánh đổi đã biết: sau đó không phân biệt được "vendor đã khai là Desk" với "vendor chưa khai gì" — chấp nhận vì Placement luôn có mặc định an toàn (khác `SizeClass`, nơi "chưa khai" mang nghĩa riêng là *không được chấm điểm*).

**`Category` và `Placement` là hai trục, tuyệt đối không phản chiếu nhau.** `Category` trả lời *"vật này LÀ gì"* (taxonomy để khách duyệt: Cây để bàn, Đá phong thủy, Trang sức phong thủy); `Placement` trả lời *"dùng ở đâu → engine chấm thế nào"*. Không được tạo category kiểu "Vật phẩm mang theo người" — nó nhân đôi cùng một thông tin ở hai nơi, và khi hai nơi lệch nhau thì không có quy tắc nào nói cái nào thắng. Sản phẩm `Carry` vẫn thuộc category thật của nó (vòng tay → "Trang sức phong thủy", charm ví đá → "Đá phong thủy").

**Vì sao KHÔNG neo vào `Category`:** `Category` là `BaseEntity` do vendor tự tạo, **không có `Code` bất biến** — khác `Vibe`/`Style`/`Element` vốn là `ILookup` có code seed từ `seed-data/styles-vibes.json`. Neo logic engine vào tên danh mục sẽ vỡ ngay lần đầu vendor đổi tên hoặc tạo danh mục trùng nghĩa.

**Vì sao một enum phẳng, không tách hai trục:** phương án tách `Placement` × `Nature` (Living/Consumable) từng được cân nhắc và bị loại — theo định nghĩa nghiệp vụ đã chốt, **cả năm giá trị đều mô tả engine phải chấm khác đi thế nào**, tức cùng một trục. Cụ thể: `Living` = "không nặng về hướng, chỉ xét element" (cây đặt theo ánh sáng chứ không theo la bàn); `Consumable` = "không đưa vào gợi ý".

## 3. Ma trận luật theo placement

| Placement | Vào tool nào | Vector mục tiêu | Directional | Lọc khắc mệnh | Lọc vibe theo `WorkPurpose` |
|---|---|---|---|---|---|
| `Desk` (mặc định) | `recommend_products` | `AdjustedIdeal − Current` | mềm: −`DIRECTION_PENALTY` + hint | theo `Scope` | có |
| `Living` | `recommend_products` | `AdjustedIdeal − Current` | **không xét** | theo `Scope` | có |
| `Architectural` | `recommend_products` | `AdjustedIdeal − Current` | mềm (**v1 tạm** — xem §7) | theo `Scope` | có |
| `Carry` | `recommend_personal_items` | vector cá nhân (§4) | **không xét** | **cứng, bất kể `Scope`** | **không** |
| `Consumable` | **loại khỏi cả hai** | — | — | — | — |

`Consumable` bị loại hoàn toàn khỏi mọi gợi ý (vẫn tìm và mua được qua `search_products`). Đánh đổi đã biết: một cây nến sẽ không bao giờ được gợi ý kể cả khi phòng thiếu Hỏa nặng — chấp nhận, vì gợi ý thứ dùng hết rồi phải mua lại không đúng tinh thần "vật phẩm cải thiện không gian".

### Hiện thực: `PlacementPolicy`, không phải `switch` trong `ScoreOne`

Năm placement × bốn luật rất dễ đẻ ra tháp `if` lồng nhau. Luật được khai báo thành bảng trong `ScoringModels.cs`:

```csharp
public sealed record PlacementPolicy(
    bool IsRecommendable, ScoringTarget Target, DirectionMode Direction,
    PersonalConflictMode Conflict, bool FilterByPurposeVibe);
// v3.1 thêm PersonalConflictMode.None; v3.2 thêm PersonalConflictMode.Scaled — xem
// score-explainability-v3.2.md §14: khi trục cá nhân bật, BiKhac trừ USER_CONFLICT_PENALTY × Wp
// thay vì bỏ hẳn penalty (None). Carry giữ nguyên AlwaysHard.
```

`RecommendationScorer.ScoreOne` chỉ đọc policy. Thêm placement sau này = thêm một dòng bảng, không sửa nhánh logic — giữ đúng nguyên tắc engine deterministic.

## 4. Vector mục tiêu cho `Carry` — hybrid Tứ Trụ → Nạp Âm

`PersonalTargetBuilder.Build(dateOfBirth, birthTime, params)`:

1. **Có giờ sinh** → `BaTuCalculator.Compute` → `FavorableElementCodes` (dụng thần). Dụng thần chính nhận `CARRY_PRIMARY_SHARE` (0.60), phụ nhận `CARRY_SECONDARY_SHARE` (0.40), chuẩn hóa Σ=1. Nguồn ghi là `TuTru`.
2. **Không có giờ sinh** → `FengShuiCalculator.BuildPersonalVector` (Nạp Âm: bản mệnh 0.60 / hành sinh 0.30 / hành được sinh 0.10). Nguồn ghi là `NapAm`.
3. **Không có ngày sinh** → trả `null`; service từ chối với 422 kèm hướng dẫn cho AI đi hỏi user. **Không chấm bừa** — thiếu vector cá nhân thì gợi ý vật đeo mất hết căn cứ.

Điểm số: `score = target · productVector / ‖target‖₁` — **cùng thang [-1,1]** với luồng workspace, nên so sánh và hiển thị chung được.

Nguồn (`TuTru`/`NapAm`) được ghi vào `RecommendationLog` stage `EngineScored` **và** trả về trong response, để audit và để AI nói đúng căn cứ thay vì đoán.

Hai tham số mới đi theo quy ước sẵn có của `ScoringParameters.FromRows` — **có default trong code, không cần seed row**; admin muốn chỉnh thì thêm row `scoring_params` qua API `scoring-config`.

| Code | Default |
|---|---|
| `CARRY_PRIMARY_SHARE` | `0.60` |
| `CARRY_SECONDARY_SHARE` | `0.40` |

## 5. Bề mặt tool

### Tool riêng, không phải thêm `mode` vào tool cũ

Ba thứ khác nhau chứ không chỉ tham số: **công thức** (§3), **điều kiện tiên quyết** (workspace profile vs ngày sinh), và **hướng dẫn khi thiếu dữ liệu** (gọi `list_my_workspaces` vs hỏi user ngày sinh). Gộp một tool hai mode buộc `Description` — vốn chính là prompt — phải mô tả song song hai bộ luật, mà LLM local (qwen/gemma qua Ollama) rất dễ trộn nhánh. Rủi ro còn lại là model gọi nhầm tool khi user hỏi mơ hồ; chống bằng cách trỏ chéo trong `Description` của cả hai (kỹ thuật đã dùng ở `get_my_profile` → `compute_destiny_chart`).

| Tool | Params | Ghi chú |
|---|---|---|
| `recommend_products` | `workspaceProfileId` (optional — bỏ trống thì lấy workspace `IsDefault`), `topN` | loại `Carry`/`Consumable` khỏi candidates |
| `recommend_personal_items` (mới) | `topN` | không cần workspace |

### Giảm số lượng gợi ý còn 3–5

Clamp **ở tầng tool**: `topN` mặc định 4, kẹp 3..5. `RecommendationService` giữ nguyên `DefaultTopN = 8` / `MaxTopN = 20` cho `POST /api/recommendations` — FE grid không bị breaking change.

### Dọn payload gửi LLM

- **Bỏ `PersonalWeight`**: legacy v2, engine v3 đã thay bằng `WorkspaceScope`. Giá trị trả về chỉ là `wsType?.PersonalWeight ?? 1.0` — model đọc được sẽ diễn giải sai.
- **Thêm `recommendationId`**: để FE/model deep-link lại phiên gợi ý qua `GET /api/recommendations/{id}` sẵn có.
- **Fallback workspace mặc định**: model không truyền `workspaceProfileId` thì dùng profile `IsDefault` thay vì báo lỗi.

## 6. Persistence

`recommendations` phục vụ cả hai loại phiên:

- `workspace_profile_id` → **nullable** (navigation `WorkspaceProfile?`), vì phiên cá nhân không có phòng.
- Cột mới `kind` (`RecommendationKind { Workspace, PersonalCarry }`, string) — không có cột này thì `GET /recommendations/{id}` và FE không biết render loại nào, và thống kê sẽ lẫn.
- `personal_weight` (NOT NULL) ghi `1.0` cho phiên cá nhân.

## 7. Không làm (out of scope)

- **Nhánh hướng cứng cho `Architectural`.** Giá trị enum được thêm ngay để tránh migration lần hai, nhưng v1 nó chấm y hệt `Desk`. Hướng bắt buộc của vật trấn yểm (gương bát quái phải chiếu ra ngoài) là **thuộc tính của chính vật phẩm**, không suy ra được từ hành trội như `ValidateDirection` đang làm → cần thêm cột hướng bắt buộc trên `products` + nhánh loại-cứng. **Rủi ro trong giai đoạn này:** nếu catalog có gương bát quái thật, nó vẫn nhận hint hướng suy-ra-từ-ngũ-hành, tức vẫn có thể sai. Phương án an toàn: chưa bán nhóm đó cho tới khi làm nhánh cứng.
- **Lọc theo ý định (cầu tài / bình an / sức khỏe / thi cử).** `VibeCodes` hiện chỉ có Focus/Relax/Creative/Calm/Energize — là vibe của *không gian*. Thêm nhóm vibe mới đòi khai lại data cho toàn bộ sp `Carry`. V1 chấm thuần ngũ hành cá nhân — vốn là tiêu chí chính khi chọn vật đeo. *(Bộ lọc vibe cứng nêu ở đây đã được mềm hóa ngay sau đó — xem `vibe-soft-scoring.md`.)*
- **Gọi AI microservice diễn giải cho phiên cá nhân.** `Contracts/Recommendation` có `AiWorkspaceInfo` bắt buộc — gửi một workspace giả là dữ liệu sai cho AI. Mở rộng contract kéo theo sửa cả service Python. Phiên cá nhân dừng ở `Status = Scored`, không `Summary`; LLM chat (người tiêu thụ thật) tự diễn giải từ `matchFacts`/`cautionFacts`.
- **Trục `ProductNature`** (Living/Consumable như thuộc tính độc lập với vị trí) — đã gộp vào enum phẳng, xem §2.
- **Engine đọc `WorkspaceProfile.Lighting`.** Hiện `Lighting` chỉ được nhét vào payload gửi AI diễn giải, **không tham gia chấm điểm**. Đưa ánh sáng vào công thức (đặc biệt cho `Living`) là tính năng riêng đáng làm nhưng độc lập với thay đổi này.

## 8. Danh sách file thay đổi

### Domain
- `Enums/Catalog/ProductPlacement.cs` — **mới**.
- `Enums/Recommendation/RecommendationKind.cs` — **mới**.
- `Entities/Catalog/Product.cs` — `Placement` (default `Desk`).
- `Entities/CustomerCare/Recommendation.cs` — `WorkspaceProfileId` → `Guid?`, `WorkspaceProfile` → nullable, thêm `Kind`.

### Application
- `Engine/ScoringModels.cs` — `PlacementPolicy` + 3 enum luật; `ScoringContext.PersonalNeedVector`; `ProductFacts.Placement`; 2 param `CARRY_*`.
- `Engine/PersonalTargetBuilder.cs` — **mới** (§4).
- `Engine/RecommendationScorer.cs` — `ScoreOne` đọc policy; `DescribeGap` phân biệt lời văn phòng vs người; `ValidateDirection` chỉ chạy khi `DirectionMode.Soft`.
- `Features/CustomerCare/Services/IRecommendationService.cs` + `RecommendationService.cs` — `GeneratePersonalAsync`.
- `Features/CustomerCare/DTOs/RecommendationDtos.cs` — `GeneratePersonalRecommendationRequest`, `RecommendationResponse.Kind` + `PersonalTarget`.
- `Features/CustomerCare/Tools/RecommendPersonalItemsTool.cs` — **mới**; `RecommendProductsTool.cs` — cập nhật (§5).
- `Features/Catalog/DTOs/ProductDtos.cs` + `ProductFengShuiDtos.cs` — `Placement` ở create/detail/fengshui.
- `Features/Catalog/Services/ProductService.cs`, `Mappings/CatalogMappingProfile.cs`.
- `Interfaces/Repositories/IProductRepository.cs` — `GetScorableCandidatesAsync(placements?)`, `SetFengShuiAsync(..., placement)`.
- `DependencyInjection.cs` — đăng ký tool mới.

### Infrastructure
- `Configurations/ProductConfiguration.cs` — `placement` string, default `Desk`.
- `Configurations/RecommendationConfiguration.cs` — `workspace_profile_id` nullable, `kind`.
- `Repositories/ProductRepository.cs` — lọc theo placement.
- Migration `ProductPlacementAndPersonalRecommendation`.

### WebAPI
- `Controllers/RecommendationsController.cs` — `POST /api/recommendations/personal`.

## 9. Test cases tối thiểu

1. Catalog cũ (chưa ai khai placement) → mọi sp có `placement = Desk` → ranking `recommend_products` **không đổi** so với trước migration.
2. Sp `Consumable` không xuất hiện trong `recommend_products` lẫn `recommend_personal_items`, nhưng vẫn ra ở `search_products`.
3. Sp `Carry` không lọt vào `recommend_products` dù điểm gap cao.
4. Sp `Living` không bao giờ bị trừ `DIRECTION_PENALTY`, kể cả khi mọi hướng hợp đều bị chắn.
5. Sp `Carry` khắc bản mệnh vẫn **bị loại cứng** kể cả ở `Public` và kể cả khi trục cá nhân bật —
   `AlwaysHard` không đổi theo `Wp` (v3.2 §14.6 #4).
5. User có `BirthTime` → `PersonalTarget.Source == "TuTru"`; xóa `BirthTime` → `"NapAm"`; cùng user, hai lần chấm cho kết quả khác nhau và log ghi đúng nguồn.
6. User không có `DateOfBirth` → `recommend_personal_items` trả 422 có hướng dẫn, **không** trả danh sách.
7. Sp `Carry` có hành trội khắc bản mệnh → bị loại **kể cả** khi `WorkspaceScope` không phải `Private` (khác luật `Desk`).
8. `recommend_personal_items` không sinh `RecommendationLog` stage `AiRequested` (không gọi AI microservice), `Status == Scored`.
9. Phiên cá nhân lưu với `workspace_profile_id IS NULL`, `kind = 'PersonalCarry'`; `GET /recommendations/{id}` đọc lại được đủ tên sản phẩm.
10. Cả hai tool clamp `topN`: gửi 1 → 3, gửi 20 → 5, bỏ trống → 4. `POST /api/recommendations` vẫn cho `topN = 12`.
11. `recommend_products` không truyền `workspaceProfileId` → dùng workspace `IsDefault`; user chưa có workspace nào → lỗi có hướng dẫn gọi `list_my_workspaces`.
12. Payload cả hai tool **không còn** khóa `personalWeight`, **có** `recommendationId`.
