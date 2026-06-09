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
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(IUnitOfWork uow, IPaymentGateway gateway, IShippingProvider shipping, ILogger<PaymentService> logger)
    {
        _uow = uow;
        _gateway = gateway;
        _shipping = shipping;
        _logger = logger;
    }

    public async Task<IServiceResult<CreatePaymentResponse>> CreatePaymentAsync(Guid orderId, Guid userId, CancellationToken ct = default)
    {
        var order = await _uow.Orders.GetDetailAsync(orderId, userId, ct);
        if (order is null)
            return ServiceResult<CreatePaymentResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy đơn hàng.");
        if (order.Status != OrderStatus.Pending)
            return ServiceResult<CreatePaymentResponse>.Failure(ApiStatusCodes.BadRequest, "Đơn hàng không ở trạng thái chờ thanh toán.");
        if (await _uow.Transactions.HasPaidAsync(orderId, ct))
            return ServiceResult<CreatePaymentResponse>.Failure(ApiStatusCodes.BadRequest, "Đơn hàng đã được thanh toán.");

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

        var txn = await _uow.Transactions.GetByOrderCodeAsync(result.OrderCode, ct);
        if (txn is null)
        {
            _logger.LogWarning("Webhook PayOS orderCode {OrderCode} không khớp giao dịch nào.", result.OrderCode);
            return ServiceResult.Success("Đã nhận webhook (không khớp giao dịch).");
        }
        if (txn.Status == PaymentStatus.Paid)
            return ServiceResult.Success("Giao dịch đã được xử lý trước đó.");

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
            if (order.Status == OrderStatus.Pending)
            {
                order.Status = OrderStatus.Paid;
                order.StatusLogs.Add(new OrderStatusLog
                {
                    FromStatus = OrderStatus.Pending.ToString(),
                    ToStatus = OrderStatus.Paid.ToString(),
                    ChangedAt = now,
                    Note = "Thanh toán PayOS thành công",
                });

                await CreateShipmentsAsync(order, now, ct);

                var rolled = OrderWorkflow.ComputeOrderStatus(order.Deliveries.Select(d => d.Status).ToList());
                if (rolled != order.Status)
                {
                    order.StatusLogs.Add(new OrderStatusLog
                    {
                        FromStatus = order.Status.ToString(),
                        ToStatus = rolled.ToString(),
                        ChangedAt = now,
                        Note = "Đã tạo vận đơn cho các nhà vườn",
                    });
                    order.Status = rolled;
                }
            }
            else
            {
                _logger.LogWarning("Order {OrderId} không ở trạng thái Pending khi nhận thanh toán ({Status}).", order.Id, order.Status);
            }

            return null;
        }, ct);

        return ServiceResult.Success("Đã xử lý webhook thanh toán.");
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
