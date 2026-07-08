using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class ScoringConfigRepository : IScoringConfigRepository
{
    private readonly AppDbContext _context;

    public ScoringConfigRepository(AppDbContext context) => _context = context;

    public Task<List<ScoringParam>> GetScoringParamsAsync(CancellationToken ct = default)
        => _context.Set<ScoringParam>().AsNoTracking().ToListAsync(ct);

    public Task<List<ElementInputMap>> GetElementInputMapAsync(CancellationToken ct = default)
        => _context.Set<ElementInputMap>().AsNoTracking().ToListAsync(ct);

    public Task<List<WorkspaceTypeElement>> GetWorkspaceTypeElementsAsync(Guid workspaceTypeId, CancellationToken ct = default)
        => _context.Set<WorkspaceTypeElement>().AsNoTracking()
            .Where(e => e.WorkspaceTypeId == workspaceTypeId).ToListAsync(ct);

    public Task<List<WorkPurposeElementModifier>> GetWorkPurposeModifiersAsync(WorkPurpose purpose, CancellationToken ct = default)
        => _context.Set<WorkPurposeElementModifier>().AsNoTracking()
            .Where(m => m.WorkPurpose == purpose).ToListAsync(ct);

    public Task<List<WorkspaceProfileInput>> GetWorkspaceProfileInputsAsync(Guid workspaceProfileId, CancellationToken ct = default)
        => _context.Set<WorkspaceProfileInput>().AsNoTracking()
            .Where(i => i.WorkspaceProfileId == workspaceProfileId).ToListAsync(ct);

    public Task<List<ProductElementInput>> GetProductElementInputsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
        => _context.Set<ProductElementInput>().AsNoTracking()
            .Where(i => productIds.Contains(i.ProductId)).ToListAsync(ct);

    public async Task ReplaceProductElementInputsAsync(Guid productId, IEnumerable<ProductElementInput> inputs, CancellationToken ct = default)
    {
        var set = _context.Set<ProductElementInput>();
        var existing = await set.Where(i => i.ProductId == productId).ToListAsync(ct);
        if (existing.Count > 0) set.RemoveRange(existing); // soft-delete qua SaveChanges

        foreach (var input in inputs)
        {
            input.ProductId = productId;
            await set.AddAsync(input, ct);
        }
    }
}
