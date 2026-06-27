using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class ReturnRepository : GenericRepository<ReturnRequest>, IReturnRepository
{
    public ReturnRepository(AppDbContext context) : base(context) { }

    public Task<ReturnRequest?> GetWithGraphAsync(Guid id, CancellationToken ct = default)
        => _set
            .Include(r => r.Items).ThenInclude(i => i.OrderItem)
            .Include(r => r.Delivery)
            .Include(r => r.Order)
            .Include(r => r.Refund)
            .Include(r => r.StatusLogs)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<ReturnRequest?> GetDetailAsync(Guid id, Guid? customerId, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking()
            .Include(r => r.Items).ThenInclude(i => i.OrderItem)
            .Include(r => r.Delivery)
            .Include(r => r.Images)
            .Include(r => r.StatusLogs)
            .Include(r => r.Refund)
            .AsQueryable();
        if (customerId.HasValue) query = query.Where(r => r.CustomerId == customerId.Value);
        return query.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<(List<ReturnRequest> Items, int Total)> GetByCustomerAsync(Guid customerId, int skip, int take, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking().Where(r => r.CustomerId == customerId);
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(r => r.Items)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<(List<ReturnRequest> Items, int Total)> GetForStoreAsync(Guid storeId, int skip, int take, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking().Where(r => r.Delivery.GardenStoreId == storeId);
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(r => r.Items)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<(List<ReturnRequest> Items, int Total)> GetAllPagedAsync(int skip, int take, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(r => r.Items)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<Delivery?> GetDeliveryForReturnAsync(Guid deliveryId, CancellationToken ct = default)
        => _context.Set<Delivery>()
            .Include(d => d.Order)
            .Include(d => d.Items)
            .FirstOrDefaultAsync(d => d.Id == deliveryId, ct);

    public async Task<Dictionary<Guid, int>> GetReturnedQuantitiesAsync(IEnumerable<Guid> orderItemIds, CancellationToken ct = default)
    {
        var ids = orderItemIds.ToList();
        var rows = await _context.Set<ReturnItem>()
            .Where(ri => ids.Contains(ri.OrderItemId)
                && ri.ReturnRequest.Status != ReturnRequestStatus.Cancelled
                && ri.ReturnRequest.Status != ReturnRequestStatus.Rejected)
            .GroupBy(ri => ri.OrderItemId)
            .Select(g => new { OrderItemId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToListAsync(ct);
        return rows.ToDictionary(x => x.OrderItemId, x => x.Qty);
    }

    public Task<List<ProductItem>> GetProductItemsWithProductAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        => _context.Set<ProductItem>()
            .Include(pi => pi.Product)
            .Where(pi => ids.Contains(pi.Id))
            .ToListAsync(ct);

    public Task AddRefundAsync(Refund refund, CancellationToken ct = default)
        => _context.Set<Refund>().AddAsync(refund, ct).AsTask();

    public void AddStatusLog(ReturnStatusLog log)
        => _context.Set<ReturnStatusLog>().Add(log);

    public Task AddImageAsync(ReturnRequestImage image, CancellationToken ct = default)
        => _context.Set<ReturnRequestImage>().AddAsync(image, ct).AsTask();

    public Task<ReturnRequestImage?> GetImageAsync(Guid returnRequestId, Guid imageId, CancellationToken ct = default)
        => _context.Set<ReturnRequestImage>()
            .FirstOrDefaultAsync(i => i.Id == imageId && i.ReturnRequestId == returnRequestId, ct);

    public void RemoveImage(ReturnRequestImage image)
        => _context.Set<ReturnRequestImage>().Remove(image);
}
