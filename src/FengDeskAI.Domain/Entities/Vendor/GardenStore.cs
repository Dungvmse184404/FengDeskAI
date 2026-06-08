using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Vendor;

/// <summary>
/// Cửa hàng/nhà vườn bán sản phẩm phong thủy (multi-vendor).
/// Một store có một owner (user) và nhiều nhân viên được phân công.
/// </summary>
public class GardenStore : BaseEntity
{
    public Guid OwnerUserId { get; set; }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Hotline { get; set; } = null!;
    public string? OpeningHours { get; set; }
    public bool IsActive { get; set; } = true;

    public StoreAddress? Address { get; set; }
    public ICollection<GardenStaffAssignment> StaffAssignments { get; set; } = new List<GardenStaffAssignment>();
}
