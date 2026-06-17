using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.Workspace.DTOs;

public class UpdateWorkspaceProfileRequest
{
    public string Name { get; set; } = null!;
    public LocationType LocationType { get; set; }
    public Guid? WorkspaceTypeId { get; set; }
    /// <summary>Mã phong cách (styles.code), vd "Minimal".</summary>
    public string StyleCode { get; set; } = null!;
    public LightingType Lighting { get; set; }
    public DeskType DeskType { get; set; }
    public CompassDirection DeskOrientation { get; set; }
    public CompassDirection RoomFacingDirection { get; set; }
    public WorkPurpose WorkPurpose { get; set; }
    public FengShuiElement FengShuiElement { get; set; }
    public int DeskArea { get; set; }
}
