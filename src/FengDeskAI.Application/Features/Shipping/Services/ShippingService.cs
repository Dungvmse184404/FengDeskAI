using System.Text.Json;
using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Sales.DTOs;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.Application.Features.Shipping.DTOs;
using FengDeskAI.Application.Features.Returns.Services;
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
    private readonly IStoreShopProvisioner _shopProvisioner;
    private readonly IReturnService _returns;

    public ShippingService(IUnitOfWork uow, IMapper mapper, Interfaces.External.IShippingProvider shipping,
        IStoreShopProvisioner shopProvisioner, IReturnService returns)
    {
        _uow = uow;
        _mapper = mapper;
        _shipping = shipping;
        _shopProvisioner = shopProvisioner;
        _returns = returns;
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

            if (delivery.IsExchange && request.NewStatus == DeliveryStatus.Delivered)
                await _returns.CompleteExchangeDeliveryAsync(delivery.Id, actorId: null, ct);

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

                if (delivery.Order.Status == OrderStatus.Shipping && preRollup != OrderStatus.Shipping)
                    await _uow.Notifications.AddAsync(new Notification
                    {
                        UserId = delivery.Order.CustomerId,
                        Type = NotificationType.OrderShipping,
                        Title = "Đơn hàng đang vận chuyển",
                        Message = "Đơn hàng của bạn đang trên đường giao đến bạn.",
                        ReferenceId = delivery.Order.Id,
                        ReferenceType = ReferenceType.Order,
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
        if (!isAdmin && delivery.Order.CustomerId != userId
            && !await _uow.Stores.CanManageAsync(delivery.GardenStoreId, userId, ct))
            return ServiceResult<List<DeliveryProgressLogResponse>>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Shipping.ViewProgressForbidden);

        var logs = await _uow.Shipping.GetProgressLogsAsync(deliveryId, ct);
        return ServiceResult<List<DeliveryProgressLogResponse>>.Success(_mapper.Map<List<DeliveryProgressLogResponse>>(logs));
    }

    /// <summary>
    /// Cửa hàng đã đủ thông tin để tạo vận đơn chưa. FE gọi khi mở màn hình đơn giao để hiện cảnh báo
    /// sớm: owner/admin được điều hướng sang mục cần bổ sung (<c>CanFix = true</c>), garden staff chỉ
    /// được báo để liên hệ chủ cửa hàng. Xem docs/adr/fix-ghn-create-shipment.md §D.
    /// </summary>
    public async Task<IServiceResult<StoreShippingReadinessResponse>> GetStoreReadinessAsync(
        Guid storeId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        // Cần Address → Ward → District để đọc mã vùng GHN.
        var store = (await _uow.Stores.GetWithAddressByIdsAsync(new[] { storeId }, ct)).FirstOrDefault();
        if (store is null)
            return ServiceResult<StoreShippingReadinessResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);

        var isOwner = isAdmin || await _uow.Stores.IsOwnerAsync(storeId, userId, ct);
        var isStaff = !isOwner && await _uow.Stores.IsAcceptedStaffAsync(storeId, userId, ct);
        if (!isOwner && !isStaff)
            return ServiceResult<StoreShippingReadinessResponse>.Failure(
                ApiStatusCodes.Forbidden, ApiStatusMessages.StoreShipping.ViewReadinessForbidden);

        return ServiceResult<StoreShippingReadinessResponse>.Success(
            BuildReadiness(store, canFix: isOwner, isAdmin: isAdmin, isOwner: isOwner));
    }

    public async Task<IServiceResult<CarrierSimulationResultResponse>> SimulateCarrierStatusAsync(
        Guid deliveryId, DeliveryStatus newStatus, CancellationToken ct = default)
    {
        if (await _uow.Shipping.GetDeliveryByIdAsync(deliveryId, ct) is null)
            return ServiceResult<CarrierSimulationResultResponse>.Failure(
                ApiStatusCodes.NotFound, ApiStatusMessages.Shipping.DeliveryNotFound);

        var result = await SimulateToStatusAsync(deliveryId, newStatus, ct);
        return result.Succeeded
            ? ServiceResult<CarrierSimulationResultResponse>.Success(result, ApiStatusMessages.Shipping.SimulationCompleted)
            : ServiceResult<CarrierSimulationResultResponse>.Failure(
                ApiStatusCodes.Conflict, result.Message ?? ApiStatusMessages.Shipping.DeliveryNotFound);
    }

    /// <summary>
    /// Bắn MỘT callback giả lập (một bước duy nhất, không tự bắc cầu). Dùng làm nguyên thủy cho
    /// <see cref="SimulateToStatusAsync"/>; caller ngoài dùng bản public đã tự đi bước trung gian.
    /// </summary>
    private async Task<IServiceResult> SendSimulatedCallbackAsync(
        Guid deliveryId, DeliveryStatus newStatus, CancellationToken ct)
    {
        var delivery = await _uow.Shipping.GetDeliveryByIdAsync(deliveryId, ct);
        if (delivery is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Shipping.DeliveryNotFound);

        // Dựng payload y như callback thật rồi đẩy vào cùng pipeline — không đi đường tắt,
        // để bug ở mapping/guard/rollup vẫn lộ ra khi test.
        var request = new ShippingWebhookRequest
        {
            Provider = delivery.ShippingProvider ?? _shipping.Name,
            EventType = "switch_status",
            DeliveryId = delivery.Id,
            ProviderOrderId = delivery.ProviderOrderId,
            NewStatus = newStatus,
            TrackingCode = delivery.TrackingCode,
            RawPayload = JsonSerializer.Serialize(new
            {
                Type = "switch_status",
                OrderCode = delivery.ProviderOrderId,
                ClientOrderCode = delivery.Id,
                Status = newStatus.ToString(),
                Simulated = true,   // đánh dấu trong raw_payload để đối soát biết đây là dữ liệu test
            }),
        };

        return await ProcessWebhookAsync(request, ct);
    }

    public async Task<IServiceResult<List<DeliveryResponse>>> GetDeliveriesByOrderAsync(
        Guid orderId, CancellationToken ct = default)
    {
        var deliveries = await _uow.Shipping.GetDeliveriesByOrderAsync(orderId, ct);
        if (deliveries.Count == 0)
            return ServiceResult<List<DeliveryResponse>>.Failure(
                ApiStatusCodes.NotFound, ApiStatusMessages.Shipping.OrderHasNoDelivery);

        return ServiceResult<List<DeliveryResponse>>.Success(_mapper.Map<List<DeliveryResponse>>(deliveries));
    }

    public async Task<IServiceResult<List<CarrierSimulationResultResponse>>> SimulateCarrierStatusForOrderAsync(
        Guid orderId, DeliveryStatus newStatus, CancellationToken ct = default)
    {
        var deliveries = await _uow.Shipping.GetDeliveriesByOrderAsync(orderId, ct);
        if (deliveries.Count == 0)
            return ServiceResult<List<CarrierSimulationResultResponse>>.Failure(
                ApiStatusCodes.NotFound, ApiStatusMessages.Shipping.OrderHasNoDelivery);

        var results = new List<CarrierSimulationResultResponse>();
        foreach (var delivery in deliveries)
        {
            ct.ThrowIfCancellationRequested();
            results.Add(await SimulateToStatusAsync(delivery.Id, newStatus, ct));
        }

        return ServiceResult<List<CarrierSimulationResultResponse>>.Success(
            results, ApiStatusMessages.Shipping.SimulationCompleted);
    }

    /// <summary>
    /// Đẩy một delivery tới trạng thái đích, tự chèn các bước trung gian mà state machine đòi hỏi
    /// (vd muốn Delivered thì phải qua Shipped). Bước trung gian fail vì đã qua giai đoạn đó thì bỏ
    /// qua; chỉ trạng thái cuối mới quyết định thành/bại.
    /// </summary>
    private async Task<CarrierSimulationResultResponse> SimulateToStatusAsync(
        Guid deliveryId, DeliveryStatus target, CancellationToken ct)
    {
        var delivery = await _uow.Shipping.GetDeliveryByIdAsync(deliveryId, ct);
        if (delivery is null)
            return Result(deliveryId, DeliveryStatus.Pending, false, ApiStatusMessages.Shipping.DeliveryNotFound);

        if (delivery.Status == target)
            return Result(deliveryId, target, true, null); // gọi lại lần 2 không tính là lỗi

        var path = FindPath(delivery.Status, target);
        if (path.Count == 0)
            return Result(deliveryId, delivery.Status, false,
                string.Format(ApiStatusMessages.Shipping.NoTransitionPathFormat, delivery.Status, target));

        // Mọi bước trong path đều hợp lệ theo state machine → không bắn callback nào biết chắc sẽ hỏng.
        foreach (var step in path)
        {
            var res = await SendSimulatedCallbackAsync(deliveryId, step, ct);
            if (!res.IsSuccess)
            {
                var current = await _uow.Shipping.GetDeliveryByIdAsync(deliveryId, ct);
                return Result(deliveryId, current?.Status ?? delivery.Status, false, res.Message);
            }
        }

        var final = await _uow.Shipping.GetDeliveryByIdAsync(deliveryId, ct);
        return Result(deliveryId, final?.Status ?? target, final?.Status == target, null);
    }

    private static CarrierSimulationResultResponse Result(Guid id, DeliveryStatus status, bool ok, string? message)
        => new() { DeliveryId = id, Status = status, Succeeded = ok, Message = message };

    /// <summary>
    /// Đường đi ngắn nhất từ trạng thái hiện tại tới đích theo <see cref="OrderWorkflow.IsValidDeliveryTransition"/>
    /// (BFS trên đồ thị trạng thái). Rỗng = không có đường hợp lệ (vd đã Cancelled).
    /// Tự suy ra từ state machine nên đổi luật chuyển trạng thái là hàm này theo ngay, không cần sửa tay.
    /// </summary>
    private static List<DeliveryStatus> FindPath(DeliveryStatus from, DeliveryStatus target)
    {
        var previous = new Dictionary<DeliveryStatus, DeliveryStatus>();
        var visited = new HashSet<DeliveryStatus> { from };
        var queue = new Queue<DeliveryStatus>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in Enum.GetValues<DeliveryStatus>())
            {
                // Kiểm tra hợp lệ TRƯỚC khi đánh dấu đã thăm, nếu không một đích không tới được từ
                // `current` sẽ bị coi là đã thăm và chặn luôn đường đi hợp lệ qua nhánh khác.
                if (!OrderWorkflow.IsValidDeliveryTransition(current, next) || !visited.Add(next)) continue;

                previous[next] = current;
                if (next == target) return Backtrack(previous, from, target);
                queue.Enqueue(next);
            }
        }
        return new List<DeliveryStatus>();
    }

    private static List<DeliveryStatus> Backtrack(
        IReadOnlyDictionary<DeliveryStatus, DeliveryStatus> previous, DeliveryStatus from, DeliveryStatus target)
    {
        var path = new List<DeliveryStatus>();
        for (var step = target; step != from; step = previous[step])
            path.Add(step);
        path.Reverse();
        return path;
    }

    public async Task<IServiceResult<CarrierShopSyncResultResponse>> SyncCarrierShopsAsync(
        int batchSize, CancellationToken ct = default)
    {
        var size = Math.Clamp(batchSize, 1, 200); // chặn trên để một cú bấm không gọi dồn nhà vận chuyển
        var result = await _shopProvisioner.BackfillAsync(size, ct);
        return ServiceResult<CarrierShopSyncResultResponse>.Success(result, ApiStatusMessages.StoreShipping.SyncCompleted);
    }

    public async Task<IServiceResult<StoreShippingReadinessResponse>> SyncStoreCarrierShopAsync(
        Guid storeId, CancellationToken ct = default)
    {
        var store = (await _uow.Stores.GetWithAddressByIdsAsync(new[] { storeId }, ct)).FirstOrDefault();
        if (store is null)
            return ServiceResult<StoreShippingReadinessResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Store.NotFound);

        await _shopProvisioner.EnsureShopIdAsync(store, ct);

        // Nhân viên sàn luôn sửa được → canFix = true, có Section để điều hướng nếu còn thiếu.
        return ServiceResult<StoreShippingReadinessResponse>.Success(
            BuildReadiness(store, canFix: true, isAdmin: true, isOwner: false),
            ApiStatusMessages.StoreShipping.SyncCompleted);
    }

    /// <summary>Gom kết quả đánh giá + ngữ cảnh vai trò thành response FE hiển thị thẳng được.</summary>
    private StoreShippingReadinessResponse BuildReadiness(Domain.Entities.Vendor.GardenStore store, bool canFix, bool isAdmin, bool isOwner)
    {
        var issues = StoreShippingReadiness.Evaluate(store, _shipping.RequiresStoreShopId);
        return new StoreShippingReadinessResponse
        {
            StoreId = store.Id,
            StoreName = store.Name,
            IsReady = issues.Count == 0,
            CanFix = canFix,
            Role = isAdmin ? "Admin" : isOwner ? "Owner" : "Staff",
            Message = issues.Count == 0
                ? ApiStatusMessages.StoreShipping.Ready
                : StoreShippingReadiness.BuildBlockedMessage(issues, canFix),
            // Chỉ người sửa được mới cần đích điều hướng; staff không có quyền vào trang sửa.
            Section = canFix ? issues.FirstOrDefault()?.Section : null,
            Issues = issues,
        };
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
        var next = OrderWorkflow.ComputeOrderStatus(order.Deliveries
            .Where(d => !d.IsExchange).Select(d => d.Status).ToList());
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
