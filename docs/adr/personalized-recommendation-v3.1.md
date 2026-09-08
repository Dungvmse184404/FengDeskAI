# ARD — Personalized Recommendation v3.1: trục cá nhân cho luồng workspace

> **Status:** **Đã code (2026-08-26)** — CHƯA chạy migration, CHƯA build/test.
> Việc còn lại: `dotnet ef migrations add PersonalizedRecommendationV31`, `database update`, `seed`, `dotnet build`, chạy golden set (§14).
> **Tiền đề (đọc trước):** `recommendation-scoring-v3.md` (engine gap-matching), `product-placement-personal-recommendation.md` (trục `ProductPlacement` + `PlacementPolicy`, Implemented 2026-08-14), `vibe-soft-scoring.md` (vibe mềm + `MIN_SCORE_THRESHOLD`, Implemented 2026-08-15).
> **Phạm vi:** chỉ đụng nhánh `ScoringTarget.WorkspaceGap` (placement `Desk` / `Living`). **Không** đụng nhánh `Carry` — nhánh đó đã chấm 100% theo cá nhân qua `PersonalTargetBuilder`.
> **Không bao gồm:** v4 Polarity (Proposal riêng). `Architectural` đã bị bỏ khỏi enum (28/08) — gộp vào `Desk`.

---

## 1. Vấn đề

Yêu cầu mới: **đưa thông tin cá nhân người dùng vào tư vấn & đề xuất**.

Sau `product-placement-personal-recommendation.md`, hệ thống đã có **một** luồng cá nhân hóa đầy đủ — nhưng chỉ cho vật mang theo người:

| Luồng | Vector mục tiêu | Cá nhân tham gia thế nào |
| --- | --- | --- |
| `Carry` → `recommend_personal_items` | **dụng thần Tứ Trụ / Nạp Âm** | ✅ **là toàn bộ điểm** |
| `Desk` / `Living` → `recommend_products` | gap của phòng | ❌ **chỉ là hình phạt** |

Với luồng workspace, công thức đang chạy là:

```
score = clamp( gapScore − userPenalty − dirPenalty − vibePenalty , −1, +1 )
        ↑ 100% phòng    ↑ −0.30 khi BiKhac (hoặc loại thẳng nếu Private)
```

→ Mệnh user chỉ có thể làm sản phẩm **biến mất** hoặc **bị trừ điểm**, **không thể** làm sản phẩm **được ưu tiên**. Hai người mệnh khác nhau, cùng một căn phòng ⇒ thứ hạng gần như y hệt nhau, chỉ khác vài món bị loại.

Đồng thời, dữ liệu cá nhân đã tính sẵn nhưng không vào điểm:

| Đã có trong code | Dùng ở đâu |
| --- | --- |
| `DestinyCalculator` → Cung mệnh, Kua, 8 hướng Bát Trạch | chỉ hiển thị + gửi AI |
| `BaTuCalculator` → Tứ Trụ, dụng thần | chỉ nhánh `Carry` + `compute_destiny_chart` |
| `WorkspaceType.PersonalWeight` | **dead code** — biến trong service tên `legacyWeight` |
| bảng `feng_shui_rules` (25 dòng, admin chỉnh được) | `GetAllRulesAsync` tồn tại nhưng **không ai gọi** |

---

## 2. Quyết định

| # | Quyết định |
| --- | --- |
| **D1** | Thêm số hạng **`personalScore`** vào nhánh `WorkspaceGap`, trọng số **`Wp`** theo `WorkspaceType.Scope` (3 bậc) |
| **D2** | `personalScore` tính từ bảng **`feng_shui_rules`** — **không** dùng dot-product (luôn ≥ 0, không phạt được khắc mệnh) |
| **D3** | Với nhánh workspace: **bỏ hard-filter + `userPenalty`** khi trục cá nhân bật; xung khắc đã nằm trong `personalScore`. Lưới an toàn dùng `MIN_SCORE_THRESHOLD` sẵn có |
| **D4** | Nguồn chân lý riêng tư = **`WorkspaceType.Scope`**. `IsPublic` + `PersonalWeight` → `[Obsolete]` |
| **D5** | Thêm **`Aspiration`** làm **tham số runtime** (không lưu trên `User`) — lọc candidates trước khi chấm. Đây đúng là mục backlog `product-placement…` §7 để lại |
| **D6** | Tag `Aspiration` trên sản phẩm do **admin duyệt** (`is_approved`) |
| **D7** | `FinalRank` do AI quyết định — **giữ nguyên**, chỉ thêm guard hoán vị |
| **D8** | **Kill-switch:** seed `PERSONAL_WEIGHT_* = 0` → ranking **byte-identical** với hiện tại; bật qua API `scoring-config` |

### Những gì KHÔNG làm nữa (đã có sẵn, tránh trùng lặp)

| Từng cân nhắc | Thực tế đã có |
| --- | --- |
| Enum `ProductKind {Consumable, Wearable, Decor, Living}` | ❌ **trùng `ProductPlacement`** (5 giá trị, đã Implemented) — dùng luôn cái có sẵn |
| Param `kind` cho `recommend_products` | ❌ đã tách **tool riêng** `recommend_personal_items` |
| Chuẩn hóa thang điểm giữa 2 loại | ❌ đã xử lý: cả hai đều `target · productVector / ‖target‖₁` ∈ [−1,1] |
| `SizeClass` × `DeskArea` vào chấm điểm | ⚠️ **`SizeClass` đã CHUYỂN từ `Product` sang `ProductItem`** (migration `MoveSizeClassToProductItem`, 14/08) — kích thước biến thiên theo SKU. Vẫn **chưa vào chấm điểm**, v3.1 không đụng |
| Bỏ `.Include(p => p.Styles)` thừa | ✅ vẫn còn thừa — xem §9 |

