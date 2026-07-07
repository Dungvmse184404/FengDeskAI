using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Domain.Entities.Recommendation;

/// <summary>
/// Trọng số ngũ hành theo loại không gian. Mỗi (type, source) là 1 bộ 5 hành, Σ≈1.
/// <c>Ideal</c> = vector lý tưởng cần đạt; <c>Interior</c> = hiện trạng nội thất mặc định
/// (fallback khi user không khai màu/vật liệu). Seed sẵn, admin sửa.
/// </summary>
public class WorkspaceTypeElement : BaseEntity
{
    public Guid WorkspaceTypeId { get; set; }

    /// <summary>"Ideal" | "Interior".</summary>
    public string Source { get; set; } = null!;

    public FengShuiElement Element { get; set; }

    /// <summary>Trọng số hành trong bộ (numeric(4,3)).</summary>
    public decimal Weight { get; set; }
}
