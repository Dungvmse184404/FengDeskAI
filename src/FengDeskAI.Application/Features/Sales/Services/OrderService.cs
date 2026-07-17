using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Sales.DTOs;
using FengDeskAI.Application.Features.Shipping.Services;
using FengDeskAI.Application.Interfaces.External;
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
    private readonly IShippingProvider _shipping;
    private readonly IDeliveryFeeEstimator _feeEstimator;

    public OrderService(IUnitOfWork uow, IMapper mapper, IOrderCancellationService cancellation,
        IShippingProvider shipping, IDeliveryFeeEstimator feeEstimator)
    {
        _uow = uow;
        _mapper = mapper;
        _cancellation = cancellation;
        _shipping = shipping;
        _feeEstimator = feeEstimator;
    }

    public async Task<IServiceResult<OrderDetailResponse>> CheckoutAsync(Guid userId, CheckoutRequest request, CancellationToken ct = default)
    {
        var resolved = await ResolveCheckoutAsync(userId, request, validatePaymentMethod: true, ct);
        if (!resolved.IsSuccess) return ServiceResult<OrderDetailResponse>.Failure(resolved.StatusCode, resolved.Message!);
        var (address, lines, cart) = resolved.Data!;

        var orderedProductItemIds = lines.Select(l => l.Pi.Id).ToHashSet();

        // Ước tính phí ship theo từng store (gọi GHN /fee, fallback calculator) để cộng vào tổng
        // ngay lúc checkout — COD trả đúng tổng, đơn online PayOS thu cả phí ship.
        var storeFees = await ComputeStoreFeesAsync(lines, address.Id, ct);
        var feeByStore = storeFees.ToDictionary(s => s.StoreId, s => s.ShippingFee);

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
            {
                OrderWorkflow.GroupItemsIntoDeliveries(order);
                foreach (var delivery in order.Deliveries)
                    delivery.ShippingFee = feeByStore.GetValueOrDefault(delivery.GardenStoreId);
            }

            order.Subtotal = order.Items.Sum(i => i.UnitPrice * i.Quantity);
            // Tổng phí ship lấy từ ước tính theo store (đúng cho cả COD lẫn online — online chưa tạo delivery).
            order.TotalShippingFee = feeByStore.Values.Sum();
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
                ReferenceType = ReferenceType.Order,
                IsRead = false,
            }, ct);

            return order.Id;
        }, ct);

        return await GetByIdAsync(orderId, userId, isPrivileged: false, ct);
    }

    public async Task<IServiceResult<PagedResult<OrderListItemResponse>>> GetMineAsync(Guid userId, PageRequest page, CancellationToken ct = default)
    {
        var (orders, total) = await _uow.Orders.GetByCustomerAsync(userId, page.Skip, page.PageSize, ct);
        var items = _mapper.Map<List<OrderListItemResponse>>(orders);
        return ServiceResult<PagedResult<OrderListItemResponse>>.Success(
            new PagedResult<OrderListItemResponse>(items, page.Page, page.PageSize, total));
    }

    public async Task<IServiceResult<PagedResult<OrderListItemResponse>>> GetAllAsync(PageRequest page, CancellationToken ct = default)
    {
        var (orders, total) = await _uow.Orders.GetAllAsync(page.Skip, page.PageSize, ct);
        var items = _mapper.Map<List<OrderListItemResponse>>(orders);
        return ServiceResult<PagedResult<OrderListItemResponse>>.Success(
            new PagedResult<OrderListItemResponse>(items, page.Page, page.PageSize, total));
    }

    public async Task<IServiceResult<OrderDetailResponse>> GetByIdAsync(Guid id, Guid userId, bool isPrivileged, CancellationToken ct = default)
    {
        // Staff trở lên (Staff/Manager/Admin) xem được mọi đơn → bỏ lọc chủ sở hữu; Customer chỉ xem đơn của mình.
        var order = await _uow.Orders.GetDetailAsync(id, isPrivileged ? null : userId, ct);
        if (order is null)
            return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Order.NotFound);
        return ServiceResult<OrderDetailResponse>.Success(_mapper.Map<OrderDetailResponse>(order));
    }

    /// <summary>
    /// [DEV] Nếu order (của user) chưa có delivery nào thì gom Items theo store thành delivery (Pending)
    /// rồi lưu — KHÔNG gọi nhà vận chuyển. Dùng để test luồng "đã giao" khi store chưa tạo đơn gửi.
    /// Trả số delivery vừa tạo (0 nếu đã có sẵn).
    /// </summary>
    public async Task<IServiceResult<int>> EnsureDeliveriesAsync(Guid orderId, Guid userId, CancellationToken ct = default)
    {
        var order = await _uow.Orders.GetForPaymentAsync(orderId, userId, ct); // kèm Items.ProductItem.Product + Deliveries
        if (order is null)
            return ServiceResult<int>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Order.NotFound);
        if (order.Deliveries.Count > 0)
            return ServiceResult<int>.Success(0);
        if (order.Items.Count == 0)
            return ServiceResult<int>.Failure(ApiStatusCodes.BadRequest, "Đơn không có sản phẩm để tạo giao hàng.");

        var created = await _uow.ExecuteInTransactionAsync(async _ =>
        {
            // Gom theo store → delivery Pending. Add tường minh + LƯU NGAY để INSERT deliveries
            // trước khi order_items tham chiếu (tránh vi phạm FK + EF phát nhầm UPDATE 0 rows).
            var byStore = new Dictionary<Guid, Delivery>();
            foreach (var storeId in order.Items.Select(i => i.ProductItem.Product.GardenStoreId).Distinct())
                byStore[storeId] = new Delivery
                {
                    OrderId = order.Id,
                    GardenStoreId = storeId,
                    Status = DeliveryStatus.Pending,
                    ShippingFee = 0m,
                };
            await _uow.Orders.AddDeliveriesAsync(byStore.Values, ct);
            await _uow.SaveChangesAsync(ct);

            foreach (var item in order.Items)
            {
                var delivery = byStore[item.ProductItem.Product.GardenStoreId];
                item.DeliveryId = delivery.Id;
                delivery.Subtotal += item.UnitPrice * item.Quantity;
            }
            await _uow.SaveChangesAsync(ct);
            return byStore.Count;
        }, ct);

        return ServiceResult<int>.Success(created);
    }

    public async Task<IServiceResult<OrderDetailResponse>> CancelAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var order = await _uow.Orders.GetWithGraphAsync(id, userId, ct);
        if (order is null)
            return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Order.NotFound);
        if (order.Status != OrderStatus.Pending)
            return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Order.CancelOnlyPending);
        if (await _uow.Transactions.HasPaidAsync(id, ct))
            return ServiceResult<OrderDetailResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Order.CancelPaidNotAllowed);

        // Hủy cả giao dịch thanh toán còn treo + link PayOS (nếu có) — tránh đơn đã hủy mà vẫn trả tiền được.
        await _cancellation.CancelAsync(order, userId, "Khách hàng hủy đơn", expired: false, ct);

        return await GetByIdAsync(id, userId, isPrivileged: false, ct);
    }

    public async Task<IServiceResult<PagedResult<StoreDeliveryResponse>>> GetStoreDeliveriesAsync(Guid storeId, Guid userId, bool isAdmin, PageRequest page, CancellationToken ct = default)
    {
        if (!isAdmin && !await _uow.Stores.CanManageAsync(storeId, userId, ct))
            return ServiceResult<PagedResult<StoreDeliveryResponse>>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Order.ViewStoreDeliveryForbidden);

        var (deliveries, total) = await _uow.Orders.GetDeliveriesForStoreAsync(storeId, page.Skip, page.PageSize, ct);
        var items = _mapper.Map<List<StoreDeliveryResponse>>(deliveries);
        return ServiceResult<PagedResult<StoreDeliveryResponse>>.Success(
            new PagedResult<StoreDeliveryResponse>(items, page.Page, page.PageSize, total));
    }

    public async Task<IServiceResult<DeliveryResponse>> UpdateDeliveryStatusAsync(Guid deliveryId, Guid userId, bool isAdmin, UpdateDeliveryStatusRequest request, CancellationToken ct = default)
    {
        var delivery = await _uow.Orders.GetDeliveryWithOrderAsync(deliveryId, ct);
        if (delivery is null)
            return ServiceResult<DeliveryResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Order.DeliveryNotFound);
        if (!isAdmin && !await _uow.Stores.CanManageAsync(delivery.GardenStoreId, userId, ct))
            return ServiceResult<DeliveryResponse>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Order.UpdateDeliveryForbidden);
        if (!OrderWorkflow.IsValidDeliveryTransition(delivery.Status, request.Status))
            return ServiceResult<DeliveryResponse>.Failure(ApiStatusCodes.BadRequest, string.Format(ApiStatusMessages.Order.DeliveryStatusTransitionFormat, delivery.Status, request.Status));

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

            // Add tường minh qua repo (Added → INSERT). Add qua navigation vào delivery đã-tracked
            // bị EF đánh Modified (UPDATE 0 rows) vì BaseEntity set sẵn Id — xem ghi chú ở PaymentService.
            await _uow.Shipping.AddProgressLogAsync(new DeliveryProgressLog
            {
                DeliveryId = delivery.Id,
                SourceType = DeliverySource.Manual,
                FromStatus = fromStatus.ToString(),
                ToStatus = request.Status.ToString(),
                Note = request.Note,
                LoggedAt = now,
            }, ct);

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
                ReferenceType = ReferenceType.Delivery,
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
                    ReferenceType = ReferenceType.Order,
                    IsRead = false,
                }, ct);

            await Task.CompletedTask;
            return null;
        }, ct);

        return ServiceResult<DeliveryResponse>.Success(_mapper.Map<DeliveryResponse>(delivery), ApiStatusMessages.Order.DeliveryStatusUpdated);
    }

    /// <summary>
    /// Tính phí ship theo từng store cho các dòng hàng (dùng chung cho checkout + xem trước phí). Gom store
    /// (điểm lấy) + địa chỉ khách (điểm giao) read-only rồi gọi <see cref="IDeliveryFeeEstimator"/> cho mỗi store.
    /// </summary>
    private async Task<List<StoreShippingFee>> ComputeStoreFeesAsync(
        List<(ProductItem Pi, int Quantity)> lines, Guid shippingAddressId, CancellationToken ct)
    {
        var shipTo = await _uow.UserAddresses.GetWithWardChainAsync(shippingAddressId, ct);
        var storeIds = lines.Select(l => l.Pi.Product.GardenStoreId).Distinct().ToList();
        var stores = (await _uow.Stores.GetWithAddressByIdsAsync(storeIds, ct)).ToDictionary(s => s.Id);

        var result = new List<StoreShippingFee>();
        foreach (var group in lines.GroupBy(l => l.Pi.Product.GardenStoreId))
        {
            var items = group.Select(x => new ShipmentItem(
                x.Pi.Id.ToString(),
                x.Pi.Name is null ? x.Pi.Product.Name : $"{x.Pi.Product.Name} - {x.Pi.Name}",
                x.Pi.Price, x.Quantity,
                x.Pi.WeightGram, x.Pi.LengthCm, x.Pi.WidthCm, x.Pi.HeightCm)).ToList();
            var weight = group.Sum(x => x.Pi.WeightGram * x.Quantity);
            var subtotal = group.Sum(x => x.Pi.Price * x.Quantity);
            stores.TryGetValue(group.Key, out var store);
            var fee = await _feeEstimator.EstimateAsync(store, shipTo, subtotal, weight, items, ct);
            result.Add(new StoreShippingFee(group.Key, store?.Name ?? string.Empty, subtotal, fee));
        }
        return result;
    }

    private sealed record StoreShippingFee(Guid StoreId, string StoreName, decimal Subtotal, decimal ShippingFee);

    private sealed record CheckoutContext(
        UserAddress Address, List<(ProductItem Pi, int Quantity)> Lines, Cart? Cart);

    /// <summary>
    /// Phân giải địa chỉ giao + danh sách dòng hàng + validate (tồn kho/active) dùng chung cho checkout và
    /// xem trước phí ship. <paramref name="validatePaymentMethod"/>=false khi chỉ xem trước (chưa chọn PTTT).
    /// </summary>
    private async Task<IServiceResult<CheckoutContext>> ResolveCheckoutAsync(
        Guid userId, CheckoutRequest request, bool validatePaymentMethod, CancellationToken ct)
    {
        var cart = await _uow.Carts.GetByCustomerAsync(userId, ct);

        // Địa chỉ: có gửi id thì phải hợp lệ (thuộc user); không gửi thì dùng địa chỉ mặc định.
        UserAddress? address;
        if (request.ShippingAddressId is { } addressId && addressId != Guid.Empty)
        {
            address = await _uow.UserAddresses.GetByIdForUserAsync(addressId, userId, ct);
            if (address is null)
                return ServiceResult<CheckoutContext>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Order.ShippingAddressInvalid);
        }
        else
        {
            address = await _uow.UserAddresses.GetDefaultForUserAsync(userId, ct);
            if (address is null)
                return ServiceResult<CheckoutContext>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Order.NoShippingAddress);
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
                return ServiceResult<CheckoutContext>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Order.QuantityInvalid);

            var productItems = await _uow.Carts.GetProductItemsAsync(qtyById.Keys, ct);
            if (productItems.Count != qtyById.Count)
                return ServiceResult<CheckoutContext>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Order.SomeProductsNotExist);

            lines = productItems.Select(pi => (pi, qtyById[pi.Id])).ToList();
        }
        else
        {
            if (cart is null || cart.Items.Count == 0)
                return ServiceResult<CheckoutContext>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Order.CartEmpty);

            lines = cart.Items.Select(ci => (ci.ProductItem, ci.Quantity)).ToList();
        }

        if (lines.Count == 0)
            return ServiceResult<CheckoutContext>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Order.NoProductsSelected);

        if (validatePaymentMethod && request.PaymentMethod is not (PaymentMethod.PayOS or PaymentMethod.COD))
            return ServiceResult<CheckoutContext>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Order.PaymentMethodInvalid);

        // Validate active + tồn kho
        foreach (var (pi, qty) in lines)
        {
            if (pi?.Product is null || !pi.Product.IsActive)
                return ServiceResult<CheckoutContext>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Order.SomeProductsDiscontinued);
            if (qty > pi.Stock)
                return ServiceResult<CheckoutContext>.Failure(ApiStatusCodes.BadRequest, string.Format(ApiStatusMessages.Order.ProductOutOfStockFormat, pi.Product.Name, pi.Stock));
        }

        return ServiceResult<CheckoutContext>.Success(new CheckoutContext(address, lines, cart));
    }

    /// <summary>
    /// Xem trước phí ship (cho FE hiển thị trước khi đặt) — không tạo đơn. Cùng input như checkout
    /// (địa chỉ + items / giỏ), trả về phí từng store + tổng.
    /// </summary>
    public async Task<IServiceResult<ShippingFeePreviewResponse>> PreviewShippingFeeAsync(
        Guid userId, CheckoutRequest request, CancellationToken ct = default)
    {
        var resolved = await ResolveCheckoutAsync(userId, request, validatePaymentMethod: false, ct);
        if (!resolved.IsSuccess) return ServiceResult<ShippingFeePreviewResponse>.Failure(resolved.StatusCode, resolved.Message!);
        var (address, lines, _) = resolved.Data!;

        var storeFees = await ComputeStoreFeesAsync(lines, address.Id, ct);
        var subtotal = storeFees.Sum(s => s.Subtotal);
        var shipping = storeFees.Sum(s => s.ShippingFee);

        return ServiceResult<ShippingFeePreviewResponse>.Success(new ShippingFeePreviewResponse
        {
            Subtotal = subtotal,
            TotalShippingFee = shipping,
            TotalAmount = subtotal + shipping,
            Stores = storeFees.Select(s => new StoreShippingFeeResponse
            {
                StoreId = s.StoreId,
                StoreName = s.StoreName,
                Subtotal = s.Subtotal,
                ShippingFee = s.ShippingFee,
            }).ToList(),
        });
    }

    /// <summary>
    /// Gọi nhà vận chuyển cho một delivery và gắn thông tin vận đơn (tracking, fee) vào entity.
    /// KHÔNG đổi <c>Status</c> và KHÔNG ghi <c>DeliveryProgressLog</c> — caller quyết định ngữ cảnh
    /// chuyển trạng thái (hiện tại là Confirmed→Preparing trong <see cref="CreateDeliveryShipmentAsync"/>).
    /// </summary>
    private async Task CreateShipmentForDeliveryAsync(Delivery delivery, CancellationToken ct)
    {
        var store = (await _uow.Stores.GetWithAddressByIdsAsync(new[] { delivery.GardenStoreId }, ct)).FirstOrDefault();
        var shipTo = await _uow.UserAddresses.GetWithWardChainAsync(delivery.Order.ShippingAddressId, ct);

        var items = delivery.Items
            .Select(i => new ShipmentItem(i.ProductItemId.ToString(), i.ProductName, i.UnitPrice, i.Quantity,
                i.ProductItem.WeightGram, i.ProductItem.LengthCm, i.ProductItem.WidthCm, i.ProductItem.HeightCm))
            .ToList();
        var weightGram = delivery.Items.Sum(i => i.ProductItem.WeightGram * i.Quantity);

        // COD: thu tiền tại điểm giao; nếu đơn đã thu online thì CodAmount = 0.
        var cod = delivery.Order.PaymentMethod == PaymentMethod.COD
            ? delivery.Subtotal + delivery.ShippingFee
            : 0m;

        var request = ShipmentRequestBuilder.Build(
            delivery.Id, delivery.OrderId, delivery.Subtotal, store, shipTo, cod, weightGram, items);
        var shipment = await _shipping.CreateShipmentAsync(request, ct);

        delivery.ShippingProvider = shipment.Provider;
        delivery.ProviderOrderId = shipment.ProviderOrderId;
        delivery.TrackingCode = shipment.TrackingCode;
        delivery.TrackingUrl = shipment.TrackingUrl;
        delivery.EstimatedDeliveryDate = shipment.EstimatedDeliveryDate;
        // Phí ship thực tế nhà vận chuyển trả về (order.TotalShippingFee đã thu lúc checkout theo ước tính).
        if (shipment.ShippingFee is { } actualFee) delivery.ShippingFee = actualFee;
    }

    /// <summary>
    /// Garden owner bấm "Tạo đơn ship" — delivery đang Confirmed mới được gọi nhà vận chuyển.
    /// Sau khi gọi: ghi tracking + chuyển trạng thái Confirmed → Preparing, log + notify khách.
    /// </summary>
    public async Task<IServiceResult<DeliveryResponse>> CreateDeliveryShipmentAsync(Guid deliveryId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var delivery = await _uow.Orders.GetDeliveryWithOrderAsync(deliveryId, ct);
        if (delivery is null)
            return ServiceResult<DeliveryResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Order.DeliveryNotFound);
        if (!isAdmin && !await _uow.Stores.CanManageAsync(delivery.GardenStoreId, userId, ct))
            return ServiceResult<DeliveryResponse>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Order.UpdateDeliveryForbidden);
        if (delivery.Status != DeliveryStatus.Confirmed)
            return ServiceResult<DeliveryResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Order.DeliveryNotConfirmed);
        if (!string.IsNullOrEmpty(delivery.ProviderOrderId))
            return ServiceResult<DeliveryResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Order.ShipmentAlreadyCreated);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            await CreateShipmentForDeliveryAsync(delivery, ct);

            var now = DateTime.UtcNow;
            delivery.Status = DeliveryStatus.Preparing;

            await _uow.Shipping.AddProgressLogAsync(new DeliveryProgressLog
            {
                DeliveryId = delivery.Id,
                SourceType = DeliverySource.System,
                FromStatus = DeliveryStatus.Confirmed.ToString(),
                ToStatus = DeliveryStatus.Preparing.ToString(),
                Note = $"Tạo vận đơn {delivery.ShippingProvider} ({delivery.TrackingCode})",
                LoggedAt = now,
            }, ct);

            await _uow.Notifications.AddAsync(new Notification
            {
                UserId = delivery.Order.CustomerId,
                Type = NotificationType.DeliveryPreparing,
                Title = "Đang chuẩn bị hàng",
                Message = "Cửa hàng đã tạo vận đơn và đang chuẩn bị hàng cho đơn giao của bạn.",
                ReferenceId = delivery.Id,
                ReferenceType = ReferenceType.Delivery,
                IsRead = false,
            }, ct);

            return null;
        }, ct);

        return ServiceResult<DeliveryResponse>.Success(_mapper.Map<DeliveryResponse>(delivery), ApiStatusMessages.Order.ShipmentCreated);
    }

    public async Task<IServiceResult<DeliveryOrderDetailResponse>> GetDeliveryDetailAsync(Guid deliveryId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var delivery = await _uow.Orders.GetDeliveryDetailAsync(deliveryId, ct);
        if (delivery is null)
            return ServiceResult<DeliveryOrderDetailResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Order.DeliveryNotFound);
        if (!isAdmin && !await _uow.Stores.CanManageAsync(delivery.GardenStoreId, userId, ct))
            return ServiceResult<DeliveryOrderDetailResponse>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Order.ViewStoreDeliveryForbidden);

        var order = delivery.Order;
        var address = order.ShippingAddress;
        var response = new DeliveryOrderDetailResponse
        {
            Id = delivery.Id,
            GardenStoreId = delivery.GardenStoreId,
            StoreName = delivery.Store?.Name,
            Status = delivery.Status,
            ShippingFee = delivery.ShippingFee,
            Subtotal = delivery.Subtotal,
            TrackingCode = delivery.TrackingCode,
            ShippingProvider = delivery.ShippingProvider,
            ShippedAt = delivery.ShippedAt,
            DeliveredAt = delivery.DeliveredAt,
            EstimatedDeliveryDate = delivery.EstimatedDeliveryDate,
            OrderId = delivery.OrderId,
            OrderCreatedAt = order.CreatedAt,
            PaymentMethod = order.PaymentMethod,
            OrderStatus = order.Status,
            OrderNote = order.Note,
            Items = delivery.Items.Select(i => new OrderItemResponse
            {
                Id = i.Id,
                ProductItemId = i.ProductItemId,
                DeliveryId = i.DeliveryId,
                ProductName = i.ProductName,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                LineTotal = i.UnitPrice * i.Quantity,
            }).ToList(),
            ShippingAddress = new DeliveryShippingAddressResponse
            {
                RecipientName = address.RecipientName,
                RecipientPhone = address.RecipientPhone,
                StreetAddress = address.StreetAddress,
                FullAddressText = $"{address.StreetAddress}, {address.Ward.Name}, {address.Ward.District.Name}, {address.Ward.District.Province.Name}",
            },
        };

        return ServiceResult<DeliveryOrderDetailResponse>.Success(response);
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

        // Đổi trạng thái + đặt note; OrderStatusLog do AppDbContext.ApplyAuditInformation tự sinh khi
        // phát hiện Order.Status đổi (Added → INSERT). KHÔNG add qua navigation vào order đã-tracked
        // (EF đánh Modified → UPDATE 0 rows) — đồng bộ cách PaymentService/OrderCancellationService log.
        order.Status = next;
        order.StatusChangeNote = "Tự động cập nhật theo tiến trình giao hàng";
    }
}
