# ARD — Refactor: Workspace Input Relaxation

> **Status:** Proposal. **PR riêng, làm TRƯỚC** `feature-workspace-ai-intake.md` và scoring v4.
> **Vấn đề:** form workspace bắt buộc nhiều field user không biết/không có (hướng bàn, diện tích bàn, phòng không có bàn, hành nhập tay) — trong khi engine v3 không dùng chúng để chấm điểm (audit: `RoomFacingDirection` không dùng ở đâu; `DeskOrientation/DeskArea/Lighting` chỉ vào payload AI diễn giải; `FengShuiElement` legacy).

## Nguyên tắc

**Không hỏi thứ user không biết.** Mọi field phong thủy optional; engine degrade từng bậc (pattern đã có: thiếu DOB → bỏ lọc mệnh, thiếu inputs → fallback Interior, thiếu hướng chắn → không phạt). User điền bổ sung sau — càng điền gợi ý càng tốt.

## 1. Domain — `Entities/Workspace/WorkspaceProfile.cs`

```csharp
public LightingType? Lighting { get; set; }              // was: LightingType
public DeskType? DeskType { get; set; }                  // was: DeskType   (null = không có bàn)
public CompassDirection? DeskOrientation { get; set; }   // was: CompassDirection
public CompassDirection? RoomFacingDirection { get; set; }
public FengShuiElement? FengShuiElement { get; set; }    // legacy, giữ cột cho data cũ
public int? DeskArea { get; set; }                       // was: int
```

Giữ nguyên required: `Name`, `LocationType`, `StyleCode`, `WorkPurpose`, `IsDefault`. (`WorkspaceTypeId` vốn đã nullable.)

## 2. Application

- `DTOs/CreateWorkspaceProfileRequest.cs` + `UpdateWorkspaceProfileRequest.cs`:
  - **Xóa hẳn `FengShuiElement`** — user không bao giờ chọn hành; engine luôn tự tính. (Breaking change FE — sửa cùng PR.)
  - `Lighting`, `DeskType`, `DeskOrientation`, `RoomFacingDirection`, `DeskArea` → nullable.
- `Services/WorkspaceProfileService.cs`: validation `DeskArea <= 0` → chỉ check khi có giá trị (`request.DeskArea is <= 0`). Bỏ mọi validate cho field đã xóa.
- `WorkspaceProfileResponse` + `Mappings/`: nullable tương ứng. Thêm field mới:

```csharp
/// <summary>% hồ sơ đã điền (fields optional có giá trị / tổng) — FE hiện progress.</summary>
public int CompletenessPercent { get; init; }
/// <summary>Gợi ý field nên bổ sung + lợi ích, vd "Thêm hướng cửa để nhận gợi ý vị trí đặt".</summary>
public IReadOnlyList<string> MissingFieldHints { get; init; }
```

Tính trong service (không lưu DB). Bộ đếm: `Lighting, DeskType, DeskOrientation, RoomFacingDirection, DeskArea, EntranceDirection, ToiletDirection, WorkspaceTypeId, có ≥1 profile input`.

- `RecommendationService.cs` (~dòng 352): payload AI diễn giải đổi sang `profile.Lighting?.ToString()`… — null thì bỏ key khỏi payload (đừng gửi chuỗi "null" cho LLM).

## 3. Infrastructure

- `WorkspaceProfileConfiguration.cs`: bỏ `IsRequired()` các cột trên (enum string giữ `HasConversion<string>()`, cột nullable).
- Migration `WorkspaceInputRelaxation`: `ALTER COLUMN ... DROP NOT NULL` × 6. **Không đổi data** — giá trị cũ giữ nguyên, chỉ nới constraint. Không cần backfill.

## 4. FE (`FengDeskAI_FE`)

- `features/users/types/workspace.d.ts`: các field trên `?`, xóa `fengShuiElement` khỏi request types.
- `schemas/`: zod bỏ `.required()` tương ứng (`.optional().nullable()`), xóa fengShuiElement.
- `WorkspaceModal.tsx`:
  - Xóa dropdown chọn hành (nếu có).
  - Field optional gom vào section "Thông tin bổ sung (không bắt buộc)" — collapse mặc định.
  - Checkbox "Không gian này không có bàn làm việc" → disable + null 3 field desk.
  - Hướng: kèm hint "Mở la bàn trên điện thoại, đứng quay mặt theo hướng nhìn của bàn/cửa".
- `ProfileWorkspace.tsx`: hiện `completenessPercent` (progress ring) + `missingFieldHints`.

## 5. Test

1. Create chỉ với `Name + LocationType + StyleCode + WorkPurpose` → 200, recommendation chạy được (fallback đủ).
2. `DeskArea = 0` gửi lên → 400; `DeskArea = null` → 200.
3. Profile cũ (đủ data) đọc/ghi bình thường sau migration.
4. Recommendation trên profile tối thiểu: không có direction penalty, AI payload không chứa key null.
5. Request chứa `fengShuiElement` (FE cũ) → bị ignore (không 400) — thêm `[JsonIgnore]`-tolerant hoặc để model binding bỏ qua.

## Out of scope

AI intake (ARD riêng), polarity v4, đổi engine.
