using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Domain.Entities.Workspace;

/// <summary>
/// Hồ sơ không gian làm việc của user — input cho AI recommendation.
/// Mỗi user có thể có nhiều profile (vd: nhà + công ty), nhưng chỉ 1 profile mặc định.
/// </summary>
public class WorkspaceProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = null!;

    public LocationType LocationType { get; set; }

    /// <summary>
    /// Loại không gian (Personal Desk, Meeting Room...) — quyết định trọng số cá nhân khi gợi ý.
    /// Nullable để tương thích dữ liệu cũ; khi null engine coi như không gian riêng (weight 1.0).
    /// </summary>
    public Guid? WorkspaceTypeId { get; set; }

    /// <summary>Mã phong cách (FK <c>styles.code</c>) — vd "Minimal".</summary>
    public string StyleCode { get; set; } = null!;
    public LightingType Lighting { get; set; }
    public DeskType DeskType { get; set; }
    public CompassDirection DeskOrientation { get; set; }
    public CompassDirection RoomFacingDirection { get; set; }
    public WorkPurpose WorkPurpose { get; set; }
    public FengShuiElement FengShuiElement { get; set; }

    /// <summary>Diện tích mặt bàn (cm²).</summary>
    public int DeskArea { get; set; }

    public bool IsDefault { get; set; }

    public User User { get; set; } = null!;
    public WorkspaceType? WorkspaceType { get; set; }
}
