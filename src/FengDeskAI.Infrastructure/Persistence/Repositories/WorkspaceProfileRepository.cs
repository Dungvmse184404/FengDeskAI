using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class WorkspaceProfileRepository : GenericRepository<WorkspaceProfile>, IWorkspaceProfileRepository
{
    public WorkspaceProfileRepository(AppDbContext context) : base(context) { }

    public Task<WorkspaceProfile?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => _set.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct);

    public Task<List<WorkspaceProfile>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _set.Where(w => w.UserId == userId)
               .OrderByDescending(w => w.IsDefault)
               .ThenByDescending(w => w.UpdatedAt)
               .ToListAsync(ct);

    public Task<WorkspaceProfile?> GetDefaultByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _set.FirstOrDefaultAsync(w => w.UserId == userId && w.IsDefault, ct);

    public Task ClearDefaultsForUserAsync(Guid userId, CancellationToken ct = default)
        => _set.Where(w => w.UserId == userId && w.IsDefault)
               .ExecuteUpdateAsync(s => s.SetProperty(w => w.IsDefault, false), ct);
}
