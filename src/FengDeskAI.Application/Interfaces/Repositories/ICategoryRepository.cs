using FengDeskAI.Domain.Entities.Catalog;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface ICategoryRepository : IGenericRepository<Category>
{
    Task<List<Category>> GetAllOrderedAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<bool> AllExistAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}
