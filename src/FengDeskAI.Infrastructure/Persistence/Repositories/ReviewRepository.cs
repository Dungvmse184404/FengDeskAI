using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class ReviewRepository : GenericRepository<Review>, IReviewRepository
{
    public ReviewRepository(AppDbContext context) : base(context) { }

    public Task<List<Review>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _set.AsNoTracking()
               .Where(r => r.UserId == userId)
               .OrderByDescending(r => r.CreatedAt)
               .ToListAsync(ct);

    public Task<List<Review>> GetByProductIdAsync(Guid productId, CancellationToken ct = default)
        => _set.AsNoTracking()
               .Where(r => r.ProductId == productId)
               .OrderByDescending(r => r.CreatedAt)
               .ToListAsync(ct);

    public Task<Review?> GetByIdWithUserAsync(Guid id, CancellationToken ct = default)
        => _set.Include(r => r.User)
               .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> HasUserPurchasedProductAsync(Guid userId, Guid productId, CancellationToken ct = default)
        => _context.Set<Domain.Entities.Sales.Order>()
               .Where(o => o.CustomerId == userId
                        && (o.Status == OrderStatus.Paid
                         || o.Status == OrderStatus.Completed
                         || o.Status == OrderStatus.Processing))//mốt gom lại sau
               .SelectMany(o => o.Items)
               .AnyAsync(oi => oi.ProductItem.ProductId == productId, ct);

    public Task<bool> HasUserReviewedProductAsync(Guid userId, Guid productId, CancellationToken ct = default)
        => _set.AnyAsync(r => r.UserId == userId && r.ProductId == productId, ct);

    public async Task<(double Average, int Count)> GetStoreRatingSummaryAsync(Guid storeId, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking().Where(r => r.Product.GardenStoreId == storeId);
        var count = await query.CountAsync(ct);
        if (count == 0) return (0, 0);
        var average = await query.AverageAsync(r => r.Rating, ct);
        return (average, count);
    }
}
