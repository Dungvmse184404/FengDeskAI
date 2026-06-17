using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class RecommendationRepository : GenericRepository<Recommendation>, IRecommendationRepository
{
    public RecommendationRepository(AppDbContext context) : base(context) { }

    public Task<Recommendation?> GetDetailForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => _set.AsNoTracking()
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

    public Task<List<FengShuiRule>> GetAllRulesAsync(CancellationToken ct = default)
        => _context.Set<FengShuiRule>().AsNoTracking().ToListAsync(ct);
}
