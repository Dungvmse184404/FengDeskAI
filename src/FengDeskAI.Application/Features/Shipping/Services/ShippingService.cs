using System.Text.Json;
using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.Application.Features.Shipping.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Entities.Shipping;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Domain.Enums.Shipping;

namespace FengDeskAI.Application.Features.Shipping.Services;

public class ShippingService : IShippingService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public ShippingService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
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
                return ServiceResult.Success("Đã nhận webhook nhưng chưa khớp delivery (lưu để đối soát sau).", ApiStatusCodes.Accepted);

            if (!OrderWorkflow.IsValidDeliveryTransition(delivery.Status, request.NewStatus))
            {
                await _uow.Shipping.AddProgressLogAsync(BuildLog(delivery, delivery.Status, request, payloadJson,
                    note: $"Webhook bị bỏ qua: chuyển trạng thái không hợp lệ {delivery.Status} → {request.NewStatus}"), ct);
                webhook.IsProcessed = true;
                return ServiceResult.Failure(ApiStatusCodes.Conflict, $"Trạng thái webhook không hợp lệ với delivery hiện tại ({delivery.Status}).");
            }

            var from = delivery.Status;
            delivery.Status = request.NewStatus;
            if (request.TrackingCode is not null) delivery.TrackingCode = request.TrackingCode;
            if (request.Provider is not null) delivery.ShippingProvider = request.Provider;

            var now = DateTime.UtcNow;
            switch (request.NewStatus)
            {
                case DeliveryStatus.Confirmed: delivery.AssignedAt = now; break;
                case DeliveryStatus.Shipped: delivery.ShippedAt = now; break;
                case DeliveryStatus.Delivered: delivery.DeliveredAt = now; break;
            }

            await _uow.Shipping.AddProgressLogAsync(BuildLog(delivery, from, request, payloadJson, request.EventType), ct);
            RollupOrder(delivery.Order);
            webhook.IsProcessed = true;

            return ServiceResult.Success("Đã xử lý webhook và cập nhật trạng thái giao hàng.");
        }, ct);
    }

    public async Task<IServiceResult<List<DeliveryProgressLogResponse>>> GetProgressLogsAsync(Guid deliveryId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var delivery = await _uow.Shipping.GetDeliveryByIdAsync(deliveryId, ct);
        if (delivery is null)
            return ServiceResult<List<DeliveryProgressLogResponse>>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy delivery.");
        if (!isAdmin && !await _uow.Stores.CanManageAsync(delivery.GardenStoreId, userId, ct))
            return ServiceResult<List<DeliveryProgressLogResponse>>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền xem tiến trình giao hàng này.");

        var logs = await _uow.Shipping.GetProgressLogsAsync(deliveryId, ct);
        return ServiceResult<List<DeliveryProgressLogResponse>>.Success(_mapper.Map<List<DeliveryProgressLogResponse>>(logs));
    }

    private async Task<Delivery?> ResolveDeliveryAsync(ShippingWebhookRequest request, CancellationToken ct)
    {
        if (request.DeliveryId.HasValue)
            return await _uow.Shipping.GetDeliveryByIdAsync(request.DeliveryId.Value, ct);
        if (!string.IsNullOrWhiteSpace(request.Provider) && !string.IsNullOrWhiteSpace(request.ProviderOrderId))
            return await _uow.Shipping.GetDeliveryByProviderOrderIdAsync(request.Provider!, request.ProviderOrderId!, ct);
        return null;
    }

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