---

## 3. Công thức v3.1

### 3.1 `personalScore` — có dấu, từ `feng_shui_rules`

**Vì sao không dùng dot-product:** `personalVector · productVector` với hai vector không âm thì kết quả **luôn ≥ 0** → không thể phạt sản phẩm khắc mệnh → trục cá nhân trở nên vô nghĩa. Đó cũng là lý do nhánh `Carry` dùng được dot-product: ở đó vector mục tiêu là *dụng thần* (thứ người ta **cần**), không phải *bản mệnh* (thứ có thể bị khắc).

```
personalDominant = ctx.PersonalVector.Dominant()

personalScore = Σ  productVector[e] × RuleScore(personalDominant, e)
               e ∈ {Kim, Moc, Thuy, Hoa, Tho}
```

`RuleScore` đọc từ `feng_shui_rules` (25 dòng đã seed, admin chỉnh qua `scoring-config`):

| Quan hệ | Điểm mặc định |
| --- | :---: |
| `TuongHoa` — tỷ hòa | **+1.0** |
| `TuongSinh` — obj sinh mệnh | **+0.8** |
| `TuongKhac` — mệnh khắc obj | **+0.2** |
| `TietKhi` — mệnh sinh obj (hao) | **−0.2** |
| `BiKhac` — obj khắc mệnh | **−1.0** |

`productVector` chuẩn hóa Σ=1 và `RuleScore ∈ [−1,+1]` ⇒ **`personalScore ∈ [−1,+1]`**, cùng thang với `gapScore`.

Repository đã sẵn sàng: `IRecommendationRepository.GetAllRulesAsync(ct)` (hiện chưa có caller).

### 3.2 Trọng số `Wp` theo `Scope`

```csharp
decimal Wp = wsType?.Scope switch
{
    WorkspaceScope.Private => p.PersonalWeightPrivate,   // 0.50
    WorkspaceScope.Shared  => p.PersonalWeightShared,    // 0.30
    WorkspaceScope.Public  => p.PersonalWeightPublic,    // 0.00
    null                   => p.PersonalWeightPrivate,   // type null (dữ liệu cũ) → coi như riêng tư
};

if (user.DateOfBirth is null) Wp = 0m;   // không có mệnh → dồn 100% về gapScore
```

**Vì sao 3 bậc chứ không phải bool do user chọn:** phòng khách / bếp / phòng ăn ở nhà riêng nằm **giữa** hai thái cực — ép nhị phân thì sai cả hai chiều. Và người dùng không có căn cứ để trả lời: "riêng tư" trong phong thủy khác "riêng tư" trong đời thường; câu trả lời không ổn định giữa các lần nhập ⇒ ranking không tái lập được. Bậc theo loại phòng là dữ liệu ổn định, admin tinh chỉnh tập trung.

### 3.3 Công thức đầy đủ — chỉ đổi nhánh `WorkspaceGap`

| Placement | `Target` | v3.1 đụng vào? |
| --- | --- | :---: |
| `Desk`, `Living` | `WorkspaceGap` | ✅ **thêm `Wp · personalScore`** |
| `Carry` | `PersonalNeed` | ❌ giữ nguyên 100% |
| `Consumable` | — | ❌ vẫn `IsRecommendable = false` |

```
// Desk / Living  (Wp > 0)                                    ⚠️ v3.2 sửa — xem ghi chú cuối §3.3
score = clamp( (1 − Wp)·gapScore + Wp·personalScore − userPenalty − dirPenalty − vibePenalty , −1, +1 )

// Wp == 0  → giữ nguyên 100% hành vi hiện tại (kill-switch, xem §5)
score = clamp( gapScore − userPenalty − dirPenalty − vibePenalty , −1, +1 )

// Carry  — KHÔNG ĐỔI
score = clamp( personalNeed·productVector/‖personalNeed‖₁ − userPenalty − vibePenalty , −1, +1 )
```

> `userPenalty` biến mất khỏi nhánh workspace **khi và chỉ khi `Wp > 0`** — nếu giữ cả hai thì xung khắc bị tính hai lần (một lần âm trong `personalScore`, một lần trừ thẳng).

> ### 🔴 SUPERSEDED (một phần) — v3.2
> Đoạn ngay trên **không còn đúng**. Xem
> [`score-explainability-v3.2.md` §14](./score-explainability-v3.2.md) (phương án **L2**).
>
> **Vấn đề:** khi phòng cần đúng hành khắc mệnh, hai lực triệt tiêu nhau —
> `d[Kim] = 0.5×(+1.0) + 0.5×(−1.0) = 0` ⇒ sản phẩm **khắc bản mệnh** hiển thị **"Trung tính" 50%**.
>
> **Sửa:** `PersonalConflictMode.None` → **`Scaled`**, giữ lại penalty nhưng **co giãn theo `Wp`**:
> ```csharp
> if (GetRelation(destiny, productDominant) == FengShuiRelation.BiKhac)
>     userPenalty = ctx.Params.UserConflictPenalty * ctx.PersonalWeight;   // 0.60 × 0.50 = 0.30
> ```
> **Không phải tính hai lần:** `personalScore` đo *mức độ hợp* (liên tục), còn "bị khắc" là một
> *phạm trù kiêng kỵ* — hai đại lượng khác loại nên tách hai số hạng.
>
> **Đứt gãy tại `Wp = 0`** (0.01 → phạt 0.006; 0.00 → loại cứng/phạt đủ 0.60) là **có chủ đích**:
> `Wp = 0` nghĩa là tắt hẳn trục cá nhân v3.1, rơi trọn về luật v3.

