using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Domain.Entities.Recommendation;

/// <summary>
/// Màu / vật liệu / hình khối thực tế user khai cho phòng — nguồn để dựng <c>currentVector</c>.
/// Optional: trống → engine fallback vector Interior mặc định theo loại phòng.
/// </summary>
public class WorkspaceProfileInput : BaseEntity
{
    public Guid WorkspaceProfileId { get; set; }
    public ElementInputKind InputKind { get; set; }
    public string InputCode { get; set; } = null!;
}
