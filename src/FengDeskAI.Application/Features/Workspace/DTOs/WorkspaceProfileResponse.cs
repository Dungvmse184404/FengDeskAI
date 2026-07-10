using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.Workspace.DTOs;

public class WorkspaceProfileResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = null!;
    public Guid? WorkspaceTypeId { get; set; }
    public LocationType LocationType { get; set; }
    public string StyleCode { get; set; } = null!;
    public LightingType? Lighting { get; set; }
    public DeskType? DeskType { get; set; }
    public CompassDirection? DeskOrientation { get; set; }
    public CompassDirection? RoomFacingDirection { get; set; }
    public WorkPurpose WorkPurpose { get; set; }
    /// <summary>Mệnh nhập tay (legacy) — chỉ còn ở dữ liệu cũ, không còn nhập mới.</summary>
    public FengShuiElement? FengShuiElement { get; set; }
    public int? DeskArea { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>% hồ sơ đã điền (fields optional có giá trị / tổng) — FE hiện progress.</summary>
    public int CompletenessPercent { get; set; }
    /// <summary>Gợi ý field nên bổ sung + lợi ích, vd "Thêm hướng cửa để nhận gợi ý vị trí đặt".</summary>
    public IReadOnlyList<string> MissingFieldHints { get; set; } = Array.Empty<string>();
}