### 3.4 `PersonalConflictMode` — thêm giá trị `None`

```csharp
public enum PersonalConflictMode
{
    ByScope,      // luật v3: loại cứng khi Private, còn lại trừ điểm
    AlwaysHard,   // Carry — giữ nguyên
    None,         // v3.1: xung khắc đã nằm trong personalScore, không loại & không trừ thêm
    Scaled,       // v3.2 (L2): không loại, trừ USER_CONFLICT_PENALTY × Wp  ← THAY None cho Desk/Living
}
```

> **v3.2:** `Desk`/`Living`/`WorkspaceFit` chuyển `ByScope → **Scaled**` (không phải `None`) khi `Wp > 0`.
> `None` giữ lại cho `Public` — không gian chung không lọc/phạt theo bản mệnh một người (§14.6 #9).
> `Carry` giữ `AlwaysHard` không đổi.

`PlacementPolicy.For` chỉ đổi khi `Wp > 0`: `Desk`/`Living`/`WorkspaceFit` chuyển `ByScope → None`. Vì `PlacementPolicy` là **bảng khai báo**, thay đổi gói gọn trong `ScoringModels.cs` — `ScoreOne` không mọc thêm nhánh `if`.

**Lưới an toàn thay cho hard-filter:** sản phẩm khắc mệnh nhận `personalScore ≈ −1` nên tự rơi xuống đáy. Admin muốn cắt hẳn thì nâng **`MIN_SCORE_THRESHOLD`** (đã có từ `vibe-soft-scoring.md`) lên `0.0` — cùng một cơ chế, không cần bộ lọc riêng, và **không bao giờ trả danh sách rỗng** vì cắt theo điểm tổng chứ không theo một thuộc tính.

### 3.5 Delta

| Thành phần | Hiện tại | v3.1 (`Wp > 0`) |
| --- | --- | --- |
| `gapScore` | **1.00** | **1 − Wp** → 0.50 / 0.70 / 1.00 · *v3.2: chia `\|gap\|₁/2`, miền ±1.0* |
| `personalScore` | ❌ không có | **Wp** → 0.50 / 0.30 / 0.00 — *mới* |
| hard-filter `BiKhac` (workspace) | ✅ khi `Private` | ❌ bỏ (`PersonalConflictMode.Scaled`) |
| `userPenalty` (workspace) | ✅ −0.30 | ~~bỏ~~ → **v3.2: `USER_CONFLICT_PENALTY × Wp` = 0.60×0.50 = −0.30** |
| `dirPenalty`, `vibePenalty`, `MIN_SCORE_THRESHOLD` | ✅ | ✅ **giữ nguyên** |
| nhánh `Carry` | ✅ | ✅ **không đụng** |
| Aspiration filter | ❌ | **hard, ở tầng candidates** — *mới* |

---

## 4. `Aspiration` — tham số runtime, không lưu trên `User`

`product-placement-personal-recommendation.md` §7 để lại đúng mục này trong "Không làm": *"Lọc theo ý định (cầu tài / bình an / sức khỏe / thi cử)"*. v3.1 làm nốt.

### 4.1 Vì sao KHÔNG lưu vào `User`

Ý định thay đổi theo bối cảnh: cùng một người cần **tài lộc** ở văn phòng và **sức khỏe** ở nhà. Một cột trên `User` sẽ stale ngay sau lần đặt đầu tiên. Đây là **tham số của phiên tư vấn**, không phải thuộc tính con người.

### 4.2 Enum + junction

```csharp
// Domain/Enums/Catalog/Aspiration.cs
public enum Aspiration { Wealth, Career, Health, Relationship, Study }
```

```csharp
// Domain/Entities/Catalog/ProductAspiration.cs
/// <summary>
/// Bảng nối Product ↔ <see cref="Aspiration"/>. Junction thuần: composite key
/// (product_id, aspiration) — cùng pattern <see cref="ProductVibe"/>.
/// Vendor đề xuất, admin bật <see cref="IsApproved"/>; chỉ dòng đã duyệt mới lọc.
/// </summary>
public class ProductAspiration
{
    public Guid ProductId { get; set; }
    public Aspiration Aspiration { get; set; }
    public bool IsApproved { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Product Product { get; set; } = null!;
}
```

> **Vì sao enum chứ không phải lookup table** như `Vibe`/`Style`: 5 giá trị là 4 cung Bát Trạch cố định (Sinh Khí / Thiên Y / Diên Niên / Phục Vị), admin **không** thêm được — cùng tính chất với `FengShuiElement`, vốn cũng là enum.

### 4.3 Vị trí trong pipeline — lọc TRƯỚC, chấm SAU

```
candidates = GetScorableCandidatesAsync(placements, aspiration)
             WHERE ... AND (@aspiration IS NULL
                            OR EXISTS(product_aspirations pa
                                      WHERE pa.product_id = p.id
                                        AND pa.aspiration = @aspiration
                                        AND pa.is_approved))
   ↓  nếu rỗng VÌ aspiration → BỎ filter + ghi caution, KHÔNG trả 422
top = scorer.Score(context, candidates)      // trọng số không đổi
```

Fallback là bắt buộc: catalog đầu chưa ai gắn tag ⇒ mọi câu hỏi "tôi muốn tiền tài" sẽ trả rỗng nếu không có nó.

### 4.4 Bề mặt tool

Thêm param `aspiration` cho **cả ba** tool (mô tả giống nhau để LLM không phải học hai luật):

| Tool | Vai trò của `aspiration` |
| --- | --- |
| `recommend_products` | lọc candidates trước khi chấm gap |
| `recommend_personal_items` | lọc candidates trước khi chấm dụng thần |
| `search_products` | lọc thuần, **không** chấm điểm |

```csharp
["aspiration"] = new("string",
    "Filter by the feng-shui goal the user states: Wealth, Career, Health, Relationship, Study. "
  + "Omit when they don't mention one; if their goal seems relevant but unclear, ask them.");
```

**Nguyên tắc prompt:** mô tả dạng *note nhẹ* ("if unclear, ask them"), **không** dùng "MUST/ALWAYS" — tránh chất tải ràng buộc lên system prompt của LLM local (qwen/gemma), đúng như lập luận đã dùng ở `product-placement…` §5.

### 4.5 `placementHint` theo aspiration

`FengShuiCalculator` thêm map `Aspiration → cung Bát Trạch`, để `ValidateDirection` chọn **đúng một** trong 4 hướng tốt thay vì lấy phần tử đầu tiên như hiện nay:

| Aspiration | Cung | Lời hint |
| --- | --- | --- |
| `Wealth`, `Career` | Sinh Khí | tài lộc, thăng tiến |
| `Health` | Thiên Y | sức khỏe, phục hồi |
| `Relationship` | Diên Niên | quan hệ, hòa hợp |
| `Study` | Phục Vị | ổn định, tập trung |

Chỉ áp cho `DirectionMode.Soft` (chỉ `Desk`). `Living` và `Carry` vẫn dùng `PlacementHintWithoutDirection`.

---

## 5. Tham số engine mới

| Code | Default trong code | **Seed** | Ghi chú |
| --- | :---: | :---: | --- |
| `PERSONAL_WEIGHT_PRIVATE` | `0.50` | **`0.00`** | kill-switch |
| `PERSONAL_WEIGHT_SHARED` | `0.30` | **`0.00`** | kill-switch |
| `PERSONAL_WEIGHT_PUBLIC` | `0.00` | `0.00` | |

**Kill-switch — cùng cách `VIBE_FILTER_HARD` và `POLARITY_SHARE` đã làm:** seed `0` khi merge ⇒ `Wp = 0` ở mọi phòng ⇒ nhánh `Wp == 0` của §3.3 chạy, `PersonalConflictMode` giữ `ByScope`, `userPenalty` giữ nguyên ⇒ **ranking byte-identical với trước**. Sau khi đối chiếu golden set, admin nâng lên `0.50 / 0.30` qua API `scoring-config` — **không cần deploy**, và đây cũng là đường lui.

Thêm 3 hằng vào `ScoringParamCodes`, 3 property vào `ScoringParameters` + `FromRows`, 3 row vào `seed-data/scoring-params.json` (`ScoringParamSeeder` idempotent theo code → DB hiện có chạy lại seed sẽ nhận thêm, không đụng giá trị cũ).

---

## 6. Seed — sửa `Scope` cho khu vực tại gia

`seed-data/workspace-types.json` hiện để **9 loại phòng sinh hoạt chung là `Private`**, khiến chúng nhận `Wp` cao nhất và bị hard-filter khắc mệnh như bàn cá nhân:

```
Kitchen · Living Room · Dining Room · Home Theater · Guest Room
Balcony · Home Gym · Kids Room · Rooftop Garden      →  "scope": "Shared"
```

`WorkspaceTypeSeeder` idempotent **theo tên** ⇒ row đã tồn tại **không** được cập nhật. Cần script SQL một lần:

```sql
UPDATE workspace_types
SET    scope = 'Shared'
WHERE  is_system_seeded
  AND  name IN ('Kitchen','Living Room','Dining Room','Home Theater','Guest Room',
                'Balcony','Home Gym','Kids Room','Rooftop Garden');
```

---

## 7. Bug sửa kèm

### 7.1 🐛 Năm sinh không nhất quán — **3 call site**

`RecommendationService.cs` dòng **69**, **219**, **340**:

```csharp
FengShuiCalculator.BuildPersonalProfile(user.DateOfBirth, user.Gender)  // → Solar2Lunar → năm ÂM  ✔
FengShuiCalculator.BuildPersonalVector(dob.Year, ...)                   // → năm DƯƠNG thô        ✘
```

Người sinh tháng 1–2 **trước Tết** có **mệnh hiển thị ≠ mệnh dùng chấm điểm**. v3.1 làm mệnh trở thành số hạng điểm thật ⇒ lỗi này chuyển từ "sai chỗ hiển thị" thành **"sai thứ hạng"**.

**Sửa:** đổi chữ ký thành `BuildPersonalVector(DateTime dateOfBirth, ...)` và tự gọi `LunarCalendarConverter.Solar2Lunar` bên trong — bịt luôn khả năng gọi sai ở call site thứ tư.

### 7.2 ⚠️ `FinalRank` do AI quyết định — giữ nguyên, thêm guard

```
Engine chọn topN  →  BaseRank 1..N, FinalRank = BaseRank
       ↓  BuildAiRequest
AI trả về  →  ApplyAiResponse:
       • lọc productId lạ → log "ContractViolation"    (KHÔNG thêm được sản phẩm)
       • item.FinalRank = explained.FinalRank          (RecommendationService.cs:537)
       ↓  BuildResponse: .OrderBy(Rank)  nhưng  Score = BaseScore
```

AI chỉ **hoán vị trong topN**. Nhưng vì sắp theo AI mà hiển thị điểm engine, **`Score` có thể không giảm dần trên UI**:

```
#1  Cây Kim Tiền     0.55
#2  Thạch anh hồng   0.80     ← user thắc mắc ngay
```

**Quyết định:** giữ hành vi (chưa khóa). Thêm guard ~10 dòng: `finalRank` không phải hoán vị hợp lệ của `1..N` (trùng / thiếu / ngoài biên) ⇒ fallback `BaseRank` + ghi `RecommendationLog`.

**Quan sát runtime trước khi quyết khóa hẳn** — `recommendation_logs` đã lưu sẵn:

```sql
SELECT l.stage, l.detail
FROM   recommendation_logs l
JOIN   recommendations r ON r.id = l.recommendation_id
WHERE  r.user_id = '<uuid>'
ORDER  BY r.created_at DESC, l.created_at
LIMIT  20;
-- EngineScored → thứ tự gốc + gap · AiRequested → payload · AiResponded → finalRank AI trả
```

---

## 8. Pipeline sau refactor

```
RecommendationService.GenerateAsync(userId, request)
 │
 ├─ 1. Load profile + user
 ├─ 2. FengShuiCalculator.BuildPersonalProfile(DateOfBirth, Gender)
 ├─ 3. ScoringParameters.FromRows(scoring_params)                       → 18 tham số
 ├─ 4. FengShuiCalculator.BuildPersonalVector(DateOfBirth, …)           ★ sửa bug §7.1
 ├─ 5. BuildWorkspaceContextAsync → WorkspaceElementAnalyzer.Analyze
 ├─ 6. ResolvePersonalWeight(wsType?.Scope, user.DateOfBirth)           ★ mới → Wp
 ├─ 7. Recommendations.GetAllRulesAsync()                               ★ mới → RuleScore lookup
 ├─ 8. Products.GetScorableCandidatesAsync(WorkspacePlacements,
 │                                          request.Aspiration)          ★ thêm tham số
 │        ↳ rỗng vì aspiration → bỏ filter + caution (không 422)
 ├─ 9. ToFacts(...) → ProductFacts(Id, Vector, Vibes, Placement)        (không đổi)
 ├─10. violated = Entrance ∪ Toilet ∪ Dark
 ├─11. RecommendationScorer.Score(context, candidates)                  ★ công thức §3.3
 └─12. BuildAiRequest → _ai.ExplainAsync → ApplyAiResponse (+ guard §7.2)
```

---

## 9. Danh sách file đã thay đổi

### Domain

| File | Việc |
| --- | --- |
| `Enums/Catalog/Aspiration.cs` | ✅ **mới** — 5 giá trị |
| `Entities/Catalog/ProductAspiration.cs` | ✅ **mới** — junction + `IsApproved`/`ApprovedBy`/`ApprovedAt` |
| `Entities/Catalog/Product.cs` | ✅ `+ ICollection<ProductAspiration> Aspirations` |
| `Entities/Workspace/WorkspaceType.cs` | ✅ đánh dấu LEGACY cho `IsPublic`, `PersonalWeight` **bằng XML comment, KHÔNG dùng `[Obsolete]`** — attribute sẽ đẻ warning ở mọi call site cũ (seeder, `legacyWeight`, DTO), đúng cách repo đã xử lý `WorkspaceProfile.FengShuiElement` |

### Application — Engine

| File | Việc |
| --- | --- |
| `Engine/ScoringModels.cs` | ✅ `ScoringParamCodes` +3 · `ScoringParameters` +3 + `PersonalWeightFor(scope)` · `PersonalConflictMode.None` · `PlacementPolicy.For(placement, personalBlendActive)` + `WorkspaceFit(bool)` · `ScoringContext` thêm `PersonalWeight`/`RuleScores`/`Aspiration`/`AspirationDirections` + `PersonalBlendActive` + `RuleScoreOf` · record `AspirationDirection` |
| `Engine/RecommendationScorer.cs` | ✅ bước 2d blend · `PersonalAffinity` · `DescribePersonalAffinity` · bước 2b bỏ qua khi `Conflict == None` · `ValidateDirection` ưu tiên cung Bát Trạch khớp mục tiêu · `DirectionVi` ủy quyền về `FengShuiCalculator` |
| `Engine/FengShuiCalculator.cs` | ✅ `GetLunarYear` (§7.1) · overload `BuildPersonalVector(DateOnly/DateTime)` · `GetNapAmElement(DateOnly)` · `GetCungForAspiration` · `DirectionVi`/`ParseDirectionVi` |
| `Engine/PersonalTargetBuilder.cs` | ✅ fallback Nạp Âm dùng năm ÂM (§7.1) |

### Application — Services / DTO / Tools

| File | Việc |
| --- | --- |
| `CustomerCare/Services/RecommendationService.cs` | ✅ `ResolvePersonalWeight` · `LoadRuleScoresAsync` · `BuildAspirationDirections` · `LoadCandidatesAsync` (fallback) · context v3.1 ở cả `GenerateAsync` và `GetProductFitAsync` · log `EngineScored` thêm `personalWeight`/`aspiration`/`aspirationRelaxed` · guard `FinalRank` (§7.2) · sửa 3 call site năm âm |
| `CustomerCare/DTOs/RecommendationDtos.cs` | ✅ `Aspiration?` ở cả 2 request · `RecommendationResponse.Note` |
| `CustomerCare/Tools/ToolArgs.cs` | ✅ **mới** `GetEnum<T>` |
| `CustomerCare/Tools/RecommendProductsTool.cs` · `RecommendPersonalItemsTool.cs` | ✅ param `aspiration` + trả `note`/`engineNote` |
| `CustomerCare/Tools/SearchProductsTool.cs` | ✅ param `aspiration` + trả `note` khi 0 kết quả vì filter |
| `Catalog/DTOs/ProductDtos.cs` | ✅ `ProductDetailResponse.Aspirations` (chỉ đã duyệt) · `ProductQueryParams.Aspiration` |
| `Catalog/DTOs/ProductFengShuiDtos.cs` | ✅ `SetProductFengShuiRequest.Aspirations` (đề xuất) · `ApproveProductAspirationsRequest` **mới** · response tách `ApprovedAspirations`/`PendingAspirations` |
| `Catalog/Services/ProductService.cs` + `IProductService.cs` | ✅ ghi đề xuất trong `SetFengShuiAsync` · **`ApproveAspirationsAsync` mới** · map `Aspiration` vào `ProductSearchFilter` |
| `Catalog/Mappings/CatalogMappingProfile.cs` | ✅ map `Aspirations` — **lọc `IsApproved`** |
| `Interfaces/Repositories/IProductRepository.cs` | ✅ `GetScorableCandidatesAsync(placements?, aspiration?, ct)` · `ReplaceProposedAspirationsAsync` · `ApproveAspirationsAsync` · `GetAspirationsAsync` · `ProductSearchFilter.Aspiration` |

### Infrastructure

| File | Việc |
| --- | --- |
| `Configurations/ProductAspirationConfiguration.cs` | ✅ **mới** — composite PK, index `aspiration` có `HasFilter("is_approved")` |
| `Configurations/ProductConfiguration.cs` | ✅ `HasMany(p => p.Aspirations)` cascade |
| `Repositories/ProductRepository.cs` | ✅ filter aspiration ở candidates + search · 3 method thẻ · `GetDetailAsync` include `Aspirations` · **đã bỏ `.Include(p => p.Styles)`** khỏi `GetScorableCandidatesAsync` |
| `seed-data/scoring-params.json` | ✅ 3 row mới, **seed `0.00`** (kill-switch) |
| `seed-data/workspace-types.json` | ⬜ **CHƯA đụng** — dùng script SQL §6 thay vì sửa file (seeder idempotent theo tên, sửa file không cập nhật row cũ) |
| Migration `PersonalizedRecommendationV31` | ⬜ **CHƯA tạo** — xem §10 |

### WebAPI

| File | Việc |
| --- | --- |
| `Controllers/ProductsController.cs` | ✅ `PUT /api/products/{id}/aspirations` — `[Authorize(ManagerOrAbove)]` |
| `Controllers/RecommendationsController.cs` | ⬜ không cần sửa — `aspiration` nằm trong body DTO |

### Tests

| File | Việc |
| --- | --- |
| `tests/FengDeskAI.UnitTests/FengShuiCalculatorTests.cs` | ✅ `ENGINE-PROFILE-05/06` — regression năm âm (§7.1) |

## 10. Migration & chạy local

```
+ product_aspirations  (product_id uuid, aspiration varchar(20),
                        is_approved bool NOT NULL DEFAULT false,
                        approved_by uuid NULL, approved_at timestamptz NULL,
                        PK(product_id, aspiration),
                        FK product_id → products ON DELETE CASCADE,
                        INDEX(aspiration) WHERE is_approved)
```

⚠️ **Bỏ `Architectural`:** enum lưu dạng string nên dòng cũ có `placement = 'Architectural'` sẽ **không parse được**
khi EF đọc lên. Thêm vào migration (hoặc chạy trước khi migrate):

```sql
UPDATE products SET placement = 'Desk' WHERE placement = 'Architectural';
```

Catalog demo hiện **không có** dòng nào dùng giá trị này, nhưng câu lệnh vô hại nếu chạy thừa.

⚠️ **Migration phải do `dotnet ef` sinh, đừng viết tay** — nó còn phải cập nhật `AppDbContextModelSnapshot.cs` (~4.700 dòng); lệch snapshot sẽ làm migration kế tiếp sinh sai.

**Không thêm cột nào vào `users`, `workspace_profiles`, `workspace_types`, `products`.**

`appsettings.Development.json` trỏ `Host=localhost;Port=5432;Database=sep490_fengdeskai_dev`:

```bash
dotnet ef migrations add PersonalizedRecommendationV31 \
  -p src/FengDeskAI.Infrastructure -s src/FengDeskAI.WebAPI
dotnet ef database update -p src/FengDeskAI.Infrastructure -s src/FengDeskAI.WebAPI
dotnet run --project src/FengDeskAI.WebAPI -- seed
# rồi chạy script SQL §6
```

---

## 11. Phụ lục — audit field chết (khảo sát 2026-08-25)

💀 dead (chỉ Entity + EF Config) · 🟠 CRUD-only · 🟡 có logic nhưng **không vào chấm điểm** · ✅ vào chấm điểm

### `User`

| Field | | Ghi chú |
| --- | :---: | --- |
| Email, PasswordHash, FullName, Phone, Role, IsActive, TokenVersion, GoogleId | ✅ | auth / admin |
| `DateOfBirth` | ✅ | cả hai luồng |
| `BirthTime` | ✅ | `PersonalTargetBuilder` (nhánh `Carry`) + `compute_destiny_chart` |
| `Gender` | 🟡 | → Kua → `FavorableDirections` → **chỉ gửi AI**, không vào điểm |
| `AuthProvider` | 🟠 | ghi lúc đăng ký, **không đọc lại** |
| **`Balance`** | 💀 | **DEAD** — chỉ `User.cs` + `UserConfiguration.cs`, không service nào đọc/ghi |

### `WorkspaceProfile`

| Field | | Ghi chú |
| --- | :---: | --- |
| UserId, Name, IsDefault, WorkspaceTypeId, WorkPurpose | ✅ | |
| `EntranceDirection`, `ToiletDirection`, `DarkDirections` | ⚠️ | Có trong `ViolatedDirections` **nhưng không có ô nhập ở form** (comment `WorkspaceProfileService:407`) ⇒ luôn null ⇒ **`dirPenalty` gần như không bao giờ kích hoạt** |
| **`FengShuiElement`** | 💀 | **DEAD** — 0 reference ngoài Entity + Config |
| `LocationType` | 🟠 | CRUD + intake parse |
| `StyleCode` | 🟠 | CRUD + gửi AI. **Không hề match với `product_styles`** khi chấm |
| `Lighting`, `DeskType`, `DeskOrientation`, `RoomFacingDirection`, `DeskArea` | 🟡 | chỉ completeness hint (`WorkspaceProfileService:399-410`) |

→ **9/16 field không tác động điểm.**

### `WorkspaceType`

| Field | | |
| --- | :---: | --- |
| Name, Description, IsSystemSeeded, **Scope** | ✅ | |
| `IsPublic` | 🟠 | thừa — trùng `Scope`; chỉ gửi AI contract |
| `PersonalWeight` | 🟠 | biến trong service tên `legacyWeight` |

### `Product`

| Field | | |
| --- | :---: | --- |
| GardenStoreId, Name, Description, IsActive, Images, Items, Elements, Vibes, `Placement`, `Element*`, IsVectorOverridden | ✅ | |
| `Styles` | 🟠 | `.Include(p => p.Styles)` trong `GetScorableCandidatesAsync` (`ProductRepository.cs:137`) rồi **không dùng** → v3.1 bỏ Include |
| `ProductItem.SizeClass` | 🟠 | Đã dời từ `Product` sang `ProductItem` (14/08). Nullable, **chưa tham gia chấm điểm** — chính XML comment cũng ghi vậy |

---

## 12. Không làm (out of scope / backlog)

| Hạng mục | Ghi chú |
| --- | --- |
| **Ô nhập `entrance/toilet/darkDirections` trên form** | **Rẻ nhất trong danh sách — chỉ DTO, 0 migration.** Mở khóa `dirPenalty` đang chết. Nên làm ngay sau v3.1 |
| **Engine đọc `Lighting`** cho `Living` | Cây ưa nắng trong phòng `Dim` = cây chết. Đã ghi trong `product-placement…` §7, vẫn còn nguyên |
| **Hướng bắt buộc cho vật trấn yểm** | Enum `Architectural` **đã bỏ 28/08** (gộp vào `Desk`). Làm lại thì thêm cột hướng bắt buộc trên `products` TRƯỚC, rồi mới thêm enum; xem `product-placement…` §7 |
| **Hướng bàn theo Bát Trạch vào điểm** | `DeskOrientation`/`RoomFacingDirection` mới chỉ là completeness hint. Cần chốt ngữ nghĩa hướng ngồi vs hướng nhìn trước |
| **`SeatingContext`** (lưng dựa tường, dưới xà ngang, quay lưng ra cửa) | Yếu tố "Thế" — nguồn sát khí lớn, chưa có mô hình |
| **`ZodiacConflict`** (xung tuổi linh vật) | Cần bảng tra 12 con giáp |
| **Kích thước vật phẩm vs mặt bàn** | `ProductItem.SizeClass` (mới dời sang, nullable) + `ProductItem.LengthCm/WidthCm` sẵn có vs `WorkspaceProfile.DeskArea`. Lưu ý: kích thước ở mức **SKU**, còn chấm điểm ở mức **Product** → phải chốt quy tắc gộp (lấy min? lấy biến thể rẻ nhất?) trước khi làm |
| **v4 Polarity (Âm/Dương)** | ARD riêng, tách PR |
| Dọn `User.Balance`, `WorkspaceProfile.FengShuiElement` | Dead columns — đợt housekeeping riêng |

---

## 13. Test cases tối thiểu

1. **Kill-switch:** seed `PERSONAL_WEIGHT_* = 0` → ranking **byte-identical** với trước v3.1 (golden set), kể cả mode Fit.
2. `Wp` = 0.50 / 0.30 / 0.00 đúng theo `Scope` = Private / Shared / Public.
3. `WorkspaceTypeId = null` → `Wp` = `PERSONAL_WEIGHT_PRIVATE`, không NPE.
4. `user.DateOfBirth = null` → `Wp` = 0 → `score == gapScore` như cũ.
5. `personalScore` của sản phẩm thuần hành khắc mệnh = **−1.0**; thuần tỷ hòa = **+1.0**.
6. `Wp > 0` + sản phẩm `BiKhac` + phòng `Private` → **vẫn nằm trong danh sách**, xếp cuối (D3).
7. `Wp > 0` → `userPenalty` **không** còn bị trừ ở nhánh workspace (chống tính hai lần).
8. `MIN_SCORE_THRESHOLD = 0.0` → sản phẩm khắc mệnh bị cắt; catalog toàn khắc mệnh vẫn **không** trả 422 khi threshold = −1.0.
9. Nhánh `Carry`: `Wp` **không** ảnh hưởng; điểm và thứ hạng giữ nguyên hoàn toàn.
10. `Consumable` vẫn không xuất hiện ở cả hai tool gợi ý, vẫn ra ở `search_products`.
11. `aspiration = Wealth` mà không sản phẩm nào có tag **đã duyệt** → bỏ filter + caution, **không** rỗng.
12. `ProductAspiration.IsApproved = false` → **không** được tính khi lọc.
13. Người sinh 2000-01-15 (trước Tết) → mệnh trong `PersonalProfile` **khớp** mệnh dùng trong `personalVector` (§7.1), cả 3 call site.
14. AI trả `finalRank` trùng nhau / ngoài `1..N` → fallback `BaseRank` + có log (§7.2).
15. Thiếu 3 row `scoring_params` mới → engine chạy default trong code, không lỗi.

---

## 14. Rollout

1. Migration + seed (`PERSONAL_WEIGHT_* = 0`) + script SQL §6 trên **DB local** `sep490_fengdeskai_dev`.
2. Golden set: ~10 sản phẩm × 3 phòng mẫu (Private / Shared / Public) × 2 user khác mệnh. Xác nhận **byte-identical** khi `Wp = 0` (test case 1).
3. Nâng `PERSONAL_WEIGHT_PRIVATE = 0.50`, `PERSONAL_WEIGHT_SHARED = 0.30` qua API `scoring-config`. Chấm tay lại golden set — hai user khác mệnh **phải** ra thứ hạng khác nhau.
4. Nếu ranking lệch ngoài ý muốn → hạ về `0.35 / 0.20`, hoặc `0` để tắt hẳn. Không cần deploy.
5. Đọc `recommendation_logs` stage `AiResponded` để quyết có khóa `FinalRank` hay không (§7.2).
6. Cập nhật `docs/api-documents/18-recommendations.md`, `25-scoring-config.md`, `02-products.md` theo quy tắc đồng bộ tài liệu (`AGENTS.md` §3).

---

## 15. Nợ tài liệu phát hiện kèm (không thuộc v3.1, nhưng phải trả)

`AGENTS.md` §3 yêu cầu docs đi cùng code. Ba thay đổi đã **Implemented** ngày 14–15/08 nhưng tài liệu tham chiếu **chưa được cập nhật**:

| File | Thiếu gì |
| --- | --- |
| `docs/api-documents/18-recommendations.md` | **Không có** `POST /api/recommendations/personal`; không có `recommendationKind`; `workspaceProfileId` giờ nullable; `personalWeight` đã bị bỏ khỏi payload tool |
| `docs/api-documents/25-scoring-config.md` | Thiếu **6 param**: `CARRY_PRIMARY_SHARE`, `CARRY_SECONDARY_SHARE`, `VIBE_MISMATCH_PENALTY`, `VIBE_UNKNOWN_PENALTY`, `VIBE_FILTER_HARD`, `MIN_SCORE_THRESHOLD` |
| `docs/api-documents/02-products.md` | **0 lần** nhắc `placement` dù vendor bắt buộc khai; `sizeClass` vẫn mô tả nằm trên `Product` |
| `docs/api-documents/99-appendix-models.md` | `sizeClass` sai vị trí (`Product` → `ProductItem`); thiếu `placement` |
| `docs/ard/bounded-contexts/customer-care.md` | **0 lần** nhắc `placement` — mô tả engine đã lỗi thời (vẫn là công thức v3 thuần) |
| `docs/ard/bounded-contexts/catalog.md` | `sizeClass` sai vị trí; thiếu `placement` |
| `docs/adr/recommendation-scoring-v3.md` | §4 mô tả `ScoreOne` chưa có `PlacementPolicy` lẫn vibe mềm — nên thêm dòng "đã được bổ sung bởi …" ở đầu file |
| **`docs/adr/product-item-size-class.md`** | ⚠️ **File KHÔNG tồn tại** nhưng đang được XML comment của `ProductItem.SizeClass` tham chiếu |

### Ngoài ra — nhiễu CRLF làm review không đọc được

Working tree hiện có **~200 file** hiện `M` chỉ vì đổi line-ending:

```
git diff HEAD --stat            OrderService.cs | 1437 ++++----
git diff HEAD --ignore-all-space --stat         OrderService.cs |   37 ++
```

Repo **không có `.gitattributes`**, file đang là CRLF. Nên thêm trước khi commit v3.1, nếu không diff của PR sẽ vô dụng:

```gitattributes
* text=auto eol=lf
*.cs text eol=lf
*.json text eol=lf
*.md text eol=lf
*.bat text eol=crlf
*.ps1 text eol=crlf
```

Sau đó `git add --renormalize .` trong **một commit riêng**, tách khỏi commit nghiệp vụ.
