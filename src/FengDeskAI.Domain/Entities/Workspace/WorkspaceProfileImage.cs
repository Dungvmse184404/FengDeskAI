using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Workspace;

/// <summary>
/// Ảnh chụp không gian của một <see cref="WorkspaceProfile"/> — FE dùng làm nền phần tổng quan (trình chiếu
/// khi có nhiều ảnh). Không tham gia chấm điểm.
/// </summary>
public class WorkspaceProfileImage : BaseEntity
{
    public Guid WorkspaceProfileId { get; set; }

    /// <summary>URL công khai trên storage (<c>Workspace_images/{userId}/{workspaceId}/…</c>).</summary>
    public string Url { get; set; } = null!;

    /// <summary>Thứ tự trình chiếu — ảnh thêm sau đứng sau.</summary>
    public int SortOrder { get; set; }

    public WorkspaceProfile WorkspaceProfile { get; set; } = null!;
}
