using System.Text.Json;
using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.Application.Features.Shipping.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Entities.Shipping;
using FengDeskAI.Domain.Enums.Notification;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Domain.Enums.Shipping;
using FengDeskAI.Domain.Entities.Announcement;

namespace FengDeskAI.Application.Features.Shipping.Services;

public class ShippingService : IShippingService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly Interfaces.External.IShippingProvider _shipping;

    public ShippingService(IUnitOfWork uow, IMapper mapper, Interfaces.External.IShippingProvider shipping)
    {
        _uow = uow;
        _mapper = mapper;
        _shipping = shipping;
    }

    public async Task<IServiceResult> ProcessWebhookAsync(ShippingWebhookRequest request, CancellationToken ct = default)
    {
        var payloadJson = string.IsNullOrWhiteSpace(request.RawPayload)
            ? JsonSerializer.Serialize(request)
            : request.RawPayload!;

        return await _uow.ExecuteInTransactionAsync<IServiceResult>(async _ =>
        {
            var webhook = new ShippingWebhook
            {
                Provider = request.Provider,
                EventType = request.EventType,
                Payload = payloadJson,
                IsProcessed = false,
                ReceivedAt = DateTime.UtcNow,
            };
            await _uow.Shipping.AddWebhookAsync(webhook, ct);

            var delivery = await ResolveDeliveryAsync(request, ct);
            if (delivery is null)
                return ServiceResult.Success(ApiStatusMessages.Shipping.WebhookUnmatched, ApiStatusCodes.Accepted);

            if (!OrderWorkflow.IsValidDeliveryTransition(delivery.Status, request.NewStatus))
            {
                await _uow.Shipping.AddProgressLogAsync(BuildLog(delivery, delivery.Status, request, payloadJson,
                    note: $"Webhook bị bỏ qua: chuyển trạng thái không hợp lệ {delivery.Status} → {request.NewStatus}"), ct);
                webhook.IsProcessed = true;
                return ServiceResult.Failure(ApiStatusCodes.Conflict, string.Format(ApiStatusMessages.Shipping.WebhookInvalidStatusFormat, delivery.Status));
            }

            var from = delivery.Status;
            delivery.Status = request.NewStatus;
            if (request.TrackingCode is not null) delivery.TrackingCode = request.TrackingCode;
            if (request.TrackingUrl is not null) delivery.TrackingUrl = request.TrackingUrl;
            if (request.Provider is not null) delivery.ShippingProvider = request.Provider;

            var now = DateTime.UtcNow;
            switch (request.NewStatus)
            {
                case DeliveryStatus.Confirmed: delivery.AssignedAt = now; break;
                case DeliveryStatus.Shipped: delivery.ShippedAt = now; break;
                case DeliveryStatus.Delivered: delivery.DeliveredAt = now; break;
            }

            await _uow.Shipping.AddProgressLogAsync(BuildLog(delivery, from, request, payloadJson, request.EventType), ct);

            var preRollup = delivery.Order.Status;
            RollupOrder(delivery.Order);
            webhook.IsProcessed = true;

            if (delivery.Order is not null)
            {
                var (nType, nTitle, nMsg) = MapDeliveryNotification(request.NewStatus);
                await _uow.Notifications.AddAsync(new Notification
                {
                    UserId = delivery.Order.CustomerId,
                    Type = nType,
                    Title = nTitle,
                    Message = nMsg,
                    ReferenceId = delivery.Id,
                    ReferenceType = ReferenceType.Delivery,
                    IsRead = false,
                }, ct);

                if (delivery.Order.Status == OrderStatus.Completed && preRollup != OrderStatus.Completed)
                    await _uow.Notifications.AddAsync(new Notification
                    {
                        UserId = delivery.Order.CustomerId,
                        Type = NotificationType.OrderCompleted,
                        Title = "Hoàn thành đơn hàng",
                        Message = "Đơn hàng của bạn đã hoàn thành. Cảm ơn bạn đã mua sắm!",
                        ReferenceId = delivery.Order.Id,
                        ReferenceType = ReferenceType.Order,
                        IsRead = false,
                    }, ct);
            }
            return ServiceResult.Success(ApiStatusMessages.Shipping.WebhookProcessed);
        }, ct);
    }

    public async Task<IServiceResult<List<DeliveryProgressLogResponse>>> GetProgressLogsAsync(Guid deliveryId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var delivery = await _uow.Shipping.GetDeliveryByIdAsync(deliveryId, ct);
        if (delivery is null)
            return ServiceResult<List<DeliveryProgressLogResponse>>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Shipping.DeliveryNotFound);
        if (!isAdmin && !await _uow.Stores.CanManageAsync(delivery.GardenStoreId, userId, ct))
            return ServiceResult<List<DeliveryProgressLogResponse>>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Shipping.ViewProgressForbidden);

        var logs = await _uow.Shipping.GetProgressLogsAsync(deliveryId, ct);
        return ServiceResult<List<DeliveryProgressLogResponse>>.Success(_mapper.Map<List<DeliveryProgressLogResponse>>(logs));
    }

    public async Task<IServiceResult> RedeliverAsync(Guid deliveryId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var delivery = await _uow.Shipping.GetDeliveryByIdAsync(deliveryId, ct);
        if (delivery is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Shipping.DeliveryNotFound);
        if (!isAdmin && !await _uow.Stores.CanManageAsync(delivery.GardenStoreId, userId, ct))
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Shipping.ViewProgressForbidden);
        if (delivery.Status != DeliveryStatus.DeliveryFailed)
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, "Chỉ yêu cầu giao lại khi đơn giao đang ở trạng thái giao thất bại.");
        if (string.IsNullOrEmpty(delivery.ProviderOrderId))
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, "Đơn giao chưa có mã vận đơn nhà vận chuyển.");

        var store = (await _uow.Stores.GetWithAddressByIdsAsync(new[] { delivery.GardenStoreId }, ct)).FirstOrDefault();
        var ok = await _shipping.RedeliverAsync(delivery.ProviderOrderId!, store?.GhnShopId, ct);
        if (!ok)
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, "Nhà vận chuyển không hỗ trợ yêu cầu giao lại.");

        // Không tự đổi trạng thái — chỉ ghi log; webhook "delivering" tiếp theo sẽ chuyển về Shipped.
        await _uow.Shipping.AddProgressLogAsync(new DeliveryProgressLog
        {
            DeliveryId = delivery.Id,
            SourceType = DeliverySource.System,
            FromStatus = delivery.Status.ToString(),
            ToStatus = delivery.Status.ToString(),
            Note = "Đã yêu cầu nhà vận chuyển giao lại",
            LoggedAt = DateTime.UtcNow,
        }, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã gửi yêu cầu giao lại đến nhà vận chuyển.");
    }

    private async Task<Delivery?> ResolveDeliveryAsync(ShippingWebhookRequest request, CancellationToken ct)
    {
        if (request.DeliveryId.HasValue)
            return await _uow.Shipping.GetDeliveryByIdAsync(request.DeliveryId.Value, ct);
        if (!string.IsNullOrWhiteSpace(request.Provider) && !string.IsNullOrWhiteSpace(request.ProviderOrderId))
            return await _uow.Shipping.GetDeliveryByProviderOrderIdAsync(request.Provider!, request.ProviderOrderId!, ct);
        return null;
    }

    private static (NotificationType Type, string Title, string Message) MapDeliveryNotification(DeliveryStatus status)
        => status switch
        {
            DeliveryStatus.Confirmed => (NotificationType.DeliveryConfirmed, "Đơn hàng đã xác nhận", "Cửa hàng đã xác nhận đơn giao của bạn."),
            DeliveryStatus.Preparing => (NotificationType.DeliveryPreparing, "Đang chuẩn bị hàng",   "Cửa hàng đang chuẩn bị hàng cho đơn giao của bạn."),
            DeliveryStatus.Shipped   => (NotificationType.DeliveryShipped,   "Đơn hàng đang giao",   "Đơn giao của bạn đang trên đường đến."),
            DeliveryStatus.Delivered => (NotificationType.DeliveryDelivered, "Giao hàng thành công", "Đơn giao của bạn đã được giao thành công."),
            DeliveryStatus.Returned  => (NotificationType.DeliveryReturned,  "Hàng đã hoàn trả",    "Đơn giao của bạn đã được hoàn trả."),
            DeliveryStatus.Cancelled => (NotificationType.DeliveryCancelled, "Hủy giao hàng",        "Đơn giao của bạn đã bị hủy."),
            _                        => (NotificationType.SystemAlert,       "Cập nhật đơn giao",    "Trạng thái đơn giao của bạn đã thay đổi."),
        };

    private static DeliveryProgressLog BuildLog(Delivery delivery, DeliveryStatus from, ShippingWebhookRequest request, string payloadJson, string? note)
        => new()
        {
            DeliveryId = delivery.Id,
            SourceType = DeliverySource.Webhook,
            FromStatus = from.ToString(),
            ToStatus = request.NewStatus.ToString(),
            RawPayload = payloadJson,
            Note = note,
            LoggedAt = DateTime.UtcNow,
        };

    private static void RollupOrder(Order order)
    {
        var next = OrderWorkflow.ComputeOrderStatus(order.Deliveries.Select(d => d.Status).ToList());
        if (next == order.Status) return;

        var from = order.Status;
        order.Status = next;
        order.StatusLogs.Add(new OrderStatusLog
        {
            FromStatus = from.ToString(),
            ToStatus = next.ToString(),
            ChangedBy = null,
            ChangedAt = DateTime.UtcNow,
            Note = "Tự động cập nhật theo webhook vận chuyển",
        });
    }
}
