using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Sales.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Entities.Shipping;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Domain.Enums.Shipping;

namespace FengDeskAI.Application.Features.Sales.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public OrderService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<IServiceResult<OrderDetailResponse>> CheckoutAsync(Guid userId, CheckoutRequest request, CancellationToken ct = default)
    {
        var cart = await _uow.Carts.GetByCustomerAsync(userId, ct);

        var address = await _uow.UserAddresses.GetByIdForUserAsync(request.ShippingAddressId, userId, ct);
        if (address is null)
        {
            address = await _uow.UserAddresses.GetDefaultForUserAsync(userId, ct);
            if (address is null)
                return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.BadRequest, "Chưa có địa chỉ giao hàng.");
        }

        // Xác định danh sách dòng cần đặt:
        //  - request.Items (productItemId + quantity): mua ngay, KHÔNG cần có trong giỏ.
        //  - không có Items: lấy từ giỏ (CartItemIds chọn lọc, hoặc cả giỏ).
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

            var cartItems = cart.Items.ToList();
            if (request.CartItemIds is { Count: > 0 })
            {
                var ids = request.CartItemIds.ToHashSet();
                cartItems = cart.Items.Where(i => ids.Contains(i.Id)).ToList();
                if (cartItems.Count != ids.Count)
                    return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.BadRequest, "Một số sản phẩm được chọn không có trong giỏ hàng.");
            }
            lines = cartItems.Select(ci => (ci.ProductItem, ci.Quantity)).ToList();
        }

        if (lines.Count == 0)
            return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.BadRequest, "Chưa chọn sản phẩm nào để đặt.");

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
                Note = request.Note,
            };

            // Mỗi store một delivery
            foreach (var group in lines.GroupBy(l => l.Pi.Product.GardenStoreId))
            {
                var delivery = new Delivery
                {
                    GardenStoreId = group.Key,
                    Status = DeliveryStatus.Pending,
                    ShippingFee = 0m,
                };

                decimal deliverySubtotal = 0m;
                foreach (var (pi, qty) in group)
                {
                    var line = new OrderItem
                    {
                        ProductItemId = pi.Id,
                        ProductName = pi.Name is null ? pi.Product.Name : $"{pi.Product.Name} - {pi.Name}",
                        UnitPrice = pi.Price,
                        Quantity = qty,
                    };
                    order.Items.Add(line);
                    delivery.Items.Add(line);
                    deliverySubtotal += pi.Price * qty;

                    pi.Stock -= qty; // trừ kho (entity đang tracked)
                }

                delivery.Subtotal = deliverySubtotal;
                order.Deliveries.Add(delivery);
            }

            order.Subtotal = order.Deliveries.Sum(d => d.Subtotal);
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

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            var from = order.Status;
            order.Status = OrderStatus.Cancelled;
            foreach (var delivery in order.Deliveries)
                delivery.Status = DeliveryStatus.Cancelled;

            // Hoàn kho
            var productItems = await _uow.Orders.GetProductItemsAsync(order.Items.Select(i => i.ProductItemId).Distinct(), ct);
            var byId = productItems.ToDictionary(p => p.Id);
            foreach (var item in order.Items)
                if (byId.TryGetValue(item.ProductItemId, out var pi))
                    pi.Stock += item.Quantity;

            order.StatusLogs.Add(new OrderStatusLog
            {
                FromStatus = from.ToString(),
                ToStatus = OrderStatus.Cancelled.ToString(),
                ChangedBy = userId,
                ChangedAt = DateTime.UtcNow,
                Note = "Khách hàng hủy đơn",
            });
            return null;
        }, ct);

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

            RecomputeOrderStatus(delivery.Order, userId);
            await Task.CompletedTask;
            return null;
        }, ct);

        return ServiceResult<DeliveryResponse>.Success(_mapper.Map<DeliveryResponse>(delivery), "Cập nhật trạng thái giao hàng thành công.");
    }

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
