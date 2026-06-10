using FengDeskAI.Domain.Entities.Sales;

namespace FengDeskAI.Application.Features.Sales.Services;

/// <summary>
/// Lõi hủy / hết hạn đơn dùng chung cho OrderService (khách hủy đơn),
/// PaymentService (hủy thanh toán) và OrderExpirationService (quét đơn quá hạn).
/// </summary>
public interface IOrderCancellationService
{
    /// <summary>
    /// Hủy link PayOS còn treo (best-effort), chuyển mọi transaction Pending + order
    /// sang Cancelled (hoặc Expired), hủy các delivery, hoàn kho và ghi OrderStatusLog.
    /// <paramref name="order"/> phải tracked kèm Items + Deliveries (GetWithGraphAsync).
    /// </summary>
    Task CancelAsync(Order order, Guid? actorId, string note, bool expired = false, CancellationToken ct = default);
}
