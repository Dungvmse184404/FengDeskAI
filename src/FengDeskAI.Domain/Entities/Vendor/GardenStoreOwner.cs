using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Vendor;

/// <summary>
/// Quan hệ sở hữu nhiều-nhiều giữa garden store và user (owner).
/// Một owner có thể sở hữu nhiều store; một store có thể có nhiều owner.
/// <see cref="IsPrimary"/> = owner đã tạo store (đúng 1 dòng/store) — không được gỡ owner primary cuối cùng.
/// Khác với <see cref="GardenStaffAssignment"/> (nhân viên): owner có toàn quyền quản lý store.
/// </summary>
public class GardenStoreOwner : BaseEntity
{
    public Guid GardenStoreId { get; set; }
    public Guid OwnerUserId { get; set; }

    /// <summary>True cho owner đã tạo store (owner chính).</summary>
    public bool IsPrimary { get; set; }
    public DateTime AssignedAt { get; set; }

    public GardenStore Store { get; set; } = null!;
}
