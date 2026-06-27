using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Media;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Returns.DTOs;
using FengDeskAI.Application.Features.Shipping.Services;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Announcement;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Entities.Shipping;
using FengDeskAI.Domain.Enums.Notification;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Domain.Enums.Shipping;

namespace FengDeskAI.Application.Features.Returns.Services;

public class ReturnService : IReturnService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IRefundService _refund;
    private readonly IShippingProvider _shipping;
    private readonly IFileStorage _storage;

    public ReturnService(IUnitOfWork uow, IMapper mapper, IRefundService refund, IShippingProvider shipping, IFileStorage storage)
    {
        _uow = uow;
        _mapper = mapper;
        _refund = refund;
        _shipping = shipping;
        _storage = storage;
    }

    // ===================== Customer =====================

    public async Task<IServiceResult<ReturnDetailResponse>> CreateAsync(Guid userId, CreateReturnRequest request, CancellationToken ct = default)
    {
        var delivery = await _uow.Returns.GetDeliveryForReturnAsync(request.DeliveryId, ct);
        if (delivery is null || delivery.Order is null || delivery.Order.CustomerId != userId)
            return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.DeliveryNotFound);

        var now = DateTime.UtcNow;
        if (delivery.Status != DeliveryStatus.Delivered)
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.NotDelivered);
        if (!ReturnWorkflow.IsWithinWindow(delivery.DeliveredAt, now))
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.OutsideWindow);

        if (request.Items is null || request.Items.Count == 0)
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.NoItems);

        var orderItemsById = delivery.Items.ToDictionary(i => i.Id);

        // Gộp trùng dòng theo OrderItem + validate cơ bản.
        var requested = new Dictionary<Guid, (int Qty, Guid? Exchange)>();
        foreach (var line in request.Items)
        {
            if (line.Quantity <= 0)
                return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.QuantityInvalid);
            if (!orderItemsById.ContainsKey(line.OrderItemId))
                return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.ItemNotInDelivery);

            if (requested.TryGetValue(line.OrderItemId, out var cur))
                requested[line.OrderItemId] = (cur.Qty + line.Quantity, cur.Exchange ?? line.ExchangeProductItemId);
            else
                requested[line.OrderItemId] = (line.Quantity, line.ExchangeProductItemId);
        }

        // Chặn trả vượt số đã mua (trừ phần đã trả ở các yêu cầu trước).
        var alreadyReturned = await _uow.Returns.GetReturnedQuantitiesAsync(requested.Keys, ct);
        foreach (var (orderItemId, info) in requested)
        {
            var oi = orderItemsById[orderItemId];
            var available = oi.Quantity - (alreadyReturned.TryGetValue(orderItemId, out var q) ? q : 0);
            if (info.Qty > available)
                return Fail(ApiStatusCodes.BadRequest,
                    string.Format(ApiStatusMessages.Returns.QuantityExceededFormat, oi.ProductName, Math.Max(0, available)));
        }

        var isCod = delivery.Order.PaymentMethod == PaymentMethod.COD;
        var refundMethod = isCod ? RefundMethod.BankTransfer : RefundMethod.Original;

        // Validate đổi hàng + chênh lệch giá.
        decimal returnedValue = 0m, replacementValue = 0m;
        if (request.Type == ReturnType.Exchange)
        {
            if (requested.Values.Any(v => v.Exchange is null))
                return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.ExchangeItemRequired);

            var exIds = requested.Values.Select(v => v.Exchange!.Value).Distinct().ToList();
            var exItems = (await _uow.Returns.GetProductItemsWithProductAsync(exIds, ct)).ToDictionary(p => p.Id);
            if (exItems.Count != exIds.Count
                || exItems.Values.Any(p => p.Product is null || p.Product.GardenStoreId != delivery.GardenStoreId))
                return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.ExchangeItemNotFound);

            foreach (var (orderItemId, info) in requested)
            {
                var oi = orderItemsById[orderItemId];
                returnedValue += oi.UnitPrice * info.Qty;
                replacementValue += exItems[info.Exchange!.Value].Price * info.Qty;
            }
            if (replacementValue > returnedValue)
                return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.ExchangeMoreExpensive);
        }

        // COD cần thông tin ngân hàng khi có tiền hoàn (trả hàng, hoặc đổi rẻ hơn).
        var hasRefund = request.Type == ReturnType.Refund || (request.Type == ReturnType.Exchange && replacementValue < returnedValue);
        if (isCod && hasRefund && (string.IsNullOrWhiteSpace(request.BankAccountNumber)
                || string.IsNullOrWhiteSpace(request.BankAccountName) || string.IsNullOrWhiteSpace(request.BankName)))
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.BankInfoRequired);

        var rr = new ReturnRequest
        {
            OrderId = delivery.OrderId,
            DeliveryId = delivery.Id,
            CustomerId = userId,
            Type = request.Type,
            Status = ReturnRequestStatus.Requested,
            Reason = request.Reason,
            ReasonDetail = request.ReasonDetail,
            RefundMethod = refundMethod,
            BankAccountName = request.BankAccountName,
            BankAccountNumber = request.BankAccountNumber,
            BankName = request.BankName,
        };

        foreach (var (orderItemId, info) in requested)
            rr.Items.Add(new ReturnItem
            {
                OrderItemId = orderItemId,
                Quantity = info.Qty,
                UnitPrice = orderItemsById[orderItemId].UnitPrice,
                ExchangeProductItemId = info.Exchange,
            });

        rr.RefundAmount = request.Type == ReturnType.Refund
            ? ReturnWorkflow.ComputeRefundAmount(rr.Items)
            : Math.Max(0m, returnedValue - replacementValue);

        if (request.ImageUrls is { Count: > 0 })
        {
            var sort = 0;
            foreach (var url in request.ImageUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
                rr.Images.Add(new ReturnRequestImage { ImageUrl = url, SortOrder = sort++ });
        }

        rr.StatusLogs.Add(new ReturnStatusLog
        {
            FromStatus = null,
            ToStatus = ReturnRequestStatus.Requested.ToString(),
            ChangedBy = userId,
            Note = "Tạo yêu cầu trả hàng/đổi trả",
            ChangedAt = now,
        });

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            await _uow.Returns.AddAsync(rr, ct);
            await NotifyAsync(userId, NotificationType.ReturnRequested, "Đã gửi yêu cầu trả hàng",
                "Yêu cầu trả hàng/đổi trả của bạn đã được gửi và đang chờ cửa hàng duyệt.",
                rr.Id, ReferenceType.Return, ct);
            return null;
        }, ct);

        return await GetByIdAsync(rr.Id, userId, isAdmin: false, ct);
    }

    public async Task<IServiceResult<PagedResult<ReturnListItemResponse>>> GetMineAsync(Guid userId, PageRequest page, CancellationToken ct = default)
    {
        var (items, total) = await _uow.Returns.GetByCustomerAsync(userId, page.Skip, page.PageSize, ct);
        return Paged(items, page, total);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> GetByIdAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var rr = await _uow.Returns.GetDetailAsync(id, null, ct);
        if (rr is null)
            return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.NotFound);

        var authorized = rr.CustomerId == userId || isAdmin
            || await _uow.Stores.CanManageAsync(rr.Delivery.GardenStoreId, userId, ct);
        if (!authorized)
            return Fail(ApiStatusCodes.Forbidden, ApiStatusMessages.Returns.ViewForbidden);

        return ServiceResult<ReturnDetailResponse>.Success(_mapper.Map<ReturnDetailResponse>(rr));
    }

    public async Task<IServiceResult<ReturnDetailResponse>> CancelAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var rr = await _uow.Returns.GetWithGraphAsync(id, ct);
        if (rr is null || rr.CustomerId != userId)
            return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.NotFound);
        if (rr.Status is not (ReturnRequestStatus.Requested or ReturnRequestStatus.Approved))
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.CancelNotAllowed);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            Transition(rr, ReturnRequestStatus.Cancelled, userId, "Khách hủy yêu cầu");
            await NotifyAsync(rr.CustomerId, NotificationType.ReturnCancelled, "Đã hủy yêu cầu trả hàng",
                "Yêu cầu trả hàng/đổi trả của bạn đã được hủy.", rr.Id, ReferenceType.Return, ct);
            return null;
        }, ct);

        return await GetByIdAsync(id, userId, isAdmin: false, ct);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> ShipBackAsync(Guid id, Guid userId, ShipBackRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.TrackingCode))
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.NoItems);

        var rr = await _uow.Returns.GetWithGraphAsync(id, ct);
        if (rr is null || rr.CustomerId != userId)
            return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.NotFound);
        if (!ReturnWorkflow.IsValidReturnTransition(rr.Status, ReturnRequestStatus.ReturnInTransit))
            return InvalidTransition(rr.Status, ReturnRequestStatus.ReturnInTransit);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            rr.ReturnTrackingCode = request.TrackingCode;
            Transition(rr, ReturnRequestStatus.ReturnInTransit, userId, $"Khách gửi hàng trả (mã VĐ: {request.TrackingCode})");
            await Task.CompletedTask;
            return null;
        }, ct);

        return await GetByIdAsync(id, userId, isAdmin: false, ct);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> UploadImagesAsync(Guid id, Guid userId, IReadOnlyList<ReturnImageFile> files, CancellationToken ct = default)
    {
        if (files is null || files.Count == 0)
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.ImageFileRequired);

        // GetDetailAsync(id, userId): chỉ trả về nếu userId là CHỦ yêu cầu → kiêm luôn kiểm quyền sở hữu.
        var rr = await _uow.Returns.GetDetailAsync(id, userId, ct);
        if (rr is null)
            return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.NotFound);

        var sort = rr.Images.Count;
        foreach (var file in files)
        {
            if (file.Content is null || file.Content.Length == 0)
                return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.ImageFileRequired);
            if (!ImageUpload.IsAllowed(file.ContentType))
                return Fail(ApiStatusCodes.UnprocessableEntity, ApiStatusMessages.Returns.ImageTypeInvalid);

            // Mỗi yêu cầu một thư mục riêng: Return_images/{returnId}/{guid}{ext}
            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ImageUpload.ExtensionFor(file.ContentType);
            var objectPath = $"Return_images/{id}/{Guid.NewGuid():N}{ext}";

            var stored = await _storage.UploadAsync(objectPath, file.Content, file.ContentType, ct);
            await _uow.Returns.AddImageAsync(new ReturnRequestImage
            {
                ReturnRequestId = id,
                ImageUrl = stored.Url,
                SortOrder = sort++,
            }, ct);
        }
        await _uow.SaveChangesAsync(ct);

        return await GetByIdAsync(id, userId, isAdmin: false, ct);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> DeleteImageAsync(Guid id, Guid imageId, Guid userId, CancellationToken ct = default)
    {
        // Owner-scoped: null nếu không phải chủ yêu cầu.
        var rr = await _uow.Returns.GetDetailAsync(id, userId, ct);
        if (rr is null)
            return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.NotFound);
        if (rr.Status != ReturnRequestStatus.Requested)
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.ImageDeleteNotAllowed);

        var image = await _uow.Returns.GetImageAsync(id, imageId, ct);
        if (image is null)
            return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.ImageNotFound);

        _uow.Returns.RemoveImage(image);
        await _uow.SaveChangesAsync(ct);
        // Xóa file trên storage best-effort sau khi DB đã commit (không chặn nghiệp vụ nếu lỗi).
        await _storage.DeleteByUrlAsync(image.ImageUrl, ct);

        return await GetByIdAsync(id, userId, isAdmin: false, ct);
    }

    // ===================== Vendor / Admin =====================

    public async Task<IServiceResult<PagedResult<ReturnListItemResponse>>> GetForStoreAsync(Guid storeId, Guid userId, bool isAdmin, PageRequest page, CancellationToken ct = default)
    {
        if (!isAdmin && !await _uow.Stores.CanManageAsync(storeId, userId, ct))
            return ServiceResult<PagedResult<ReturnListItemResponse>>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Returns.ManageForbidden);

        var (items, total) = await _uow.Returns.GetForStoreAsync(storeId, page.Skip, page.PageSize, ct);
        return Paged(items, page, total);
    }

    public async Task<IServiceResult<PagedResult<ReturnListItemResponse>>> GetAllAsync(PageRequest page, CancellationToken ct = default)
    {
        var (items, total) = await _uow.Returns.GetAllPagedAsync(page.Skip, page.PageSize, ct);
        return Paged(items, page, total);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> ApproveAsync(Guid id, Guid userId, bool isAdmin, ApproveReturnRequest request, CancellationToken ct = default)
    {
        var (rr, error) = await LoadForManageAsync(id, userId, isAdmin, ct);
        if (error is not null) return error;
        if (!ReturnWorkflow.IsValidReturnTransition(rr!.Status, ReturnRequestStatus.Approved))
            return InvalidTransition(rr.Status, ReturnRequestStatus.Approved);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            rr.ApprovedBy = userId;
            rr.ApprovedAt = DateTime.UtcNow;
            Transition(rr, ReturnRequestStatus.Approved, userId, string.IsNullOrWhiteSpace(request.Note) ? "Đã duyệt yêu cầu" : request.Note);
            await NotifyAsync(rr.CustomerId, NotificationType.ReturnApproved, "Yêu cầu trả hàng được duyệt",
                "Cửa hàng đã duyệt yêu cầu. Vui lòng gửi hàng trả về theo hướng dẫn.", rr.Id, ReferenceType.Return, ct);
            return null;
        }, ct);

        return await GetByIdAsync(id, userId, isAdmin, ct);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> RejectAsync(Guid id, Guid userId, bool isAdmin, RejectReturnRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.NoItems);

        var (rr, error) = await LoadForManageAsync(id, userId, isAdmin, ct);
        if (error is not null) return error;
        if (!ReturnWorkflow.IsValidReturnTransition(rr!.Status, ReturnRequestStatus.Rejected))
            return InvalidTransition(rr.Status, ReturnRequestStatus.Rejected);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            rr.RejectedReason = request.Reason;
            Transition(rr, ReturnRequestStatus.Rejected, userId, $"Từ chối: {request.Reason}");
            await NotifyAsync(rr.CustomerId, NotificationType.ReturnRejected, "Yêu cầu trả hàng bị từ chối",
                $"Yêu cầu của bạn đã bị từ chối. Lý do: {request.Reason}", rr.Id, ReferenceType.Return, ct);
            return null;
        }, ct);

        return await GetByIdAsync(id, userId, isAdmin, ct);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> ReceiveAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var (rr, error) = await LoadForManageAsync(id, userId, isAdmin, ct);
        if (error is not null) return error;
        if (!ReturnWorkflow.IsValidReturnTransition(rr!.Status, ReturnRequestStatus.ItemReceived))
            return InvalidTransition(rr.Status, ReturnRequestStatus.ItemReceived);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            rr.ReceivedAt = DateTime.UtcNow;
            Transition(rr, ReturnRequestStatus.ItemReceived, userId, "Đã nhận hàng trả, đang kiểm tra");
            await NotifyAsync(rr.CustomerId, NotificationType.ReturnReceived, "Đã nhận hàng trả",
                "Cửa hàng đã nhận hàng trả của bạn và đang xử lý.", rr.Id, ReferenceType.Return, ct);
            return null;
        }, ct);

        return await GetByIdAsync(id, userId, isAdmin, ct);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> ResolveAsync(Guid id, Guid userId, bool isAdmin, ResolveReturnRequest request, CancellationToken ct = default)
    {
        var (rr, error) = await LoadForManageAsync(id, userId, isAdmin, ct);
        if (error is not null) return error;

        var target = rr!.Type == ReturnType.Refund ? ReturnRequestStatus.Refunding : ReturnRequestStatus.Exchanging;
        if (!ReturnWorkflow.IsValidReturnTransition(rr.Status, target))
            return InvalidTransition(rr.Status, target);

        // Đổi hàng: nạp + kiểm tồn kho biến thể thay thế TRƯỚC transaction (entity tracked dùng tiếp để trừ kho).
        Dictionary<Guid, ProductItem> exItems = new();
        if (rr.Type == ReturnType.Exchange)
        {
            var exIds = rr.Items.Where(i => i.ExchangeProductItemId.HasValue)
                .Select(i => i.ExchangeProductItemId!.Value).Distinct().ToList();
            exItems = (await _uow.Returns.GetProductItemsWithProductAsync(exIds, ct)).ToDictionary(p => p.Id);
            foreach (var ri in rr.Items.Where(i => i.ExchangeProductItemId.HasValue))
            {
                if (!exItems.TryGetValue(ri.ExchangeProductItemId!.Value, out var ex) || ex.Product is null)
                    return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.ExchangeItemNotFound);
                if (ex.Stock < ri.Quantity)
                    return Fail(ApiStatusCodes.BadRequest,
                        string.Format(ApiStatusMessages.Returns.ExchangeOutOfStockFormat,
                            ex.Name is null ? ex.Product.Name : $"{ex.Product.Name} - {ex.Name}", ex.Stock));
            }
        }

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            var now = DateTime.UtcNow;

            // Hoàn kho hàng trả (nếu kiểm đạt).
            if (request.Restock)
            {
                var productItemIds = rr.Items.Select(i => i.OrderItem.ProductItemId).Distinct();
                var byId = (await _uow.Orders.GetProductItemsAsync(productItemIds, ct)).ToDictionary(p => p.Id);
                foreach (var ri in rr.Items)
                    if (byId.TryGetValue(ri.OrderItem.ProductItemId, out var pi))
                        pi.Stock += ri.Quantity;
            }

            if (rr.Type == ReturnType.Refund)
            {
                await _refund.CreateRefundAsync(rr, rr.RefundAmount, rr.RefundMethod, $"Hoàn tiền trả hàng #{rr.Id}", ct);
                Transition(rr, ReturnRequestStatus.Refunding, userId, string.IsNullOrWhiteSpace(request.Note) ? "Chấp nhận hoàn tiền" : request.Note);
            }
            else
            {
                await CreateReplacementDeliveryAsync(rr, exItems, now, ct);
                if (rr.RefundAmount > 0)
                    await _refund.CreateRefundAsync(rr, rr.RefundAmount, rr.RefundMethod, $"Hoàn chênh lệch đổi hàng #{rr.Id}", ct);

                Transition(rr, ReturnRequestStatus.Exchanging, userId, string.IsNullOrWhiteSpace(request.Note) ? "Chấp nhận đổi hàng" : request.Note);
                Transition(rr, ReturnRequestStatus.Completed, userId, "Đã tạo đơn giao hàng thay thế");
                await NotifyAsync(rr.CustomerId, NotificationType.ExchangeShipped, "Đang giao hàng đổi",
                    "Cửa hàng đã tạo đơn giao hàng thay thế cho yêu cầu đổi của bạn.", rr.Id, ReferenceType.Return, ct);
            }
            return null;
        }, ct);

        return await GetByIdAsync(id, userId, isAdmin, ct);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> CompleteRefundAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var (rr, error) = await LoadForManageAsync(id, userId, isAdmin, ct);
        if (error is not null) return error;
        if (rr!.Status != ReturnRequestStatus.Refunding)
            return InvalidTransition(rr.Status, ReturnRequestStatus.Completed);
        if (rr.Refund is null)
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.NoRefundToComplete);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            _refund.Complete(rr.Refund, userId);
            Transition(rr, ReturnRequestStatus.Completed, userId, "Đã hoàn tất hoàn tiền");
            await NotifyAsync(rr.CustomerId, NotificationType.RefundCompleted, "Đã hoàn tiền",
                $"Khoản hoàn tiền {rr.RefundAmount:#,##0} đ cho yêu cầu của bạn đã được xử lý.", rr.Id, ReferenceType.Refund, ct);
            return null;
        }, ct);

        return await GetByIdAsync(id, userId, isAdmin, ct);
    }

    // ===================== Helpers =====================

    /// <summary>
    /// Tạo delivery thay thế cho hàng đổi: thêm delivery + LƯU NGAY (để order_items tham chiếu hợp lệ),
    /// thêm order_items mới của biến thể thay thế, trừ kho, tạo vận đơn qua provider.
    /// </summary>
    private async Task CreateReplacementDeliveryAsync(ReturnRequest rr, Dictionary<Guid, ProductItem> exItems, DateTime now, CancellationToken ct)
    {
        var replacement = new Delivery
        {
            OrderId = rr.OrderId,
            GardenStoreId = rr.Delivery.GardenStoreId,
            Status = DeliveryStatus.Pending,
            ShippingFee = 0m,
        };
        await _uow.Orders.AddDeliveriesAsync(new[] { replacement }, ct);
        await _uow.SaveChangesAsync(ct);

        decimal subtotal = 0m;
        var totalWeightGram = 0;
        var newItems = new List<OrderItem>();
        var shipmentItems = new List<ShipmentItem>();
        foreach (var ri in rr.Items.Where(i => i.ExchangeProductItemId.HasValue))
        {
            var ex = exItems[ri.ExchangeProductItemId!.Value];
            var productName = ex.Name is null ? ex.Product.Name : $"{ex.Product.Name} - {ex.Name}";
            newItems.Add(new OrderItem
            {
                OrderId = rr.OrderId,
                ProductItemId = ex.Id,
                DeliveryId = replacement.Id,
                ProductName = productName,
                UnitPrice = ex.Price,
                Quantity = ri.Quantity,
            });
            shipmentItems.Add(new ShipmentItem(ex.Id.ToString(), productName, ex.Price, ri.Quantity,
                ex.WeightGram, ex.LengthCm, ex.WidthCm, ex.HeightCm));
            totalWeightGram += ex.WeightGram * ri.Quantity;
            subtotal += ex.Price * ri.Quantity;
            ex.Stock -= ri.Quantity; // trừ kho biến thể thay thế
        }
        await _uow.Orders.AddOrderItemsAsync(newItems, ct);
        replacement.Subtotal = subtotal;

        // Gom điểm lấy hàng (store) + điểm giao (khách) để tạo vận đơn hàng đổi (không thu thêm tiền → COD = 0).
        var store = (await _uow.Stores.GetWithAddressByIdsAsync(new[] { replacement.GardenStoreId }, ct)).FirstOrDefault();
        var shipTo = await _uow.UserAddresses.GetWithWardChainAsync(rr.Order.ShippingAddressId, ct);

        var shipment = await _shipping.CreateShipmentAsync(ShipmentRequestBuilder.Build(
            replacement.Id, rr.OrderId, subtotal, store, shipTo,
            codAmount: 0m, totalWeightGram: totalWeightGram, items: shipmentItems), ct);
        replacement.ShippingProvider = shipment.Provider;
        replacement.ProviderOrderId = shipment.ProviderOrderId;
        replacement.TrackingCode = shipment.TrackingCode;
        replacement.EstimatedDeliveryDate = shipment.EstimatedDeliveryDate;
        replacement.AssignedAt = now;
        replacement.Status = DeliveryStatus.Confirmed;

        await _uow.Shipping.AddProgressLogAsync(new DeliveryProgressLog
        {
            DeliveryId = replacement.Id,
            SourceType = DeliverySource.System,
            FromStatus = DeliveryStatus.Pending.ToString(),
            ToStatus = DeliveryStatus.Confirmed.ToString(),
            Note = $"Tạo vận đơn hàng đổi {shipment.Provider} ({shipment.TrackingCode})",
            LoggedAt = now,
        }, ct);

        rr.ReplacementDeliveryId = replacement.Id;
    }

    private async Task<(ReturnRequest? Rr, IServiceResult<ReturnDetailResponse>? Error)> LoadForManageAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct)
    {
        var rr = await _uow.Returns.GetWithGraphAsync(id, ct);
        if (rr is null)
            return (null, Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.NotFound));
        if (!isAdmin && !await _uow.Stores.CanManageAsync(rr.Delivery.GardenStoreId, userId, ct))
            return (null, Fail(ApiStatusCodes.Forbidden, ApiStatusMessages.Returns.ManageForbidden));
        return (rr, null);
    }

    private void Transition(ReturnRequest rr, ReturnRequestStatus to, Guid? actorId, string note)
    {
        var from = rr.Status;
        rr.Status = to;
        rr.StatusChangeNote = note;
        // Add tường minh qua DbSet (Added → INSERT). Add qua navigation vào rr đã-tracked bị EF
        // đánh Modified (UPDATE 0 rows) vì BaseEntity set sẵn Id — xem ghi chú ở PaymentService.
        _uow.Returns.AddStatusLog(new ReturnStatusLog
        {
            ReturnRequestId = rr.Id,
            FromStatus = from.ToString(),
            ToStatus = to.ToString(),
            ChangedBy = actorId,
            Note = note,
            ChangedAt = DateTime.UtcNow,
        });
    }

    private Task NotifyAsync(Guid userId, NotificationType type, string title, string message, Guid refId, ReferenceType refType, CancellationToken ct)
        => _uow.Notifications.AddAsync(new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            ReferenceId = refId,
            ReferenceType = refType,
            IsRead = false,
        }, ct);

    private IServiceResult<PagedResult<ReturnListItemResponse>> Paged(List<ReturnRequest> items, PageRequest page, int total)
        => ServiceResult<PagedResult<ReturnListItemResponse>>.Success(
            new PagedResult<ReturnListItemResponse>(_mapper.Map<List<ReturnListItemResponse>>(items), page.Page, page.PageSize, total));

    private static ServiceResult<ReturnDetailResponse> Fail(int code, string message)
        => ServiceResult<ReturnDetailResponse>.Failure(code, message);

    private static ServiceResult<ReturnDetailResponse> InvalidTransition(ReturnRequestStatus from, ReturnRequestStatus to)
        => ServiceResult<ReturnDetailResponse>.Failure(ApiStatusCodes.BadRequest,
            string.Format(ApiStatusMessages.Returns.InvalidTransitionFormat, from, to));
}
