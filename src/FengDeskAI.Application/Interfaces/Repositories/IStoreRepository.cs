using FengDeskAI.Domain.Entities.Vendor;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IStoreRepository : IGenericRepository<GardenStore>
{
    Task<List<GardenStore>> GetActiveAsync(CancellationToken ct = default);
    Task<GardenStore?> GetDetailAsync(Guid id, CancellationToken ct = default);

    /// <summary>True nếu user là owner hoặc nhân viên đang active của store.</summary>
    Task<bool> CanManageAsync(Guid storeId, Guid userId, CancellationToken ct = default);

    // ===== Owner (quan hệ nhiều-nhiều garden_store_owners) =====
    /// <summary>True nếu user là owner của store.</summary>
    Task<bool> IsOwnerAsync(Guid storeId, Guid userId, CancellationToken ct = default);
    /// <summary>Các store mà user đồng sở hữu (kèm Address/Owners) — cho kênh người bán.</summary>
    Task<List<GardenStore>> GetByOwnerAsync(Guid ownerUserId, CancellationToken ct = default);
    Task<List<GardenStoreOwner>> GetOwnersAsync(Guid storeId, CancellationToken ct = default);
    /// <summary>Owner record (tracked) để cập nhật/gỡ.</summary>
    Task<GardenStoreOwner?> GetOwnerAsync(Guid storeId, Guid userId, CancellationToken ct = default);
    Task<int> CountOwnersAsync(Guid storeId, CancellationToken ct = default);
    Task AddOwnerAsync(GardenStoreOwner owner, CancellationToken ct = default);

    Task<List<GardenStaffAssignment>> GetStaffAsync(Guid storeId, CancellationToken ct = default);
    Task<GardenStaffAssignment?> GetActiveAssignmentAsync(Guid storeId, Guid staffId, CancellationToken ct = default);
    Task<GardenStaffAssignment?> GetAssignmentByIdAsync(Guid assignmentId, Guid storeId, CancellationToken ct = default);
    Task AddAssignmentAsync(GardenStaffAssignment assignment, CancellationToken ct = default);

    /// <summary>True nếu store tồn tại (kể cả đã soft-delete).</summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Các store kèm địa chỉ + chuỗi phường/quận/tỉnh (read-only) để tạo vận đơn.
    /// Dùng khi gom dữ liệu điểm lấy hàng cho nhà vận chuyển (vd AhaMove).
    /// </summary>
    Task<List<GardenStore>> GetWithAddressByIdsAsync(IEnumerable<Guid> storeIds, CancellationToken ct = default);

    /// <summary>Địa chỉ active của store (đã lọc soft-delete), tracked để cập nhật.</summary>
    Task<StoreAddress?> GetAddressAsync(Guid storeId, CancellationToken ct = default);
    /// <summary>Địa chỉ của store kể cả đã soft-delete (để hồi sinh khi Add lại — StoreId là unique).</summary>
    Task<StoreAddress?> GetAddressIncludingDeletedAsync(Guid storeId, CancellationToken ct = default);
    /// <summary>True nếu có bản ghi địa chỉ cho store (kể cả đã soft-delete).</summary>
    Task<bool> AddressExistsAsync(Guid storeId, CancellationToken ct = default);
    Task AddAddressAsync(StoreAddress address, CancellationToken ct = default);

    /// <summary>Xóa vật lý store + địa chỉ + phân công nhân viên (bypass soft-delete). Nên gọi trong transaction.</summary>
    Task HardDeleteAsync(Guid id, CancellationToken ct = default);
    /// <summary>Xóa vật lý địa chỉ của store (bypass soft-delete).</summary>
    Task HardDeleteAddressAsync(Guid storeId, CancellationToken ct = default);
}
