using FengDeskAI.Domain.Entities.Catalog;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface ITagRepository : IGenericRepository<Tag>
{
    Task<List<Tag>> GetAllOrderedAsync(CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> AllExistAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}
