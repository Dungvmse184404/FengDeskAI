using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Payment.DTOs;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Entities.Shipping;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Domain.Enums.Shipping;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Application.Features.Payment.Services;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _uow;
    private readonly IPaymentGateway _gateway;
    private readonly IShippingProvider _shipping;
    private readonly IOrderCancellationService _cancellation;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(IUnitOfWork uow, IPaymentGateway gateway, IShippingProvider shipping,
        IOrderCancellationService cancellation, ILogger<PaymentService> logger)
    {
        _uow = uow;
        _gateway = gateway;
        _shipping = shipping;
        _cancellation = cancellation;
        _logger = logger;
    }

    public async Task<IServiceResult<CreatePaymentResponse>> CreatePaymentAsync(Guid orderId, Guid userId, CancellationToken ct = default)
    {
        var order = await _uow.Orders.GetDetailAsync(orderId, userId, ct);
        if (order is null)
            return ServiceResult<CreatePaymentResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy đơn hàng.");
        if (order.Status != OrderStatus.Pending)
            return ServiceResult<CreatePaymentResponse>.Failure(ApiStatusCodes.BadRequest, "Đơn hàng không ở trạng thái chờ thanh toán.");
        if (order.PaymentMethod == PaymentMethod.COD)
            return ServiceResult<CreatePaymentResponse>.Failure(ApiStatusCodes.BadRequest, "Đơn COD thanh toán khi nhận hàng, không cần thanh toán online.");
        if (await _uow.Transactions.HasPaidAsync(orderId, ct))
            return ServiceResult<CreatePaymentResponse>.Failure(ApiStatusCodes.BadRequest, "Đơn hàng đã được thanh toán.");

        // Vô hiệu các link cũ còn treo để một đơn không thể bị trả tiền 2 lần qua link khác nhau.
        var staleTxns = await _uow.Transactions.GetPendingByOrderAsync(orderId, ct);
        foreach (var stale in staleTxns)
        {
            try
            {
                await _gateway.CancelPaymentLinkAsync(stale.OrderCode, "Tạo link thanh toán mới", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hủy link PayOS cũ thất bại cho orderCode {OrderCode}.", stale.OrderCode);
            }
            stale.Status = PaymentStatus.Expired;
        }

        var amount = (int)Math.Round(order.TotalAmount, MidpointRounding.AwayFromZero);
        if (amount <= 0)
            return ServiceResult<CreatePaymentResponse>.Failure(ApiStatusCodes.BadRequest, "Số tiền thanh toán không hợp lệ.");

        var orderCode = GenerateOrderCode();
        var items = order.Items.Count > 0
            ? order.Items.Select(i => new PaymentLineItem(Truncate(i.ProductName, 25), i.Quantity, (int)Math.Round(i.UnitPrice))).ToList()
            : new List<PaymentLineItem> { new("Đơn hàng FengDeskAI", 1, amount) };

        var description = Truncate($"FD #{orderCode}", 25);

        PaymentLinkResult link;
        try
        {
            link = await _gateway.CreatePaymentLinkAsync(new PaymentLinkRequest(orderCode, amount, description, items), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tạo link PayOS thất bại cho order {OrderId}", orderId);
            return ServiceResult<CreatePaymentResponse>.Failure(ApiStatusCodes.ServiceUnavailable, "Không tạo được link thanh toán. Thử lại sau.");
        }

        var txn = new Transaction
        {
            OrderId = orderId,
            OrderCode = orderCode,
            Amount = order.TotalAmount,
            PaymentMethod = PaymentMethod.PayOS,
            Status = PaymentStatus.Pending,
        };
        await _uow.Transactions.AddAsync(txn, ct);
        await _uow.SaveChangesAsync(ct);

        return ServiceResult<CreatePaymentResponse>.Success(new CreatePaymentResponse
        {
            OrderId = orderId,
            OrderCode = orderCode,
            Amount = order.TotalAmount,
            CheckoutUrl = link.CheckoutUrl,
            QrCode = link.QrCode,
            PaymentLinkId = link.PaymentLinkId,
            Status = PaymentStatus.Pending,
        }, "Đã tạo link thanh toán.", ApiStatusCodes.Created);
    }

    public async Task<IServiceResult> HandleWebhookAsync(string rawJsonBody, CancellationToken ct = default)
    {
        PaymentWebhookResult result;
        try
        {
            result = _gateway.VerifyWebhook(rawJsonBody);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Webhook PayOS có chữ ký không hợp lệ.");
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, "Webhook không hợp lệ.");
        }

        _logger.LogInformation("[Webhook] Nhận PayOS: orderCode={OrderCode} success={Success} code={Code} ref={Ref}",
            result.OrderCode, result.Success, result.Code, result.ProviderReference);

        var txn = await _uow.Transactions.GetByOrderCodeAsync(result.OrderCode, ct);
        if (txn is null)
        {
            _logger.LogWarning("[Webhook] orderCode {OrderCode} không khớp giao dịch nào.", result.OrderCode);
            return ServiceResult.Success("Đã nhận webhook (không khớp giao dịch).");
        }
        _logger.LogInformation("[Webhook] Khớp transaction {TxnId} (status {Status}) của order {OrderId} (status {OrderStatus}).",
            txn.Id, txn.Status, txn.OrderId, txn.Order?.Status);
        if (txn.Status == PaymentStatus.Paid)
            return ServiceResult.Success("Giao dịch đã được xử lý trước đó.");

        // Một transaction duy nhất. KHÔNG retry trên cùng context: nếu transaction rollback,
        // EF change tracker không reset → retry sẽ phát UPDATE cho entity chưa tồn tại (0 rows).
        // Lỗi thật → trả lỗi để PayOS gọi lại (request mới = context sạch); idempotent nhờ check
        // txn.Status == Paid ở trên.
        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            var now = DateTime.UtcNow;

            if (!result.Success)
            {
                txn.Status = PaymentStatus.Failed;
                _logger.LogInformation("Giao dịch {OrderCode} thất bại: {Code} {Desc}", result.OrderCode, result.Code, result.Description);
                return null;
            }

            txn.Status = PaymentStatus.Paid;
            txn.ProviderTransactionId = result.ProviderReference;
            txn.PaidAt = now;

            var order = txn.Order;
            if (order.Status == OrderStatus.Pending || order.Status == OrderStatus.Expired)
            {
                if (order.Status == OrderStatus.Expired)
                {
                    // Đơn đã bị job chuyển Expired (kho đã hoàn) — trừ lại kho vì nhận được thanh toán.
                    var productItems = await _uow.Orders.GetProductItemsAsync(order.Items.Select(i => i.ProductItemId).Distinct(), ct);
                    var byId = productItems.ToDictionary(p => p.Id);
                    foreach (var item in order.Items)
                        if (byId.TryGetValue(item.ProductItemId, out var pi))
                            pi.Stock -= item.Quantity;
                }

                order.Status = OrderStatus.Paid;
                order.StatusChangeNote = "Thanh toán PayOS thành công";

                // Đơn online: delivery chỉ tạo sau khi đã thanh toán — lưu delivery trước, gắn item sau.
                if (order.Deliveries.Count == 0)
                    await CreateAndLinkDeliveriesAsync(order, ct);

                // Khôi phục delivery nếu đã bị huỷ lúc expire.
                foreach (var delivery in order.Deliveries.Where(d => d.Status == DeliveryStatus.Cancelled))
                {
                    delivery.Status = DeliveryStatus.Pending;
                    delivery.ProgressLogs.Add(new DeliveryProgressLog
                    {
                        DeliveryId = delivery.Id,
                        SourceType = DeliverySource.System,
                        FromStatus = DeliveryStatus.Cancelled.ToString(),
                        ToStatus = DeliveryStatus.Pending.ToString(),
                        Note = "Khôi phục giao hàng do nhận thanh toán trễ",
                        LoggedAt = now,
                    });
                }

                await CreateShipmentsAsync(order, now, ct);

                var rolled = OrderWorkflow.ComputeOrderStatus(order.Deliveries.Select(d => d.Status).ToList());
                if (rolled != order.Status)
                {
                    order.Status = rolled;
                    order.StatusChangeNote = "Đã tạo vận đơn cho các nhà vườn";
                }
            }
            else if (order.Status == OrderStatus.Cancelled)
            {
                // Trả tiền sau khi đơn đã hết hạn/hủy (kho đã hoàn) — không revive, cần đối soát hoàn tiền thủ công.
                _logger.LogError("Nhận thanh toán cho order {OrderId} đã {Status} — cần hoàn tiền thủ công cho giao dịch {OrderCode}.",
                    order.Id, order.Status, txn.OrderCode);
            }
            else
            {
                _logger.LogWarning("Order {OrderId} không ở trạng thái Pending khi nhận thanh toán ({Status}).", order.Id, order.Status);
            }

            return null;
        }, ct);

        _logger.LogInformation("[Webhook] Đã xử lý xong orderCode {OrderCode}: transaction={TxnStatus}, order={OrderStatus}.",
            result.OrderCode, txn.Status, txn.Order?.Status);
        return ServiceResult.Success("Đã xử lý webhook thanh toán.");
    }

    public async Task<IServiceResult<PaymentStatusResponse>> CancelPaymentAsync(Guid orderId, Guid userId, string? reason, CancellationToken ct = default)
    {
        var order = await _uow.Orders.GetWithGraphAsync(orderId, userId, ct);
        if (order is null)
            return ServiceResult<PaymentStatusResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy đơn hàng.");
        if (order.Status != OrderStatus.Pending)
            return ServiceResult<PaymentStatusResponse>.Failure(ApiStatusCodes.BadRequest, "Chỉ hủy được khi đơn đang chờ thanh toán.");

        var txn = await _uow.Transactions.GetLatestByOrderAsync(orderId, ct);
        if (txn is null)
            return ServiceResult<PaymentStatusResponse>.Failure(ApiStatusCodes.BadRequest, "Đơn chưa có giao dịch thanh toán để hủy.");
        if (await _uow.Transactions.HasPaidAsync(orderId, ct))
            return ServiceResult<PaymentStatusResponse>.Failure(ApiStatusCodes.BadRequest, "Đơn đã thanh toán, không thể hủy thanh toán.");

        // Hủy mọi link/giao dịch còn treo + đơn + hoàn kho — dùng chung lõi hủy với OrderService.
        await _cancellation.CancelAsync(order, userId,
            string.IsNullOrWhiteSpace(reason) ? "Hủy thanh toán" : $"Hủy thanh toán: {reason}",
            expired: false, ct);

        return ServiceResult<PaymentStatusResponse>.Success(new PaymentStatusResponse
        {
            OrderId = orderId,
            OrderStatus = OrderStatus.Cancelled,
            OrderCode = txn.OrderCode,
            PaymentStatus = PaymentStatus.Cancelled,
            Amount = txn.Amount,
            ProviderTransactionId = txn.ProviderTransactionId,
            PaidAt = txn.PaidAt,
        }, "Đã hủy thanh toán và hủy đơn hàng.");
    }

    public async Task<IServiceResult<PaymentStatusResponse>> GetStatusAsync(Guid orderId, Guid userId, CancellationToken ct = default)
    {
        var order = await _uow.Orders.GetDetailAsync(orderId, userId, ct);
        if (order is null)
            return ServiceResult<PaymentStatusResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy đơn hàng.");

        var txn = await _uow.Transactions.GetLatestByOrderAsync(orderId, ct);
        return ServiceResult<PaymentStatusResponse>.Success(new PaymentStatusResponse
        {
            OrderId = orderId,
            OrderStatus = order.Status,
            OrderCode = txn?.OrderCode,
            PaymentStatus = txn?.Status,
            Amount = txn?.Amount,
            ProviderTransactionId = txn?.ProviderTransactionId,
            PaidAt = txn?.PaidAt,
        });
    }

    /// <summary>
    /// Tạo delivery theo store rồi <b>lưu ngay</b> (deliveries chỉ phụ thuộc order đã tồn tại),
    /// sau đó mới gắn <c>order_items.delivery_id</c>. Đảm bảo deliveries đã có trong DB (cùng
    /// transaction) trước khi order_items tham chiếu → tránh vi phạm FK khi đơn online tạo
    /// delivery lúc thanh toán (re-parent item cũ sang delivery mới).
    /// </summary>
    private async Task CreateAndLinkDeliveriesAsync(Order order, CancellationToken ct)
    {
        var byStore = new Dictionary<Guid, Delivery>();
        foreach (var storeId in order.Items.Select(i => i.ProductItem.Product.GardenStoreId).Distinct())
        {
            var delivery = new Delivery
            {
                OrderId = order.Id,
                GardenStoreId = storeId,
                Status = DeliveryStatus.Pending,
                ShippingFee = 0m,
            };
            byStore[storeId] = delivery;
            order.Deliveries.Add(delivery);
        }

        // Pha 1: Add tường minh qua DbSet (chắc chắn Added) + LƯU NGAY để INSERT deliveries
        // trước khi order_items tham chiếu — tránh FK + tránh EF phát nhầm UPDATE (0 rows).
        await _uow.Orders.AddDeliveriesAsync(byStore.Values, ct);
        await _uow.SaveChangesAsync(ct);

        // Pha 2: gắn item vào delivery theo store + cộng subtotal (deliveries đã tồn tại trong transaction)
        foreach (var item in order.Items)
        {
            var delivery = byStore[item.ProductItem.Product.GardenStoreId];
            item.DeliveryId = delivery.Id;
            item.Delivery = delivery;
            delivery.Subtotal += item.UnitPrice * item.Quantity;
        }
    }

    private async Task CreateShipmentsAsync(Order order, DateTime now, CancellationToken ct)
    {
        foreach (var delivery in order.Deliveries.Where(d => d.Status == DeliveryStatus.Pending))
        {
            var shipment = await _shipping.CreateShipmentAsync(
                new ShipmentRequest(delivery.Id, order.Id, delivery.Subtotal, null, null, null), ct);

            delivery.ShippingProvider = shipment.Provider;
            delivery.ProviderOrderId = shipment.ProviderOrderId;
            delivery.TrackingCode = shipment.TrackingCode;
            delivery.EstimatedDeliveryDate = shipment.EstimatedDeliveryDate;
            delivery.AssignedAt = now;
            delivery.Status = DeliveryStatus.Confirmed;

            delivery.ProgressLogs.Add(new DeliveryProgressLog
            {
                DeliveryId = delivery.Id,
                SourceType = DeliverySource.System,
                FromStatus = DeliveryStatus.Pending.ToString(),
                ToStatus = DeliveryStatus.Confirmed.ToString(),
                Note = $"Tạo vận đơn {shipment.Provider} ({shipment.TrackingCode})",
                LoggedAt = now,
            });
        }
    }

    private static long GenerateOrderCode()
        => DateTimeOffset.UtcNow.ToUnixTimeSeconds() * 1000 + Random.Shared.Next(0, 1000);

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
