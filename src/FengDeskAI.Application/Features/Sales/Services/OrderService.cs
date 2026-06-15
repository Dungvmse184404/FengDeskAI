using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Sales.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Entities.Shipping;
using FengDeskAI.Domain.Enums.Notification;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Domain.Enums.Shipping;
using FengDeskAI.Domain.Entities.Announcement;

namespace FengDeskAI.Application.Features.Sales.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IOrderCancellationService _cancellation;

    public OrderService(IUnitOfWork uow, IMapper mapper, IOrderCancellationService cancellation)
    {
        _uow = uow;
        _mapper = mapper;
        _cancellation = cancellation;
    }

    public async Task<IServiceResult<OrderDetailResponse>> CheckoutAsync(Guid userId, CheckoutRequest request, CancellationToken ct = default)
    {
        var cart = await _uow.Carts.GetByCustomerAsync(userId, ct);

        // Địa chỉ: có gửi id thì phải hợp lệ (thuộc user); không gửi thì dùng địa chỉ mặc định.
        UserAddress? address;
        if (request.ShippingAddressId is { } addressId && addressId != Guid.Empty)
        {
            address = await _uow.UserAddresses.GetByIdForUserAsync(addressId, userId, ct);
            if (address is null)
                return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.BadRequest, "Địa chỉ giao hàng không hợp lệ.");
        }
        else
        {
            address = await _uow.UserAddresses.GetDefaultForUserAsync(userId, ct);
            if (address is null)
                return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.BadRequest, "Chưa có địa chỉ giao hàng. Vui lòng thêm địa chỉ hoặc đặt một địa chỉ mặc định.");
        }

        // Xác định danh sách dòng cần đặt:
        //  - request.Items (productItemId + quantity): mua ngay, KHÔNG cần có trong giỏ.
        //  - Items trống: đặt toàn bộ giỏ hàng.
        List<(ProductItem Pi, int Quantity)> lines;

        if (request.Items is { Count: > 0 })
        {
            var qtyById = request.Items
                .GroupBy(x => x.ProductItemId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            if (qtyById.Values.Any(q => q <= 0))
                return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.BadRequest, "Số lượng phải lớn hơn 0.");

            var productItems = await _uow.Carts.GetProductItemsAsync(qtyById.Keys, ct);
            if (productItems.Count != qtyById.Count)
                return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.BadRequest, "Một số sản phẩm không tồn tại.");

            lines = productItems.Select(pi => (pi, qtyById[pi.Id])).ToList();
        }
        else
        {
            if (cart is null || cart.Items.Count == 0)
                return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.BadRequest, "Giỏ hàng trống.");

            lines = cart.Items.Select(ci => (ci.ProductItem, ci.Quantity)).ToList();
        }

        if (lines.Count == 0)
            return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.BadRequest, "Chưa chọn sản phẩm nào để đặt.");

        if (request.PaymentMethod is not (PaymentMethod.PayOS or PaymentMethod.COD))
            return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.BadRequest, "Phương thức thanh toán không hợp lệ.");

        // Validate active + tồn kho
        foreach (var (pi, qty) in lines)
        {
            if (pi?.Product is null || !pi.Product.IsActive)
                return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.BadRequest, "Có sản phẩm đã ngừng bán. Vui lòng kiểm tra lại.");
            if (qty > pi.Stock)
                return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.BadRequest, $"Sản phẩm '{pi.Product.Name}' không đủ tồn kho (còn {pi.Stock}).");
        }

        var orderedProductItemIds = lines.Select(l => l.Pi.Id).ToHashSet();

        var orderId = await _uow.ExecuteInTransactionAsync(async _ =>
        {
            var order = new Order
            {
                CustomerId = userId,
                ShippingAddressId = address.Id,
                Status = OrderStatus.Pending,
                PaymentMethod = request.PaymentMethod,
                Note = request.Note,
            };

            foreach (var (pi, qty) in lines)
            {
                order.Items.Add(new OrderItem
                {
                    ProductItemId = pi.Id,
                    ProductItem = pi,
                    ProductName = pi.Name is null ? pi.Product.Name : $"{pi.Product.Name} - {pi.Name}",
                    UnitPrice = pi.Price,
                    Quantity = qty,
                });

                pi.Stock -= qty; // trừ kho (entity đang tracked)
            }

            // Delivery: COD tạo ngay khi đặt; đơn online chỉ tạo khi webhook báo đã thanh toán.
            if (request.PaymentMethod == PaymentMethod.COD)
                OrderWorkflow.GroupItemsIntoDeliveries(order);

            order.Subtotal = order.Items.Sum(i => i.UnitPrice * i.Quantity);
            order.TotalShippingFee = order.Deliveries.Sum(d => d.ShippingFee);
            order.TotalAmount = order.Subtotal + order.TotalShippingFee;
            order.StatusLogs.Add(new OrderStatusLog
            {
                ToStatus = OrderStatus.Pending.ToString(),
                ChangedBy = userId,
                ChangedAt = DateTime.UtcNow,
                Note = "Tạo đơn hàng",
            });

            await _uow.Orders.AddAsync(order, ct);

            // Món có trong giỏ thì xóa khỏi giỏ; món không có thì thôi.
            if (cart is not null)
            {
                var toRemove = cart.Items.Where(ci => orderedProductItemIds.Contains(ci.ProductItemId)).ToList();
                if (toRemove.Count > 0)
                    _uow.Carts.RemoveItems(toRemove);
            }

            await _uow.Notifications.AddAsync(new Notification
            {
                UserId = userId,
                Type = NotificationType.OrderPlaced,
                Title = "Đặt hàng thành công",
                Message = request.PaymentMethod == PaymentMethod.COD
                    ? "Đơn hàng của bạn đã được đặt. Vui lòng chờ xác nhận từ cửa hàng."
                    : "Đơn hàng của bạn đã được đặt. Vui lòng thanh toán để xác nhận đơn hàng.",
                ReferenceId = order.Id,
                ReferenceType = "Order",
                IsRead = false,
            }, ct);

            return order.Id;
        }, ct);

        return await GetByIdAsync(orderId, userId, ct);
    }

    public async Task<IServiceResult<PagedResult<OrderListItemResponse>>> GetMineAsync(Guid userId, PageRequest page, CancellationToken ct = default)
    {
        var (orders, total) = await _uow.Orders.GetByCustomerAsync(userId, page.Skip, page.PageSize, ct);
        var items = _mapper.Map<List<OrderListItemResponse>>(orders);
        return ServiceResult<PagedResult<OrderListItemResponse>>.Success(
            new PagedResult<OrderListItemResponse>(items, page.Page, page.PageSize, total));
    }

    public async Task<IServiceResult<OrderDetailResponse>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var order = await _uow.Orders.GetDetailAsync(id, userId, ct);
        if (order is null)
            return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy đơn hàng.");
        return ServiceResult<OrderDetailResponse>.Success(_mapper.Map<OrderDetailResponse>(order));
    }

    public async Task<IServiceResult<OrderDetailResponse>> CancelAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var order = await _uow.Orders.GetWithGraphAsync(id, userId, ct);
        if (order is null)
            return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy đơn hàng.");
        if (order.Status != OrderStatus.Pending)
            return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.BadRequest, "Chỉ có thể hủy đơn ở trạng thái chờ xử lý.");
        if (await _uow.Transactions.HasPaidAsync(id, ct))
            return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.BadRequest, "Đơn đã thanh toán, không thể hủy trực tiếp.");

        // Hủy cả giao dịch thanh toán còn treo + link PayOS (nếu có) — tránh đơn đã hủy mà vẫn trả tiền được.
        await _cancellation.CancelAsync(order, userId, "Khách hàng hủy đơn", expired: false, ct);

        return await GetByIdAsync(id, userId, ct);
    }

    public async Task<IServiceResult<PagedResult<StoreDeliveryResponse>>> GetStoreDeliveriesAsync(Guid storeId, Guid userId, bool isAdmin, PageRequest page, CancellationToken ct = default)
    {
        if (!isAdmin && !await _uow.Stores.CanManageAsync(storeId, userId, ct))
            return ServiceResult<PagedResult<StoreDeliveryResponse>>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền xem đơn giao của cửa hàng này.");

        var (deliveries, total) = await _uow.Orders.GetDeliveriesForStoreAsync(storeId, page.Skip, page.PageSize, ct);
        var items = _mapper.Map<List<StoreDeliveryResponse>>(deliveries);
        return ServiceResult<PagedResult<StoreDeliveryResponse>>.Success(
            new PagedResult<StoreDeliveryResponse>(items, page.Page, page.PageSize, total));
    }

    public async Task<IServiceResult<DeliveryResponse>> UpdateDeliveryStatusAsync(Guid deliveryId, Guid userId, bool isAdmin, UpdateDeliveryStatusRequest request, CancellationToken ct = default)
    {
        var delivery = await _uow.Orders.GetDeliveryWithOrderAsync(deliveryId, ct);
        if (delivery is null)
            return ServiceResult<DeliveryResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy đơn giao.");
        if (!isAdmin && !await _uow.Stores.CanManageAsync(delivery.GardenStoreId, userId, ct))
            return ServiceResult<DeliveryResponse>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền cập nhật đơn giao này.");
        if (!OrderWorkflow.IsValidDeliveryTransition(delivery.Status, request.Status))
            return ServiceResult<DeliveryResponse>.Failure(ApiStatusCodes.BadRequest, $"Không thể chuyển trạng thái từ {delivery.Status} sang {request.Status}.");

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            var fromStatus = delivery.Status;
            delivery.Status = request.Status;
            if (request.TrackingCode is not null) delivery.TrackingCode = request.TrackingCode;
            if (request.ShippingProvider is not null) delivery.ShippingProvider = request.ShippingProvider;

            var now = DateTime.UtcNow;
            switch (request.Status)
            {
                case DeliveryStatus.Confirmed: delivery.AssignedAt = now; break;
                case DeliveryStatus.Shipped: delivery.ShippedAt = now; break;
                case DeliveryStatus.Delivered: delivery.DeliveredAt = now; break;
            }

            delivery.ProgressLogs.Add(new DeliveryProgressLog
            {
                DeliveryId = delivery.Id,
                SourceType = DeliverySource.Manual,
                FromStatus = fromStatus.ToString(),
                ToStatus = request.Status.ToString(),
                Note = request.Note,
                LoggedAt = now,
            });

            var preRollupStatus = delivery.Order.Status;
            RecomputeOrderStatus(delivery.Order, userId);

            var (nType, nTitle, nMsg) = MapDeliveryNotification(request.Status);
            await _uow.Notifications.AddAsync(new Notification
            {
                UserId = delivery.Order.CustomerId,
                Type = nType,
                Title = nTitle,
                Message = nMsg,
                ReferenceId = delivery.Id,
                ReferenceType = "Delivery",
                IsRead = false,
            }, ct);

            if (delivery.Order.Status == OrderStatus.Completed && preRollupStatus != OrderStatus.Completed)
                await _uow.Notifications.AddAsync(new Notification
                {
                    UserId = delivery.Order.CustomerId,
                    Type = NotificationType.OrderCompleted,
                    Title = "Hoàn thành đơn hàng",
                    Message = "Đơn hàng của bạn đã hoàn thành. Cảm ơn bạn đã mua sắm!",
                    ReferenceId = delivery.Order.Id,
                    ReferenceType = "Order",
                    IsRead = false,
                }, ct);

            await Task.CompletedTask;
            return null;
        }, ct);

        return ServiceResult<DeliveryResponse>.Success(_mapper.Map<DeliveryResponse>(delivery), "Cập nhật trạng thái giao hàng thành công.");
    }

    private static (NotificationType Type, string Title, string Message) MapDeliveryNotification(DeliveryStatus status)
        => status switch
        {
            DeliveryStatus.Confirmed => (NotificationType.DeliveryConfirmed, "Đơn hàng đã xác nhận", "Cửa hàng đã xác nhận đơn giao của bạn."),
            DeliveryStatus.Preparing => (NotificationType.DeliveryPreparing, "Đang chuẩn bị hàng", "Cửa hàng đang chuẩn bị hàng cho đơn giao của bạn."),
            DeliveryStatus.Shipped   => (NotificationType.DeliveryShipped,   "Đơn hàng đang giao",  "Đơn giao của bạn đang trên đường đến."),
            DeliveryStatus.Delivered => (NotificationType.DeliveryDelivered, "Giao hàng thành công","Đơn giao của bạn đã được giao thành công."),
            DeliveryStatus.Returned  => (NotificationType.DeliveryReturned,  "Hàng đã hoàn trả",   "Đơn giao của bạn đã được hoàn trả."),
            DeliveryStatus.Cancelled => (NotificationType.DeliveryCancelled, "Hủy giao hàng",       "Đơn giao của bạn đã bị hủy."),
            _                        => (NotificationType.SystemAlert,       "Cập nhật đơn giao",   "Trạng thái đơn giao của bạn đã thay đổi."),
        };

    private static void RecomputeOrderStatus(Order order, Guid? actorId)
    {
        var next = OrderWorkflow.ComputeOrderStatus(order.Deliveries.Select(d => d.Status).ToList());
        if (next == order.Status) return;

        var from = order.Status;
        order.Status = next;
        order.StatusLogs.Add(new OrderStatusLog
        {
            FromStatus = from.ToString(),
            ToStatus = next.ToString(),
            ChangedBy = actorId,
            ChangedAt = DateTime.UtcNow,
            Note = "Tự động cập nhật theo tiến trình giao hàng",
        });
    }
}
