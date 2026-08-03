using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class OrderRepository : GenericRepository<Order>, IOrderRepository
{
    public OrderRepository(AppDbContext context) : base(context) { }

    public Task<List<ProductItem>> GetProductItemsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        => _context.Set<ProductItem>().Where(pi => ids.Contains(pi.Id)).ToListAsync(ct);

    public Task AddDeliveriesAsync(IEnumerable<Delivery> deliveries, CancellationToken ct = default)
        => _context.Set<Delivery>().AddRangeAsync(deliveries, ct);

    public Task AddOrderItemsAsync(IEnumerable<OrderItem> items, CancellationToken ct = default)
        => _context.Set<OrderItem>().AddRangeAsync(items, ct);

    public Task<Order?> GetForPaymentAsync(Guid id, Guid customerId, CancellationToken ct = default)
        => _set.Include(o => o.Items).ThenInclude(i => i.ProductItem).ThenInclude(pi => pi.Product)
               .Include(o => o.Deliveries)
               .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == customerId, ct);

    public Task<Order?> GetForDeliveryCreationAsync(Guid id, CancellationToken ct = default)
        => _set.Include(o => o.Items).ThenInclude(i => i.ProductItem).ThenInclude(pi => pi.Product)
               .Include(o => o.Deliveries)
               .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<(List<Order> Items, int Total)> GetByCustomerAsync(Guid customerId, int skip, int take, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking().Where(o => o.CustomerId == customerId);
        var total = await query.CountAsync(ct);
        var items = await IncludeStores(query)
            .OrderByDescending(o => o.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<(List<Order> Items, int Total)> GetAllAsync(int skip, int take, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await IncludeStores(query)
            .OrderByDescending(o => o.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return (items, total);
    }

    /// <summary>
    /// Nạp cửa hàng cho màn danh sách đơn: qua delivery, và qua product của item (đơn online chưa
    /// thanh toán chưa có delivery). Split query vì 2 collection include gây nhân bản dòng.
    /// </summary>
    private static IQueryable<Order> IncludeStores(IQueryable<Order> query)
        => query
            .Include(o => o.Deliveries).ThenInclude(d => d.Store)
            .Include(o => o.Items).ThenInclude(i => i.ProductItem).ThenInclude(pi => pi.Product).ThenInclude(p => p.Store)
            .Include(o => o.Items).ThenInclude(i => i.ProductItem).ThenInclude(pi => pi.Product).ThenInclude(p => p.Images)
            .AsSplitQuery();

    public Task<Order?> GetDetailAsync(Guid id, Guid? customerId, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.ProductItem).ThenInclude(pi => pi.Product).ThenInclude(p => p.Images)
            .Include(o => o.Deliveries).ThenInclude(d => d.Store)
            .Include(o => o.StatusLogs)
            .AsSplitQuery()
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

    public Task<List<Order>> GetOverduePendingAsync(DateTime cutoffUtc, int take, CancellationToken ct = default)
        => _set.Include(o => o.Items)
               .Include(o => o.Deliveries)
               .Where(o => o.Status == OrderStatus.Pending
                        && o.PaymentMethod != PaymentMethod.COD
                        && o.CreatedAt < cutoffUtc)
               .OrderBy(o => o.CreatedAt)
               .Take(take)
               .ToListAsync(ct);

    public Task<Delivery?> GetDeliveryWithOrderAsync(Guid deliveryId, CancellationToken ct = default)
        => _context.Set<Delivery>()
            .Include(d => d.Store)
            .Include(d => d.Items).ThenInclude(i => i.ProductItem)
            .Include(d => d.Order).ThenInclude(o => o.Deliveries)
            .FirstOrDefaultAsync(d => d.Id == deliveryId, ct);

    public async Task<(List<Delivery> Items, int Total)> GetDeliveriesForStoreAsync(
        Guid storeId, Guid? assignedStaffId, int skip, int take, CancellationToken ct = default)
    {
        var query = _context.Set<Delivery>().AsNoTracking().Where(d => d.GardenStoreId == storeId);
        if (assignedStaffId.HasValue)
            query = query.Where(d => d.AssignedStaffId == assignedStaffId.Value);
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(d => d.Order)
            .OrderByDescending(d => d.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<Delivery?> GetDeliveryDetailAsync(Guid deliveryId, CancellationToken ct = default)
        => _context.Set<Delivery>().AsNoTracking()
            .Include(d => d.Store)
            .Include(d => d.Items).ThenInclude(i => i.ProductItem).ThenInclude(pi => pi.Product).ThenInclude(p => p.Images)
            .Include(d => d.Order).ThenInclude(o => o.ShippingAddress).ThenInclude(a => a.Ward).ThenInclude(w => w.District).ThenInclude(dt => dt.Province)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Id == deliveryId, ct);
}
