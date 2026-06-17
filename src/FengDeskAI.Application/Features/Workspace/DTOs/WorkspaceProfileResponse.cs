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
    public LightingType Lighting { get; set; }
    public DeskType DeskType { get; set; }
    public CompassDirection DeskOrientation { get; set; }
    public CompassDirection RoomFacingDirection { get; set; }
    public WorkPurpose WorkPurpose { get; set; }
    public FengShuiElement FengShuiElement { get; set; }
    public int DeskArea { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
