using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class WorkspaceTypeRepository : GenericRepository<WorkspaceType>, IWorkspaceTypeRepository
{
    public WorkspaceTypeRepository(AppDbContext context) : base(context) { }

    public Task<List<WorkspaceType>> GetAvailableForUserAsync(Guid userId, CancellationToken ct = default)
        => _set.AsNoTracking()
            .Where(t => t.IsSystemSeeded || t.CreatedBy == userId)
            .OrderByDescending(t => t.IsSystemSeeded)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);

    public Task<bool> IsAvailableToUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => _set.AnyAsync(t => t.Id == id && (t.IsSystemSeeded || t.CreatedBy == userId), ct);
}
