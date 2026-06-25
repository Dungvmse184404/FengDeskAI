using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Vendor;

/// <summary>
/// Cửa hàng/nhà vườn bán sản phẩm phong thủy (multi-vendor).
/// Quyền sở hữu qua bảng nối <see cref="GardenStoreOwner"/> (nhiều owner/store);
/// nhân viên qua <see cref="GardenStaffAssignment"/>.
/// </summary>
public class GardenStore : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Hotline { get; set; } = null!;
    public string? OpeningHours { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Dịch vụ AhaMove mặc định cho điểm lấy hàng của store (vd "SGN-BIKE" — tiền tố mã thành phố). Null nếu chưa cấu hình.</summary>
    public string? AhamoveServiceId { get; set; }

    public StoreAddress? Address { get; set; }
    public ICollection<GardenStoreOwner> Owners { get; set; } = new List<GardenStoreOwner>();
    public ICollection<GardenStaffAssignment> StaffAssignments { get; set; } = new List<GardenStaffAssignment>();
}
