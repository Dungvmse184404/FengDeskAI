using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class ReviewRepository : GenericRepository<Review>, IReviewRepository
{
    public ReviewRepository(AppDbContext context) : base(context) { }

    public Task<List<Review>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _set.AsNoTracking()
               .Include(r => r.User)
               .Include(r => r.OrderItem)
               .Where(r => r.UserId == userId)
               .OrderByDescending(r => r.CreatedAt)
               .ToListAsync(ct);

    public async Task<(List<Review> Items, int TotalCount)> GetPagedAsync(
        Guid? productId, Guid? storeId, int skip, int take, CancellationToken ct = default)
    {
        var query = Filter(productId, storeId);
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(r => r.User)
            .Include(r => r.OrderItem)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<List<ReviewCandidateRow>> GetCandidatesAsync(
        Guid userId, Guid? orderId = null, Guid? orderItemId = null, Guid? productId = null, CancellationToken ct = default)
    {
        var items = _context.Set<OrderItem>().AsNoTracking().Where(i => i.Order.CustomerId == userId);
        if (orderId is { } oid) items = items.Where(i => i.OrderId == oid);
        if (orderItemId is { } iid) items = items.Where(i => i.Id == iid);
        if (productId is { } pid) items = items.Where(i => (i.ProductId ?? i.ProductItem!.ProductId) == pid);

        return items
            .OrderByDescending(i => i.Order.CreatedAt)
            .Select(i => new ReviewCandidateRow(
                i.Id,
                i.OrderId,
                i.ProductId ?? i.ProductItem!.ProductId,
                i.ProductName,
                i.VariantName,
                i.ImageUrl,
                i.GardenStoreId ?? (i.Delivery != null ? i.Delivery.GardenStoreId : null),
                i.Delivery != null ? i.Delivery.Status : null,
                // Hoàn hàng = lệnh hoàn tiền của dòng này đã xong (cả Refund lẫn Exchange rơi về hoàn tiền).
                _context.Set<ReturnItem>().Any(ri => ri.OrderItemId == i.Id
                    && ri.ReturnRequest.Refund != null
                    && ri.ReturnRequest.Refund.Status == RefundStatus.Completed),
                _context.Set<Product>().Any(p => p.Id == (i.ProductId ?? i.ProductItem!.ProductId)),
                _set.Where(r => r.OrderItemId == i.Id).Select(r => (Guid?)r.Id).FirstOrDefault(),
                i.Order.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<int[]> GetRatingDistributionAsync(Guid? productId, Guid? storeId, CancellationToken ct = default)
    {
        var rows = await Filter(productId, storeId)
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var distribution = new int[5];
        foreach (var row in rows.Where(r => r.Rating is >= 1 and <= 5))
            distribution[row.Rating - 1] = row.Count;
        return distribution;
    }

    public async Task<(double Average, int Count)> GetStoreRatingSummaryAsync(Guid storeId, CancellationToken ct = default)
    {
        var distribution = await GetRatingDistributionAsync(null, storeId, ct);
        var count = distribution.Sum();
        if (count == 0) return (0, 0);
        var average = distribution.Select((n, i) => n * (i + 1)).Sum() / (double)count;
        return (average, count);
    }

    private IQueryable<Review> Filter(Guid? productId, Guid? storeId)
    {
        var query = _set.AsNoTracking();
        if (productId is { } pid) query = query.Where(r => r.ProductId == pid);
        // Theo cột chụp: đánh giá của sản phẩm đã xoá vẫn tính cho cửa hàng.
        if (storeId is { } sid) query = query.Where(r => (r.GardenStoreId ?? r.Product!.GardenStoreId) == sid);
        return query;
    }
}
