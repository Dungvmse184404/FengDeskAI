using FengDeskAI.Domain.Entities.Identity;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IRefreshTokenRepository : IGenericRepository<RefreshToken>
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<List<RefreshToken>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task RevokeAllActiveForUserAsync(Guid userId, CancellationToken ct = default);
}
