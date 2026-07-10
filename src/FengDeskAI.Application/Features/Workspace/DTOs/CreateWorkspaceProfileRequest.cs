using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.Workspace.DTOs;

public class CreateWorkspaceProfileRequest
{
    public string Name { get; set; } = null!;
    public LocationType LocationType { get; set; }

    /// <summary>Loại không gian (Personal Desk, Meeting Room...). Bỏ trống → coi như riêng tư (weight 1.0).</summary>
    public Guid? WorkspaceTypeId { get; set; }

    /// <summary>Mã phong cách (styles.code), vd "Minimal".</summary>
    public string StyleCode { get; set; } = null!;
    public LightingType? Lighting { get; set; }
    /// <summary>Null = không gian không có bàn làm việc.</summary>
    public DeskType? DeskType { get; set; }
    public CompassDirection? DeskOrientation { get; set; }
    public CompassDirection? RoomFacingDirection { get; set; }
    public WorkPurpose WorkPurpose { get; set; }
    public int? DeskArea { get; set; }
    public bool IsDefault { get; set; }

    /// <summary>Màu/vật liệu/hình khối thực tế của phòng (vd từ AI intake). Bỏ trống → không đổi/không lưu.</summary>
    public List<WorkspaceProfileInputDto>? Inputs { get; set; }
}
