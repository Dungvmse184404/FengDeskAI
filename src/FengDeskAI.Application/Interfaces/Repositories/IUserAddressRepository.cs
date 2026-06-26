using FengDeskAI.Domain.Entities.Geography;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IUserAddressRepository : IGenericRepository<UserAddress>
{
    Task<List<UserAddress>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<UserAddress?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<UserAddress?> GetDefaultForUserAsync(Guid userId, CancellationToken ct = default);
    Task ClearDefaultsForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Địa chỉ kèm chuỗi phường/quận/tỉnh (read-only) để dựng điểm giao cho nhà vận chuyển.
    /// </summary>
    Task<UserAddress?> GetWithWardChainAsync(Guid id, CancellationToken ct = default);
}
