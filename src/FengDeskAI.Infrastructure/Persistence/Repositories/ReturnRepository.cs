using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Payment;
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
            .Include(r => r.VendorLiability)
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
            .Include(r => r.VendorLiability)
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

    // ----- Staff queue / SLA -----

    public async Task<(List<ReturnRequest> Items, int Total)> GetPendingForStaffAsync(int skip, int take, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking().Where(r =>
            r.Status == ReturnRequestStatus.Requested
            || r.Status == ReturnRequestStatus.UnderReview
            || r.Status == ReturnRequestStatus.Reviewing);
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(r => r.Items)
            .OrderBy(r => r.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<List<ReturnRequest>> GetOverdueEvidenceTicketsAsync(DateTime nowUtc, int max, CancellationToken ct = default)
        => _set
            .Where(r => r.Status == ReturnRequestStatus.NeedMoreEvidence
                && r.EvidenceDeadline != null && r.EvidenceDeadline < nowUtc)
            .OrderBy(r => r.EvidenceDeadline)
            .Take(max)
            .ToListAsync(ct);

    // ----- Refund -----

    public Task<Refund?> GetRefundByIdAsync(Guid refundId, CancellationToken ct = default)
        => _context.Set<Refund>()
            .Include(f => f.ReturnRequest)
            .FirstOrDefaultAsync(f => f.Id == refundId, ct);

    public Task<Refund?> GetRefundByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
        => _context.Set<Refund>()
            .Include(f => f.ReturnRequest)
            .FirstOrDefaultAsync(f => f.IdempotencyKey == idempotencyKey, ct);

    public Task<Refund?> GetRefundByProviderRefAsync(string providerRefundId, CancellationToken ct = default)
        => _context.Set<Refund>()
            .Include(f => f.ReturnRequest)
            .FirstOrDefaultAsync(f => f.ProviderRefundId == providerRefundId, ct);

    public async Task<(List<Refund> Items, int Total)> GetRefundsForManagerAsync(int skip, int take, CancellationToken ct = default)
    {
        var query = _context.Set<Refund>().AsNoTracking()
            .Where(f => f.Status == RefundStatus.Failed || f.Status == RefundStatus.ManagerReview);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(f => f.UpdatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<List<Refund>> GetRetryableFailedRefundsAsync(int maxRetry, int max, CancellationToken ct = default)
        => _context.Set<Refund>()
            .Include(f => f.ReturnRequest)
            .Where(f => f.Status == RefundStatus.Failed && f.RetryCount < maxRetry)
            .OrderBy(f => f.UpdatedAt)
            .Take(max)
            .ToListAsync(ct);

    // ----- Vendor liability -----

    public Task AddVendorLiabilityAsync(VendorLiability liability, CancellationToken ct = default)
        => _context.Set<VendorLiability>().AddAsync(liability, ct).AsTask();

    public Task<VendorLiability?> GetVendorLiabilityAsync(Guid id, CancellationToken ct = default)
        => _context.Set<VendorLiability>()
            .Include(v => v.ReturnRequest)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<(List<VendorLiability> Items, int Total)> GetLiabilitiesByGardenAsync(Guid gardenId, int skip, int take, CancellationToken ct = default)
    {
        var query = _context.Set<VendorLiability>().AsNoTracking().Where(v => v.GardenStoreId == gardenId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<List<VendorLiability>> GetOverdueLiabilitiesAsync(DateTime nowUtc, int max, CancellationToken ct = default)
        => _context.Set<VendorLiability>()
            .Where(v => v.Status == VendorLiabilityStatus.Pending && v.DisputeDeadline < nowUtc)
            .OrderBy(v => v.DisputeDeadline)
            .Take(max)
            .ToListAsync(ct);
}
