using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class TagRepository : GenericRepository<Tag>, ITagRepository
{
    public TagRepository(AppDbContext context) : base(context) { }

    public Task<List<Tag>> GetAllOrderedAsync(CancellationToken ct = default)
        => _set.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
        => _set.AnyAsync(t => t.Name.ToLower() == name.ToLower() && (excludeId == null || t.Id != excludeId), ct);

    public async Task<bool> AllExistAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0) return true;
        var found = await _set.CountAsync(t => distinct.Contains(t.Id), ct);
        return found == distinct.Count;
    }
}
