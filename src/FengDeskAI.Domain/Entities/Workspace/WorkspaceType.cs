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

    /// <summary>Không gian dùng chung (phòng họp, khu co-working) hay riêng tư (bàn cá nhân).</summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Hệ số nhân áp lên phần điểm <b>cá nhân</b> (mệnh + hướng). 1.0 cho không gian riêng,
    /// 0.5 cho không gian công cộng. Không ảnh hưởng phần điểm chức năng (mục đích, ánh sáng, kích thước).
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
