using FengDeskAI.Domain.Entities.Identity;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IAuthorizationAuditRepository
{
    Task AddAsync(AuthorizationAuditLog log, CancellationToken ct = default);
    Task<(List<AuthorizationAuditLog> Items, int Total)> GetByResourceAsync(
        string resourceType, Guid resourceId, int skip, int take, CancellationToken ct = default);
}
