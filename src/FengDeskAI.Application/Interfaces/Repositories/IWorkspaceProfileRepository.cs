using FengDeskAI.Domain.Entities.Workspace;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IWorkspaceProfileRepository : IGenericRepository<WorkspaceProfile>
{
    Task<WorkspaceProfile?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<List<WorkspaceProfile>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<WorkspaceProfile?> GetDefaultByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task ClearDefaultsForUserAsync(Guid userId, CancellationToken ct = default);
}
