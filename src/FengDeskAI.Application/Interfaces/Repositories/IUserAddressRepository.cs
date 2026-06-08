using FengDeskAI.Domain.Entities.Geography;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IUserAddressRepository : IGenericRepository<UserAddress>
{
    Task<List<UserAddress>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<UserAddress?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<UserAddress?> GetDefaultForUserAsync(Guid userId, CancellationToken ct = default);
    Task ClearDefaultsForUserAsync(Guid userId, CancellationToken ct = default);
}
