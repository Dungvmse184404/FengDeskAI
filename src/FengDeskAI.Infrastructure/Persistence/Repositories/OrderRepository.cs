using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class OrderRepository : GenericRepository<Order>, IOrderRepository
{
    public OrderRepository(AppDbContext context) : base(context) { }

    public Task<List<ProductItem>> GetProductItemsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        => _context.Set<ProductItem>().Where(pi => ids.Contains(pi.Id)).ToListAsync(ct);

    public async Task<(List<Order> Items, int Total)> GetByCustomerAsync(Guid customerId, int skip, int take, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking().Where(o => o.CustomerId == customerId);
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(o => o.Deliveries)
            .OrderByDescending(o => o.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<Order?> GetDetailAsync(Guid id, Guid? customerId, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Deliveries).ThenInclude(d => d.Store)
            .Include(o => o.StatusLogs)
            .AsQueryable();
        if (customerId.HasValue) query = query.Where(o => o.CustomerId == customerId.Value);
        return query.FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public Task<Order?> GetWithGraphAsync(Guid id, Guid? customerId, CancellationToken ct = default)
    {
        var query = _set
            .Include(o => o.Items)
            .Include(o => o.Deliveries)
            .AsQueryable();
        if (customerId.HasValue) query = query.Where(o => o.CustomerId == customerId.Value);
        return query.FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public Task<Delivery?> GetDeliveryWithOrderAsync(Guid deliveryId, CancellationToken ct = default)
        => _context.Set<Delivery>()
            .Include(d => d.Store)
            .Include(d => d.Order).ThenInclude(o => o.Deliveries)
            .FirstOrDefaultAsync(d => d.Id == deliveryId, ct);

    public async Task<(List<Delivery> Items, int Total)> GetDeliveriesForStoreAsync(Guid storeId, int skip, int take, CancellationToken ct = default)
    {
        var query = _context.Set<Delivery>().AsNoTracking().Where(d => d.GardenStoreId == storeId);
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(d => d.Order)
            .OrderByDescending(d => d.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return (items, total);
    }
}
