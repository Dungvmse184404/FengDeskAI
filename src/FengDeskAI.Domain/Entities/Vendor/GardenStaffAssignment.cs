using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Vendor;

namespace FengDeskAI.Domain.Entities.Vendor;

/// <summary>
/// Phân công nhân viên cho garden store. Giờ vừa là LỜI MỜI vừa là MEMBERSHIP qua <see cref="Status"/>:
/// Pending = đã mời chưa đồng ý; Accepted = đang là nhân viên; Rejected = bị từ chối; Revoked = owner đã gỡ / huỷ lời mời.
/// Quyền store-scoped chỉ tính khi <c>Status == Accepted</c>.
/// </summary>
public class GardenStaffAssignment : BaseEntity
{
    public Guid GardenStoreId { get; set; }
    /// <summary>Người được mời / là nhân viên.</summary>
    public Guid StaffId { get; set; }
    /// <summary>Owner đã mời (đổi tên từ AssignedBy).</summary>
    public Guid InvitedBy { get; set; }

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    /// <summary>Lúc owner mời (đổi tên từ AssignedAt).</summary>
    public DateTime InvitedAt { get; set; }
    /// <summary>Lúc staff accept/reject.</summary>
    public DateTime? RespondedAt { get; set; }
    /// <summary>Lúc owner gỡ (Status chuyển sang Revoked).</summary>
    public DateTime? UnassignedAt { get; set; }

    public GardenStore Store { get; set; } = null!;
}
