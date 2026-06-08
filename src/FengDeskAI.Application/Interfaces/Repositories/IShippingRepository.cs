using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Entities.Shipping;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IShippingRepository
{
    Task AddWebhookAsync(ShippingWebhook webhook, CancellationToken ct = default);

    /// <summary>Tìm delivery theo provider + provider_order_id (kèm Order.Deliveries để rollup).</summary>
    Task<Delivery?> GetDeliveryByProviderOrderIdAsync(string provider, string providerOrderId, CancellationToken ct = default);

    /// <summary>Tìm delivery theo id (kèm Order.Deliveries để rollup).</summary>
    Task<Delivery?> GetDeliveryByIdAsync(Guid deliveryId, CancellationToken ct = default);

    Task AddProgressLogAsync(DeliveryProgressLog log, CancellationToken ct = default);
    Task<List<DeliveryProgressLog>> GetProgressLogsAsync(Guid deliveryId, CancellationToken ct = default);
}
