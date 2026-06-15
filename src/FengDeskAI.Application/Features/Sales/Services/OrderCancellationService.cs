using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Entities.Shipping;
using FengDeskAI.Domain.Enums.Notification;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Domain.Enums.Shipping;
using Microsoft.Extensions.Logging;
using FengDeskAI.Domain.Entities.Announcement;

namespace FengDeskAI.Application.Features.Sales.Services;

public class OrderCancellationService : IOrderCancellationService
{
    private readonly IUnitOfWork _uow;
    private readonly IPaymentGateway _gateway;
    private readonly ILogger<OrderCancellationService> _logger;

    public OrderCancellationService(IUnitOfWork uow, IPaymentGateway gateway, ILogger<OrderCancellationService> logger)
    {
        _uow = uow;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task CancelAsync(Order order, Guid? actorId, string note, bool expired = false, CancellationToken ct = default)
    {
        var pendingTxns = await _uow.Transactions.GetPendingByOrderAsync(order.Id, ct);

        // Hủy link PayOS TRƯỚC khi chuyển trạng thái local để link không còn thanh toán được
        // — best-effort: link đã hết hạn/lỗi mạng vẫn cho hủy ở local.
        foreach (var txn in pendingTxns.Where(t => t.PaymentMethod == PaymentMethod.PayOS))
        {
            try
            {
                await _gateway.CancelPaymentLinkAsync(txn.OrderCode, note, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hủy link PayOS thất bại cho orderCode {OrderCode} — vẫn chuyển trạng thái ở local.", txn.OrderCode);
            }
        }

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            var now = DateTime.UtcNow;
            var toTxnStatus = expired ? PaymentStatus.Expired : PaymentStatus.Cancelled;
            var toOrderStatus = expired ? OrderStatus.Expired : OrderStatus.Cancelled;

            foreach (var txn in pendingTxns)
                txn.Status = toTxnStatus;

            var from = order.Status;
            order.Status = toOrderStatus;

            foreach (var delivery in order.Deliveries.Where(d => d.Status != DeliveryStatus.Cancelled))
            {
                var deliveryFrom = delivery.Status;
                delivery.Status = DeliveryStatus.Cancelled;
                delivery.ProgressLogs.Add(new DeliveryProgressLog
                {
                    DeliveryId = delivery.Id,
                    SourceType = DeliverySource.System,
                    FromStatus = deliveryFrom.ToString(),
                    ToStatus = DeliveryStatus.Cancelled.ToString(),
                    Note = note,
                    LoggedAt = now,
                });
            }

            // Hoàn kho
            var productItems = await _uow.Orders.GetProductItemsAsync(order.Items.Select(i => i.ProductItemId).Distinct(), ct);
            var byId = productItems.ToDictionary(p => p.Id);
            foreach (var item in order.Items)
                if (byId.TryGetValue(item.ProductItemId, out var pi))
                    pi.Stock += item.Quantity;

            order.StatusChangeNote = note;

            await _uow.Notifications.AddAsync(new Notification
            {
                UserId = order.CustomerId,
                Type = NotificationType.OrderCancelled,
                Title = expired ? "Đơn hàng hết hạn thanh toán" : "Đơn hàng đã hủy",
                Message = expired
                    ? "Đơn hàng của bạn đã hết hạn do chưa thanh toán đúng hạn."
                    : "Đơn hàng của bạn đã bị hủy.",
                ReferenceId = order.Id,
                ReferenceType = ReferenceType.Order,
                IsRead = false,
            }, ct);

            return null;
        }, ct);
    }
}
