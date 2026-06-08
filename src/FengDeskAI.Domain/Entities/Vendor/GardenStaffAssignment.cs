using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Vendor;

/// <summary>
/// Phân công một user làm nhân viên cho một garden store.
/// Thuần ghi nhận "ai làm việc ở store nào, do ai phân công" — không có cấp bậc/role riêng.
/// </summary>
public class GardenStaffAssignment : BaseEntity
{
    public Guid GardenStoreId { get; set; }
    public Guid StaffId { get; set; }
    public Guid AssignedBy { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime AssignedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }

    public GardenStore Store { get; set; } = null!;
}
