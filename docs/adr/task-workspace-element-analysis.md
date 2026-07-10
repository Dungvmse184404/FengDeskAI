# TASK (for Claude Code) — Workspace Element Analysis endpoint

> Mục tiêu: expose **vector ngũ hành của một workspace** (ideal / adjustedIdeal / current / gap)
> qua endpoint nhẹ, KHÔNG cần chạy cả phiên recommendation — để FE hiển thị "phòng của bạn đang thiếu/thừa hành gì".

## Bối cảnh (đọc trước khi code)

Logic dựng vector **đã có sẵn** và thuần (không cần viết lại):
- `WorkspaceVectorBuilder.BuildIdeal / ApplyIntent / BuildCurrent` — `src/FengDeskAI.Application/Features/CustomerCare/Engine/ElementVectorBuilders.cs`
- `ElementVector` (có `.Subtract`, `.Enumerate`, `.Dominant`) — cùng thư mục.

Phần cần tách nằm **inline** trong `RecommendationService.GenerateAsync` (dòng ~55–83),
file `src/FengDeskAI.Application/Features/CustomerCare/Services/RecommendationService.cs`:

```csharp
var p = ScoringParameters.FromRows(await _uow.ScoringConfig.GetScoringParamsAsync(ct));
// typeElements theo profile.WorkspaceTypeId
var resolver = new ElementInputResolver(await _uow.ScoringConfig.GetElementInputMapAsync(ct));
var ideal = WorkspaceVectorBuilder.BuildIdeal(typeElements);
var modifiers = await _uow.ScoringConfig.GetWorkPurposeModifiersAsync(profile.WorkPurpose, ct);
var adjustedIdeal = WorkspaceVectorBuilder.ApplyIntent(ideal, modifiers);
var profileInputs = await _uow.ScoringConfig.GetWorkspaceProfileInputsAsync(profile.Id, ct);
var currentVector = WorkspaceVectorBuilder.BuildCurrent(profileInputs, resolver, typeElements);
// gap = adjustedIdeal.Subtract(currentVector)   // hiện tính trong RecommendationScorer
```

Repo dùng lại (đã tồn tại, KHÔNG thêm bảng): `_uow.ScoringConfig.*`, `_uow.WorkspaceProfiles.GetByIdForUserAsync`, `_uow.WorkspaceTypes.GetByIdAsync`.

## Việc cần làm

### 1. Tách orchestration ra method tái dùng
Tạo `WorkspaceElementAnalyzer` (static hoặc service) trả về 4 vector từ 1 `WorkspaceProfile`:

```csharp
public sealed record WorkspaceElementAnalysis(
    ElementVector Ideal, ElementVector AdjustedIdeal,
    ElementVector Current, ElementVector Gap);
```

Gợi ý đặt: `Features/CustomerCare/Engine/` (thuần) + 1 helper trong service để nạp data.
Method: `Task<WorkspaceElementAnalysis> AnalyzeAsync(WorkspaceProfile profile, CancellationToken ct)`
— copy đúng logic dòng 55–83, `Gap = AdjustedIdeal.Subtract(Current)`.

### 2. Refactor `GenerateAsync`
Thay đoạn inline bằng lời gọi method mới. Hành vi chấm điểm phải **không đổi** (gap vẫn như cũ).

### 3. DTO response
`src/FengDeskAI.Application/Features/CustomerCare/DTOs/` — thêm:

```csharp
public sealed record WorkspaceElementAnalysisResponse
{
    public Guid WorkspaceProfileId { get; init; }
    public string DominantNeed { get; init; } = null!;   // hành gap dương lớn nhất
    public List<ElementAnalysisRow> Elements { get; init; } = new();
}
public sealed record ElementAnalysisRow
{
    public string Element { get; init; } = null!;        // Kim/Moc/Thuy/Hoa/Tho
    public decimal Ideal { get; init; }
    public decimal AdjustedIdeal { get; init; }
    public decimal Current { get; init; }
    public decimal Gap { get; init; }                    // + thiếu, − thừa
}
```

### 4. Service + endpoint
- Interface: `IWorkspaceProfileService` (hoặc service recommendation) thêm
  `Task<IServiceResult<WorkspaceElementAnalysisResponse>> GetElementAnalysisAsync(Guid profileId, Guid userId, CancellationToken ct)`.
  Xác thực profile thuộc user (dùng `GetByIdForUserAsync`), NotFound nếu không có.
- Controller `WorkspaceController`:
  ```csharp
  [HttpGet("{id:guid}/element-analysis")]
  public async Task<IActionResult> GetElementAnalysis(Guid id, CancellationToken ct)
      => ToActionResult(await _service.GetElementAnalysisAsync(id, CurrentUserId, ct));
  ```
  Route đầy đủ: `GET /api/workspace/{id}/element-analysis` · `[Authorize]`.

### 5. Test
- Unit: cùng input → `Gap == AdjustedIdeal − Current`; workspace không có `WorkspaceTypeId` → ideal rỗng, không throw.
- Regression: điểm recommendation trước/sau refactor giống nhau.

### 6. Doc
Cập nhật `docs/api-documents/16-workspace-profiles.md`: thêm endpoint `GET /{id}/element-analysis` + ví dụ response.

## Ràng buộc
- **Không** thêm bảng/migration. **Không** đổi engine chấm điểm.
- Định danh code tiếng Anh; comment tiếng Việt được.
- Giữ convention hiện có (`ServiceResult`, `ApiControllerBase`, `_uow`).

## Response mẫu (để FE và test đối chiếu)
```json
{
  "workspaceProfileId": "guid",
  "dominantNeed": "Thuy",
  "elements": [
    { "element": "Thuy", "ideal": 0.20, "adjustedIdeal": 0.30, "current": 0.00, "gap":  0.30 },
    { "element": "Moc",  "ideal": 0.25, "adjustedIdeal": 0.25, "current": 0.24, "gap":  0.01 },
    { "element": "Kim",  "ideal": 0.15, "adjustedIdeal": 0.10, "current": 0.36, "gap": -0.26 }
  ]
}
```

## Ngoài phạm vi (task khác)
- Component FE hiển thị vector (đặt ở trang Workspace + WorkspaceSwitcher).
- Endpoint "fit 1 sản phẩm × 1 workspace" cho trang chi tiết sản phẩm.
- Lộ `scope` / hướng cửa-WC / màu-vật liệu ra API workspace (xem [recommendation-scoring-v3.md](./recommendation-scoring-v3.md) mục 7).
