using FengDeskAI.Domain.Entities.Vendor;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IStoreRepository : IGenericRepository<GardenStore>
{
    Task<List<GardenStore>> GetActiveAsync(CancellationToken ct = default);
    Task<GardenStore?> GetDetailAsync(Guid id, CancellationToken ct = default);

    /// <summary>True nếu user là owner hoặc nhân viên đang active của store.</summary>
    Task<bool> CanManageAsync(Guid storeId, Guid userId, CancellationToken ct = default);

    Task<List<GardenStaffAssignment>> GetStaffAsync(Guid storeId, CancellationToken ct = default);
    Task<GardenStaffAssignment?> GetActiveAssignmentAsync(Guid storeId, Guid staffId, CancellationToken ct = default);
    Task<GardenStaffAssignment?> GetAssignmentByIdAsync(Guid assignmentId, Guid storeId, CancellationToken ct = default);
    Task AddAssignmentAsync(GardenStaffAssignment assignment, CancellationToken ct = default);

    Task<StoreAddress?> GetAddressAsync(Guid storeId, CancellationToken ct = default);
    Task AddAddressAsync(StoreAddress address, CancellationToken ct = default);
}
