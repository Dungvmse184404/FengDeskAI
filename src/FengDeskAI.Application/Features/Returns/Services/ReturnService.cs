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
using FengDeskAI.Domain.StateMachines;

namespace FengDeskAI.Application.Features.Returns.Services;

/// <summary>
/// Luồng RMA v2. Quyết định do Staff; Vendor chỉ góp ý (non-blocking) + xác nhận nhận hàng.
/// Chuyển trạng thái đóng gói trong entity (guard bằng <see cref="ReturnStateMachine"/>); transition sai → 409.
/// </summary>
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
        // Bắt buộc có ảnh bằng chứng khi tạo ticket (guard rule).
        // FE upload ảnh trước qua POST /api/uploads để lấy URL rồi truyền vào ImageUrls.
        var hasUrlEvidence = request.ImageUrls is not null && request.ImageUrls.Any(u => !string.IsNullOrWhiteSpace(u));
        if (!hasUrlEvidence)
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.EvidenceRequired);

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
            Note = "Tạo ticket trả hàng/đổi trả",
            ChangedAt = now,
        });

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            await _uow.Returns.AddAsync(rr, ct);
            await NotifyAsync(userId, NotificationType.ReturnRequested, "Đã gửi yêu cầu trả hàng",
                "Yêu cầu của bạn đã được gửi và đang chờ nhân viên nền tảng tiếp nhận.",
                rr.Id, ReferenceType.Return, ct);
            return null;
        }, ct);

        return await LoadDetailAsync(rr.Id, ct);
    }

    public async Task<IServiceResult<PagedResult<ReturnListItemResponse>>> GetMineAsync(Guid userId, PageRequest page, CancellationToken ct = default)
    {
        var (items, total) = await _uow.Returns.GetByCustomerAsync(userId, page.Skip, page.PageSize, ct);
        return Paged(items, page, total);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> GetByIdAsync(Guid id, RmaActor actor, CancellationToken ct = default)
    {
        var rr = await _uow.Returns.GetDetailAsync(id, null, ct);
        if (rr is null)
            return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.NotFound);

        var authorized = rr.CustomerId == actor.UserId || actor.CanDecide
            || (actor.IsGardenOwner && await _uow.Stores.CanManageAsync(rr.Delivery.GardenStoreId, actor.UserId, ct));
        if (!authorized)
            return Fail(ApiStatusCodes.Forbidden, ApiStatusMessages.Returns.ViewForbidden);

        return ServiceResult<ReturnDetailResponse>.Success(_mapper.Map<ReturnDetailResponse>(rr));
    }

    public async Task<IServiceResult<ReturnDetailResponse>> CancelAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var rr = await _uow.Returns.GetWithGraphAsync(id, ct);
        if (rr is null || rr.CustomerId != userId)
            return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.NotFound);
        if (!ReturnStateMachine.CanTransition(rr.Status, ReturnRequestStatus.Cancelled, rr.Reason))
            return InvalidTransition(rr.Status, ReturnRequestStatus.Cancelled);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            var from = rr.Status;
            rr.Cancel();
            LogTransition(rr, from, "Khách hủy yêu cầu", userId);
            await NotifyAsync(rr.CustomerId, NotificationType.ReturnCancelled, "Đã hủy yêu cầu trả hàng",
                "Yêu cầu trả hàng/đổi trả của bạn đã được hủy.", rr.Id, ReferenceType.Return, ct);
            return null;
        }, ct);

        return await LoadDetailAsync(id, ct);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> ResubmitEvidenceAsync(Guid id, Guid userId, IReadOnlyList<ReturnImageFile> files, CancellationToken ct = default)
    {
        var fileError = ValidateImageFiles(files);
        if (fileError is not null) return fileError;

        var rr = await _uow.Returns.GetWithGraphAsync(id, ct);
        if (rr is null || rr.CustomerId != userId)
            return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.NotFound);
        if (!ReturnStateMachine.CanTransition(rr.Status, ReturnRequestStatus.Requested, rr.Reason))
            return InvalidTransition(rr.Status, ReturnRequestStatus.Requested);

        // Upload ảnh bổ sung (nếu có) trước khi đổi trạng thái.
        if (files is { Count: > 0 })
            await UploadImagesToReturnAsync(rr.Id, files, rr.Images.Count, ct);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            var from = rr.Status;
            rr.ResubmitEvidence();
            LogTransition(rr, from, "Khách bổ sung bằng chứng", userId);
            await NotifyAsync(rr.CustomerId, NotificationType.ReturnRequested, "Đã bổ sung bằng chứng",
                "Bằng chứng bổ sung đã được gửi, đang chờ nhân viên xem lại.", rr.Id, ReferenceType.Return, ct);
            return null;
        }, ct);

        return await LoadDetailAsync(id, ct);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> UploadImagesAsync(Guid id, Guid userId, IReadOnlyList<ReturnImageFile> files, CancellationToken ct = default)
    {
        if (files is null || files.Count == 0)
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.ImageFileRequired);
        var fileError = ValidateImageFiles(files);
        if (fileError is not null) return fileError;

        var rr = await _uow.Returns.GetDetailAsync(id, userId, ct);
        if (rr is null)
            return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.NotFound);
        if (rr.Status is not (ReturnRequestStatus.Requested or ReturnRequestStatus.NeedMoreEvidence))
            return Fail(ApiStatusCodes.Conflict, ApiStatusMessages.Returns.ImageAddNotAllowed);

        await UploadImagesToReturnAsync(id, files, rr.Images.Count, ct);
        return await LoadDetailAsync(id, ct);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> DeleteImageAsync(Guid id, Guid imageId, Guid userId, CancellationToken ct = default)
    {
        var rr = await _uow.Returns.GetDetailAsync(id, userId, ct);
        if (rr is null)
            return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.NotFound);
        if (rr.Status is not (ReturnRequestStatus.Requested or ReturnRequestStatus.NeedMoreEvidence))
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.ImageDeleteNotAllowed);

        var image = await _uow.Returns.GetImageAsync(id, imageId, ct);
        if (image is null)
            return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.ImageNotFound);

        _uow.Returns.RemoveImage(image);
        await _uow.SaveChangesAsync(ct);
        await _storage.DeleteByUrlAsync(image.ImageUrl, ct);

        return await LoadDetailAsync(id, ct);
    }

    // ===================== Vendor (non-blocking) =====================

    public async Task<IServiceResult<PagedResult<ReturnListItemResponse>>> GetForStoreAsync(Guid storeId, RmaActor actor, PageRequest page, CancellationToken ct = default)
    {
        if (!actor.CanDecide && !(actor.IsGardenOwner && await _uow.Stores.CanManageAsync(storeId, actor.UserId, ct)))
            return ServiceResult<PagedResult<ReturnListItemResponse>>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Returns.ManageForbidden);

        var (items, total) = await _uow.Returns.GetForStoreAsync(storeId, page.Skip, page.PageSize, ct);
        return Paged(items, page, total);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> VendorAcknowledgeAsync(Guid id, RmaActor actor, CancellationToken ct = default)
    {
        var (rr, error) = await LoadForVendorAsync(id, actor, ct);
        if (error is not null) return error;

        rr!.VendorAcknowledge();
        await _uow.SaveChangesAsync(ct);
        return await LoadDetailAsync(id, ct);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> VendorDisputeAsync(Guid id, RmaActor actor, VendorDisputeRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.DisputeReasonRequired);

        var (rr, error) = await LoadForVendorAsync(id, actor, ct);
        if (error is not null) return error;

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            rr!.VendorDispute();
            // Ghi log để Staff thấy phản đối — KHÔNG chặn quyết định của Staff.
            LogTransition(rr, rr.Status, $"Vendor phản đối: {request.Reason}", actor.UserId);
            await Task.CompletedTask;
            return null;
        }, ct);

        return await LoadDetailAsync(id, ct);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> ConfirmItemReceivedAsync(Guid id, RmaActor actor, CancellationToken ct = default)
    {
        var (rr, error) = await LoadForVendorAsync(id, actor, ct);
        if (error is not null) return error;
        if (!ReturnStateMachine.CanTransition(rr!.Status, ReturnRequestStatus.ItemReceived, rr.Reason))
            return InvalidTransition(rr.Status, ReturnRequestStatus.ItemReceived);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            var now = DateTime.UtcNow;
            var from = rr.Status;
            rr.ConfirmItemReceived(now);      // ReturnInTransit → ItemReceived
            LogTransition(rr, from, "Vendor xác nhận đã nhận hàng trả", actor.UserId);
            rr.MoveToReviewing();             // ItemReceived → Reviewing (chuyển cho Staff quyết định)
            LogTransition(rr, ReturnRequestStatus.ItemReceived, "Chuyển sang bước Staff ra quyết định", actor.UserId);
            await NotifyAsync(rr.CustomerId, NotificationType.ReturnReceived, "Đã nhận hàng trả",
                "Cửa hàng đã nhận hàng trả của bạn; nền tảng đang xử lý.", rr.Id, ReferenceType.Return, ct);
            return null;
        }, ct);

        return await LoadDetailAsync(id, ct);
    }

    // ===================== Staff (decision) =====================

    public async Task<IServiceResult<PagedResult<ReturnListItemResponse>>> GetPendingForStaffAsync(PageRequest page, CancellationToken ct = default)
    {
        var (items, total) = await _uow.Returns.GetPendingForStaffAsync(page.Skip, page.PageSize, ct);
        return Paged(items, page, total);
    }

    public async Task<IServiceResult<PagedResult<ReturnListItemResponse>>> GetAllAsync(PageRequest page, CancellationToken ct = default)
    {
        var (items, total) = await _uow.Returns.GetAllPagedAsync(page.Skip, page.PageSize, ct);
        return Paged(items, page, total);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> AcceptAsync(Guid id, RmaActor actor, CancellationToken ct = default)
    {
        var (rr, error) = await LoadForDecisionAsync(id, actor, ct);
        if (error is not null) return error;
        if (!ReturnStateMachine.CanTransition(rr!.Status, ReturnRequestStatus.UnderReview, rr.Reason))
            return InvalidTransition(rr.Status, ReturnRequestStatus.UnderReview);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            var now = DateTime.UtcNow;
            var from = rr.Status;
            rr.Accept(now.AddHours(ReturnWorkflow.VendorResponseSlaHours)); // Requested → UnderReview
            LogTransition(rr, from, "Staff tiếp nhận; thông báo vendor (SLA phản hồi, non-blocking)", actor.UserId);

            var routedFrom = rr.Status;
            rr.RouteAfterAccept(); // UnderReview → Reviewing (plant_health) | ReturnInTransit (hàng vật lý)
            LogTransition(rr, routedFrom,
                rr.Reason == ReturnReason.PlantHealth
                    ? "Cây chết — bỏ qua thu hồi, chuyển thẳng bước quyết định"
                    : "Yêu cầu khách gửi hàng trả về để thu hồi", actor.UserId);

            await NotifyAsync(rr.CustomerId, NotificationType.ReturnApproved, "Yêu cầu đang được xử lý",
                rr.Reason == ReturnReason.PlantHealth
                    ? "Nền tảng đã tiếp nhận yêu cầu và đang xem xét (không cần gửi trả cây)."
                    : "Nền tảng đã tiếp nhận. Vui lòng gửi hàng trả về theo hướng dẫn.",
                rr.Id, ReferenceType.Return, ct);
            return null;
        }, ct);

        return await LoadDetailAsync(id, ct);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> RequestMoreEvidenceAsync(Guid id, RmaActor actor, RequestMoreEvidenceRequest request, CancellationToken ct = default)
    {
        var (rr, error) = await LoadForDecisionAsync(id, actor, ct);
        if (error is not null) return error;
        if (!ReturnStateMachine.CanTransition(rr!.Status, ReturnRequestStatus.NeedMoreEvidence, rr.Reason))
            return InvalidTransition(rr.Status, ReturnRequestStatus.NeedMoreEvidence);

        var hours = request.DeadlineHours is > 0 ? request.DeadlineHours!.Value : ReturnWorkflow.EvidenceSlaHours;
        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            var from = rr.Status;
            rr.RequestMoreEvidence(DateTime.UtcNow.AddHours(hours));
            LogTransition(rr, from, string.IsNullOrWhiteSpace(request.Note) ? "Yêu cầu bổ sung bằng chứng" : request.Note!, actor.UserId);
            await NotifyAsync(rr.CustomerId, NotificationType.ReturnRequested, "Cần bổ sung bằng chứng",
                $"Vui lòng bổ sung bằng chứng trong {hours} giờ để yêu cầu được tiếp tục.", rr.Id, ReferenceType.Return, ct);
            return null;
        }, ct);

        return await LoadDetailAsync(id, ct);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> ApproveRefundAsync(Guid id, RmaActor actor, ApproveRefundRequest request, CancellationToken ct = default)
    {
        var (rr, error) = await LoadForDecisionAsync(id, actor, ct);
        if (error is not null) return error;
        if (!ReturnStateMachine.CanTransition(rr!.Status, ReturnRequestStatus.Refunding, rr.Reason))
            return InvalidTransition(rr.Status, ReturnRequestStatus.Refunding);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            var now = DateTime.UtcNow;
            // Hoàn kho chỉ khi có thu hồi hàng (không áp dụng cây chết).
            if (request.Restock && rr.Reason != ReturnReason.PlantHealth)
                await RestockAsync(rr, ct);

            var from = rr.Status;
            rr.ApproveRefund(actor.UserId, now); // Reviewing → Refunding
            LogTransition(rr, from, string.IsNullOrWhiteSpace(request.Note) ? "Staff duyệt hoàn tiền" : request.Note!, actor.UserId);

            // Ứng tiền hoàn cho khách NGAY (không chờ vendor).
            await _refund.CreateRefundAsync(rr, rr.RefundAmount, rr.RefundMethod, $"Hoàn tiền ticket #{rr.Id}", ct);
            await NotifyAsync(rr.CustomerId, NotificationType.ReturnApproved, "Yêu cầu hoàn tiền được duyệt",
                "Nền tảng đã duyệt hoàn tiền và đang xử lý chuyển tiền cho bạn.", rr.Id, ReferenceType.Return, ct);
            return null;
        }, ct);

        return await LoadDetailAsync(id, ct);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> ApproveExchangeAsync(Guid id, RmaActor actor, ApproveExchangeRequest request, CancellationToken ct = default)
    {
        var (rr, error) = await LoadForDecisionAsync(id, actor, ct);
        if (error is not null) return error;
        if (!ReturnStateMachine.CanTransition(rr!.Status, ReturnRequestStatus.Exchanging, rr.Reason))
            return InvalidTransition(rr.Status, ReturnRequestStatus.Exchanging);
        if (rr.Type != ReturnType.Exchange || !rr.Items.Any(i => i.ExchangeProductItemId.HasValue))
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.ExchangeItemRequired);

        // Nạp biến thể thay thế + kiểm tồn kho.
        var exIds = rr.Items.Where(i => i.ExchangeProductItemId.HasValue)
            .Select(i => i.ExchangeProductItemId!.Value).Distinct().ToList();
        var exItems = (await _uow.Returns.GetProductItemsWithProductAsync(exIds, ct)).ToDictionary(p => p.Id);
        var outOfStock = false;
        foreach (var ri in rr.Items.Where(i => i.ExchangeProductItemId.HasValue))
        {
            if (!exItems.TryGetValue(ri.ExchangeProductItemId!.Value, out var ex) || ex.Product is null)
                return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.ExchangeItemNotFound);
            if (ex.Stock < ri.Quantity) outOfStock = true;
        }

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            var now = DateTime.UtcNow;
            if (request.Restock && rr.Reason != ReturnReason.PlantHealth)
                await RestockAsync(rr, ct);

            var from = rr.Status;
            rr.ApproveExchange(actor.UserId, now); // Reviewing → Exchanging
            LogTransition(rr, from, string.IsNullOrWhiteSpace(request.Note) ? "Staff duyệt đổi hàng" : request.Note!, actor.UserId);

            if (outOfStock)
            {
                // Hết hàng thay thế → fallback sang hoàn tiền (không dead-end).
                var exFrom = rr.Status;
                rr.FallbackToRefund(); // Exchanging → Refunding
                LogTransition(rr, exFrom, "Hết hàng thay thế — chuyển sang hoàn tiền", actor.UserId);
                rr.RefundAmount = ReturnWorkflow.ComputeRefundAmount(rr.Items);
                await _refund.CreateRefundAsync(rr, rr.RefundAmount, rr.RefundMethod, $"Hoàn tiền (hết hàng đổi) ticket #{rr.Id}", ct);
                await NotifyAsync(rr.CustomerId, NotificationType.ReturnApproved, "Chuyển sang hoàn tiền",
                    "Sản phẩm đổi đã hết hàng, nền tảng sẽ hoàn tiền cho bạn.", rr.Id, ReferenceType.Return, ct);
            }
            else
            {
                await CreateReplacementDeliveryAsync(rr, exItems, now, ct);
                var exFrom = rr.Status;
                rr.CompleteExchange(); // Exchanging → Completed
                LogTransition(rr, exFrom, "Đã tạo đơn giao hàng thay thế", actor.UserId);

                // Đổi rẻ hơn → hoàn chênh lệch (refund độc lập, không đổi trạng thái ticket).
                if (rr.RefundAmount > 0)
                    await _refund.CreateRefundAsync(rr, rr.RefundAmount, rr.RefundMethod, $"Hoàn chênh lệch đổi hàng ticket #{rr.Id}", ct);

                await NotifyAsync(rr.CustomerId, NotificationType.ExchangeShipped, "Đang giao hàng đổi",
                    "Nền tảng đã tạo đơn giao hàng thay thế cho bạn.", rr.Id, ReferenceType.Return, ct);
            }
            return null;
        }, ct);

        return await LoadDetailAsync(id, ct);
    }

    public async Task<IServiceResult<ReturnDetailResponse>> RejectAsync(Guid id, RmaActor actor, RejectReturnRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.RejectReasonRequired);

        var (rr, error) = await LoadForDecisionAsync(id, actor, ct);
        if (error is not null) return error;
        if (!ReturnStateMachine.CanTransition(rr!.Status, ReturnRequestStatus.Rejected, rr.Reason))
            return InvalidTransition(rr.Status, ReturnRequestStatus.Rejected);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            var from = rr.Status;
            rr.Reject(actor.UserId, DateTime.UtcNow, request.Reason);
            LogTransition(rr, from, $"Từ chối: {request.Reason}", actor.UserId);
            await NotifyAsync(rr.CustomerId, NotificationType.ReturnRejected, "Yêu cầu trả hàng bị từ chối",
                $"Yêu cầu của bạn đã bị từ chối. Lý do: {request.Reason}", rr.Id, ReferenceType.Return, ct);
            return null;
        }, ct);

        return await LoadDetailAsync(id, ct);
    }

    // ===================== Worker =====================

    public async Task<int> AutoRejectOverdueEvidenceAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var overdue = await _uow.Returns.GetOverdueEvidenceTicketsAsync(now, 100, ct);
        foreach (var rr in overdue)
        {
            var from = rr.Status;
            rr.RejectForEvidenceTimeout(); // NeedMoreEvidence → Rejected
            LogTransition(rr, from, "Tự động từ chối: quá hạn bổ sung bằng chứng", null);
            await NotifyAsync(rr.CustomerId, NotificationType.ReturnRejected, "Yêu cầu bị từ chối",
                "Bạn không bổ sung bằng chứng kịp thời hạn nên yêu cầu đã bị từ chối.", rr.Id, ReferenceType.Return, ct);
        }
        if (overdue.Count > 0) await _uow.SaveChangesAsync(ct);
        return overdue.Count;
    }

    // ===================== Helpers =====================

    private async Task RestockAsync(ReturnRequest rr, CancellationToken ct)
    {
        var productItemIds = rr.Items.Select(i => i.OrderItem.ProductItemId).Distinct();
        var byId = (await _uow.Orders.GetProductItemsAsync(productItemIds, ct)).ToDictionary(p => p.Id);
        foreach (var ri in rr.Items)
            if (byId.TryGetValue(ri.OrderItem.ProductItemId, out var pi))
                pi.Stock += ri.Quantity;
    }

    /// <summary>
    /// Tạo delivery thay thế (0đ, is_exchange = true) cho hàng đổi: thêm delivery + LƯU NGAY,
    /// thêm order_items biến thể thay thế, trừ kho, tạo vận đơn qua provider.
    /// </summary>
    private async Task CreateReplacementDeliveryAsync(ReturnRequest rr, Dictionary<Guid, ProductItem> exItems, DateTime now, CancellationToken ct)
    {
        var replacement = new Delivery
        {
            OrderId = rr.OrderId,
            GardenStoreId = rr.Delivery.GardenStoreId,
            Status = DeliveryStatus.Pending,
            ShippingFee = 0m,
            IsExchange = true,
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
            ex.Stock -= ri.Quantity;
        }
        await _uow.Orders.AddOrderItemsAsync(newItems, ct);
        replacement.Subtotal = subtotal;

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

    private async Task<(ReturnRequest? Rr, IServiceResult<ReturnDetailResponse>? Error)> LoadForDecisionAsync(Guid id, RmaActor actor, CancellationToken ct)
    {
        if (!actor.CanDecide)
            return (null, Fail(ApiStatusCodes.Forbidden, ApiStatusMessages.Returns.StaffOnly));
        var rr = await _uow.Returns.GetWithGraphAsync(id, ct);
        if (rr is null)
            return (null, Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.NotFound));
        return (rr, null);
    }

    private async Task<(ReturnRequest? Rr, IServiceResult<ReturnDetailResponse>? Error)> LoadForVendorAsync(Guid id, RmaActor actor, CancellationToken ct)
    {
        var rr = await _uow.Returns.GetWithGraphAsync(id, ct);
        if (rr is null)
            return (null, Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.NotFound));
        var allowed = actor.IsAdmin
            || (actor.IsGardenOwner && await _uow.Stores.CanManageAsync(rr.Delivery.GardenStoreId, actor.UserId, ct));
        if (!allowed)
            return (null, Fail(ApiStatusCodes.Forbidden, ApiStatusMessages.Returns.ManageForbidden));
        return (rr, null);
    }

    private void LogTransition(ReturnRequest rr, ReturnRequestStatus from, string note, Guid? actorId)
    {
        rr.StatusChangeNote = note;
        _uow.Returns.AddStatusLog(new ReturnStatusLog
        {
            ReturnRequestId = rr.Id,
            FromStatus = from.ToString(),
            ToStatus = rr.Status.ToString(),
            ChangedBy = actorId,
            Note = note,
            ChangedAt = DateTime.UtcNow,
        });
    }

    private static ServiceResult<ReturnDetailResponse>? ValidateImageFiles(IReadOnlyList<ReturnImageFile>? files)
    {
        if (files is null) return null;
        foreach (var file in files)
        {
            if (file.Content is null || file.Content.Length == 0)
                return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.ImageFileRequired);
            if (!ImageUpload.IsAllowed(file.ContentType))
                return Fail(ApiStatusCodes.UnprocessableEntity, ApiStatusMessages.Returns.ImageTypeInvalid);
        }
        return null;
    }

    private async Task UploadImagesToReturnAsync(Guid returnId, IReadOnlyList<ReturnImageFile> files, int baseSort, CancellationToken ct)
    {
        var sort = baseSort;
        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ImageUpload.ExtensionFor(file.ContentType);
            var objectPath = $"Return_images/{returnId}/{Guid.NewGuid():N}{ext}";
            var stored = await _storage.UploadAsync(objectPath, file.Content, file.ContentType, ct);
            await _uow.Returns.AddImageAsync(new ReturnRequestImage
            {
                ReturnRequestId = returnId,
                ImageUrl = stored.Url,
                SortOrder = sort++,
            }, ct);
        }
        await _uow.SaveChangesAsync(ct);
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

    private async Task<IServiceResult<ReturnDetailResponse>> LoadDetailAsync(Guid id, CancellationToken ct)
    {
        var rr = await _uow.Returns.GetDetailAsync(id, null, ct);
        return rr is null
            ? Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.NotFound)
            : ServiceResult<ReturnDetailResponse>.Success(_mapper.Map<ReturnDetailResponse>(rr));
    }

    private IServiceResult<PagedResult<ReturnListItemResponse>> Paged(List<ReturnRequest> items, PageRequest page, int total)
        => ServiceResult<PagedResult<ReturnListItemResponse>>.Success(
            new PagedResult<ReturnListItemResponse>(_mapper.Map<List<ReturnListItemResponse>>(items), page.Page, page.PageSize, total));

    private static ServiceResult<ReturnDetailResponse> Fail(int code, string message)
        => ServiceResult<ReturnDetailResponse>.Failure(code, message);

    private static ServiceResult<ReturnDetailResponse> InvalidTransition(ReturnRequestStatus from, ReturnRequestStatus to)
        => ServiceResult<ReturnDetailResponse>.Failure(ApiStatusCodes.Conflict,
            string.Format(ApiStatusMessages.Returns.InvalidTransitionFormat, from, to));
}
