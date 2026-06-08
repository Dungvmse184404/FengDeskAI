using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Entities.Shipping;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class ShippingRepository : IShippingRepository
{
    private readonly AppDbContext _context;

    public ShippingRepository(AppDbContext context) => _context = context;

    public async Task AddWebhookAsync(ShippingWebhook webhook, CancellationToken ct = default)
        => await _context.Set<ShippingWebhook>().AddAsync(webhook, ct);

    public Task<Delivery?> GetDeliveryByProviderOrderIdAsync(string provider, string providerOrderId, CancellationToken ct = default)
        => _context.Set<Delivery>()
            .Include(d => d.Order).ThenInclude(o => o.Deliveries)
            .FirstOrDefaultAsync(d => d.ShippingProvider == provider && d.ProviderOrderId == providerOrderId, ct);

    public Task<Delivery?> GetDeliveryByIdAsync(Guid deliveryId, CancellationToken ct = default)
        => _context.Set<Delivery>()
            .Include(d => d.Order).ThenInclude(o => o.Deliveries)
            .FirstOrDefaultAsync(d => d.Id == deliveryId, ct);

    public async Task AddProgressLogAsync(DeliveryProgressLog log, CancellationToken ct = default)
        => await _context.Set<DeliveryProgressLog>().AddAsync(log, ct);

    public Task<List<DeliveryProgressLog>> GetProgressLogsAsync(Guid deliveryId, CancellationToken ct = default)
        => _context.Set<DeliveryProgressLog>().AsNoTracking()
            .Where(l => l.DeliveryId == deliveryId)
            .OrderBy(l => l.LoggedAt)
            .ToListAsync(ct);
}
