using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class CategoryRepository : GenericRepository<Category>, ICategoryRepository
{
    public CategoryRepository(AppDbContext context) : base(context) { }

    public Task<List<Category>> GetAllOrderedAsync(CancellationToken ct = default)
        => _set.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => _set.AnyAsync(c => c.Id == id, ct);

    public async Task<bool> AllExistAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0) return true;
        var found = await _set.CountAsync(c => distinct.Contains(c.Id), ct);
        return found == distinct.Count;
    }
}
