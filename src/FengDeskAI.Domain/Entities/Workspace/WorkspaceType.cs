using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Domain.Entities.Workspace;

/// <summary>
/// Loại không gian làm việc (vd "Personal Desk", "Meeting Room"). Quyết định mức độ
/// ảnh hưởng của yếu tố cá nhân (mệnh, hướng) tới gợi ý qua <see cref="PersonalWeight"/>.
/// Có loại seed sẵn (<see cref="IsSystemSeeded"/>) và loại do khách tự thêm —
/// loại tự thêm mặc định <see cref="PersonalWeight"/> = 1.0 (xem business rule recommendation).
/// </summary>
public class WorkspaceType : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>
    /// LEGACY — trùng vai trò với <see cref="Scope"/> (enum 3 bậc). Giữ cột cho dữ liệu &amp; DTO cũ,
    /// KHÔNG dùng cho logic mới. Engine chỉ đọc <see cref="Scope"/>.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// LEGACY (engine v2) — không code nào đọc để chấm điểm; chỉ được ghi vào
    /// <c>recommendations.personal_weight</c> cho dữ liệu cũ. Trọng số cá nhân của engine v3.1 lấy từ
    /// <c>scoring_params</c> (<c>PERSONAL_WEIGHT_PRIVATE/SHARED/PUBLIC</c>) theo <see cref="Scope"/>.
    /// Xem <c>docs/adr/personalized-recommendation-v3.1.md</c> §3.2.
    /// </summary>
    public decimal PersonalWeight { get; set; } = 1.0m;

    /// <summary>
    /// Mức riêng tư (engine v3): quyết định bộ lọc mệnh là hard (Private) hay soft (Shared/Public).
    /// Thay vai trò của <see cref="PersonalWeight"/> ở công thức mới. Mặc định Private.
    /// </summary>
    public WorkspaceScope Scope { get; set; } = WorkspaceScope.Private;

    /// <summary>True nếu là loại hệ thống seed sẵn (không cho user sửa/xóa).</summary>
    public bool IsSystemSeeded { get; set; }
}
