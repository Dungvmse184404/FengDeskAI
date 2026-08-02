using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Returns.DTOs;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Announcement;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Notification;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.StateMachines;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Application.Features.Returns.Services;

/// <summary>
/// Refund sub-saga. Nền tảng ứng tiền hoàn cho khách NGAY khi Staff duyệt (không chờ vendor).
/// Idempotency key chống hoàn trùng; Failed không dead-end; xác nhận thủ công BẮT BUỘC audit trail.
/// </summary>
public class RefundService : IRefundService
{
    private readonly IUnitOfWork _uow;
    private readonly IPaymentGateway _gateway;
    private readonly IVendorLiabilityService _liability;
    private readonly IMapper _mapper;
    private readonly ILogger<RefundService> _logger;

    public RefundService(IUnitOfWork uow, IPaymentGateway gateway, IVendorLiabilityService liability, IMapper mapper, ILogger<RefundService> logger)
    {
        _uow = uow;
        _gateway = gateway;
        _liability = liability;
        _mapper = mapper;
        _logger = logger;
    }

    // ===================== Tạo Pending (gọi trong transaction của ticket) =====================

    public async Task<Refund> CreateRefundAsync(ReturnRequest ticket, decimal amount, RefundMethod method, string reason, CancellationToken ct = default)
    {
        // Idempotent theo ticket — chống tạo/thực thi hoàn tiền 2 lần (invariant #2).
        if (ticket.Refund is not null) return ticket.Refund;
        var key = ReturnWorkflow.RefundIdempotencyKey(ticket.Id);
        var existing = await _uow.Returns.GetRefundByIdempotencyKeyAsync(key, ct);
        if (existing is not null) return existing;

        // Invariant #7: số tiền hoàn không vượt tổng giá trị các dòng của ticket.
        var cap = ReturnWorkflow.ComputeRefundAmount(ticket.Items);
        if (amount > cap) amount = cap;

        var refund = new Refund
        {
            ReturnRequestId = ticket.Id,
            OrderId = ticket.OrderId,
            Amount = amount,
            Method = method,
            IdempotencyKey = key,
            Gateway = "payos",
            Note = reason,
        };
        ticket.Refund = refund;
        await _uow.Returns.AddRefundAsync(refund, ct);

        // Hoàn về nguồn: gắn giao dịch PayOS gốc nếu có.
        if (method == RefundMethod.Original)
        {
            var txn = await _uow.Transactions.GetLatestByOrderAsync(ticket.OrderId, ct);
            if (txn is not null && txn.Status == PaymentStatus.Paid)
            {
                refund.TransactionId = txn.Id;
            }
        }

        // Chỉ tạo Pending trong transaction của ticket. Worker dispatch sau commit để Pending là trạng thái
        // quan sát/hủy được và transaction quyết định ticket không phải chờ provider.
        return refund;
    }

    // ===================== Webhook =====================

    public async Task<IServiceResult> HandleWebhookAsync(string rawJsonBody, CancellationToken ct = default)
    {
        PaymentWebhookResult verified;
        try { verified = _gateway.VerifyWebhook(rawJsonBody); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Refund webhook chữ ký/định dạng không hợp lệ.");
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.WebhookInvalid);
        }

        if (string.IsNullOrEmpty(verified.ProviderReference))
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.WebhookInvalid);

        var refund = await _uow.Returns.GetRefundByProviderRefAsync(verified.ProviderReference, ct);
        if (refund is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.RefundNotFound);

        // Idempotent: webhook lặp cho refund đã kết thúc → no-op thành công.
        if (RefundStateMachine.IsTerminal(refund.Status))
            return ServiceResult.Success(ApiStatusMessages.Returns.WebhookProcessed);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            if (verified.Success)
            {
                if (refund.Status == RefundStatus.Pending)
                    refund.MarkProcessing(verified.ProviderReference, DateTime.UtcNow);
                else if (refund.Status is RefundStatus.Failed or RefundStatus.ManagerReview)
                    refund.RetryToProcessing(verified.ProviderReference, DateTime.UtcNow);

                if (refund.Status == RefundStatus.Processing)
                    await CompleteRefundAndTicketAsync(refund, actorId: null, ct);
            }
            else if (refund.Status == RefundStatus.Processing)
            {
                refund.MarkFailed();
            }
            return null;
        }, ct);

        return ServiceResult.Success(ApiStatusMessages.Returns.WebhookProcessed);
    }

    // ===================== Manager =====================

    public async Task<IServiceResult<RefundResponse>> RetryRefundAsync(Guid refundId, RmaActor actor, CancellationToken ct = default)
    {
        if (!actor.CanManageRefund) return Fail(ApiStatusCodes.Forbidden, ApiStatusMessages.Returns.ManagerOnly);

        var refund = await _uow.Returns.GetRefundByIdAsync(refundId, ct);
        if (refund is null) return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.RefundNotFound);
        if (refund.Status is not (RefundStatus.Failed or RefundStatus.ManagerReview))
            return Fail(ApiStatusCodes.Conflict, ApiStatusMessages.Returns.RefundNotRetryable);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            await AttemptGatewayRetryAsync(refund, allowBeyondAutoRetryLimit: true, ct);
            return null;
        }, ct);

        return Ok(refund);
    }

    public async Task<IServiceResult<RefundResponse>> ManagerConfirmRefundAsync(Guid refundId, RmaActor actor, ManagerConfirmRefundRequest request, CancellationToken ct = default)
    {
        if (!actor.CanManageRefund) return Fail(ApiStatusCodes.Forbidden, ApiStatusMessages.Returns.ManagerOnly);
        if (string.IsNullOrWhiteSpace(request.ManualReason) || string.IsNullOrWhiteSpace(request.EvidenceUrl))
            return Fail(ApiStatusCodes.BadRequest, ApiStatusMessages.Returns.ManualEvidenceRequired);

        var refund = await _uow.Returns.GetRefundByIdAsync(refundId, ct);
        if (refund is null) return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.RefundNotFound);
        if (refund.Status != RefundStatus.ManagerReview)
            return Fail(ApiStatusCodes.Conflict, ApiStatusMessages.Returns.RefundNotManualConfirmable);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            refund.ManagerComplete(request.ManualReason, request.EvidenceUrl, actor.UserId, DateTime.UtcNow);
            await CompleteTicketAndLiabilityAsync(refund, actor.UserId, ct);
            return null;
        }, ct);

        return Ok(refund);
    }

    public async Task<IServiceResult<RefundResponse>> ManagerCancelRefundAsync(Guid refundId, RmaActor actor, CancellationToken ct = default)
    {
        if (!actor.CanManageRefund) return Fail(ApiStatusCodes.Forbidden, ApiStatusMessages.Returns.ManagerOnly);

        var refund = await _uow.Returns.GetRefundByIdAsync(refundId, ct);
        if (refund is null) return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.RefundNotFound);
        if (!RefundStateMachine.CanTransition(refund.Status, RefundStatus.Cancelled))
            return Fail(ApiStatusCodes.Conflict, ApiStatusMessages.Returns.RefundNotCancellable);
        if (refund.ReturnRequest.Status != Domain.Enums.Sales.ReturnRequestStatus.Refunding)
            return Fail(ApiStatusCodes.Conflict, ApiStatusMessages.Returns.RefundNotCancellable);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            refund.Cancel(actor.UserId);
            var ticket = refund.ReturnRequest;
            if (ticket.Status == Domain.Enums.Sales.ReturnRequestStatus.Refunding)
            {
                var from = ticket.Status;
                const string reason = "Manager hủy hoàn tiền do phát hiện dấu hiệu gian lận.";
                ticket.Reject(actor.UserId, DateTime.UtcNow, reason);
                _uow.Returns.AddStatusLog(new ReturnStatusLog
                {
                    ReturnRequestId = ticket.Id,
                    FromStatus = from.ToString(),
                    ToStatus = ticket.Status.ToString(),
                    ChangedBy = actor.UserId,
                    Note = reason,
                    ChangedAt = DateTime.UtcNow,
                });
                await _uow.Notifications.AddAsync(new Notification
                {
                    UserId = ticket.CustomerId,
                    Type = NotificationType.ReturnRejected,
                    Title = "Yêu cầu hoàn tiền bị hủy",
                    Message = reason,
                    ReferenceId = ticket.Id,
                    ReferenceType = ReferenceType.Return,
                    IsRead = false,
                }, ct);
            }
            return null;
        }, ct);
        return Ok(refund);
    }

    public async Task<IServiceResult<PagedResult<RefundResponse>>> GetForManagerAsync(PageRequest page, CancellationToken ct = default)
    {
        var (items, total) = await _uow.Returns.GetRefundsForManagerAsync(page.Skip, page.PageSize, ct);
        return ServiceResult<PagedResult<RefundResponse>>.Success(
            new PagedResult<RefundResponse>(_mapper.Map<List<RefundResponse>>(items), page.Page, page.PageSize, total));
    }

    public async Task<IServiceResult<RefundResponse>> GetByIdAsync(
        Guid refundId, RmaActor actor, CancellationToken ct = default)
    {
        if (!actor.CanManageRefund) return Fail(ApiStatusCodes.Forbidden, ApiStatusMessages.Returns.ManagerOnly);
        var refund = await _uow.Returns.GetRefundByIdAsync(refundId, ct);
        return refund is null
            ? Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.RefundNotFound)
            : Ok(refund);
    }

    // ===================== Worker =====================

    public async Task<int> AutoProcessFailedRefundsAsync(CancellationToken ct = default)
    {
        var failed = await _uow.Returns.GetFailedRefundsAsync(50, ct);
        if (failed.Count == 0) return 0;

        foreach (var refund in failed)
        {
            try
            {
                await _uow.ExecuteInTransactionAsync<object?>(async _ =>
                {
                    await AttemptGatewayRetryAsync(refund, allowBeyondAutoRetryLimit: false, ct);
                    return null;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-retry refund {RefundId} thất bại — thử lại chu kỳ sau.", refund.Id);
            }
        }
        return failed.Count;
    }

    public async Task<int> ProcessPendingRefundsAsync(CancellationToken ct = default)
    {
        var pending = await _uow.Returns.GetPendingRefundsAsync(50, ct);
        foreach (var refund in pending)
        {
            try
            {
                await _uow.ExecuteInTransactionAsync<object?>(async _ =>
                {
                    await SubmitPendingRefundAsync(refund, ct);
                    return null;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dispatch refund Pending {RefundId} thất bại.", refund.Id);
            }
        }
        return pending.Count;
    }

    public async Task<int> FailStaleProcessingRefundsAsync(CancellationToken ct = default)
    {
        var staleBefore = DateTime.UtcNow.AddMinutes(-ReturnWorkflow.RefundProcessingTimeoutMinutes);
        var stale = await _uow.Returns.GetStaleProcessingRefundsAsync(staleBefore, 50, ct);
        foreach (var refund in stale)
        {
            refund.MarkFailed();
            _logger.LogWarning("Refund {RefundId} timeout webhook; chuyển Processing → Failed.", refund.Id);
        }
        if (stale.Count > 0) await _uow.SaveChangesAsync(ct);
        return stale.Count;
    }

    public async Task<IServiceResult<RefundResponse>> SimulateResultAsync(
        Guid refundId, bool success, RmaActor actor, CancellationToken ct = default)
    {
        if (!actor.IsAdmin) return Fail(ApiStatusCodes.Forbidden, ApiStatusMessages.Returns.ManagerOnly);
        var refund = await _uow.Returns.GetRefundByIdAsync(refundId, ct);
        if (refund is null) return Fail(ApiStatusCodes.NotFound, ApiStatusMessages.Returns.RefundNotFound);
        if (refund.Status is RefundStatus.Completed or RefundStatus.Cancelled) return Ok(refund);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            if (refund.Status == RefundStatus.Pending)
                refund.MarkProcessing($"DEV-{refund.Id:N}", DateTime.UtcNow);
            else if (success && refund.Status is RefundStatus.Failed or RefundStatus.ManagerReview)
                refund.RetryToProcessing(refund.ProviderRefundId ?? $"DEV-{refund.Id:N}", DateTime.UtcNow);

            if (success && refund.Status == RefundStatus.Processing)
                await CompleteRefundAndTicketAsync(refund, actor.UserId, ct);
            else if (!success && refund.Status == RefundStatus.Processing)
                refund.MarkFailed();
            return null;
        }, ct);

        return Ok(refund);
    }

    // ===================== Helpers =====================

    /// <summary>Gọi lại cổng cho một refund Failed/ManagerReview; hết lượt → escalate ManagerReview.</summary>
    private async Task AttemptGatewayRetryAsync(
        Refund refund, bool allowBeyondAutoRetryLimit, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (!allowBeyondAutoRetryLimit && refund.RetryCount >= ReturnWorkflow.MaxRefundRetries)
        {
            refund.EscalateToManagerReview();
            return;
        }

        // Ghi nhận lượt thử trước khi gọi external provider để exception/timeout vẫn tăng retry_count.
        refund.RetryToProcessing(refund.ProviderRefundId, now);
        try
        {
            var rounded = (int)Math.Round(refund.Amount, MidpointRounding.AwayFromZero);
            long orderCode = 0;
            if (refund.TransactionId is not null)
            {
                var txn = await _uow.Transactions.GetLatestByOrderAsync(refund.OrderId, ct);
                if (txn is not null) orderCode = txn.OrderCode;
            }
            var result = await _gateway.RefundAsync(
                new RefundRequest(orderCode, rounded, refund.Note ?? "Retry hoàn tiền", refund.IdempotencyKey, refund.ProviderRefundId), ct);
            refund.SetProviderReference(result.ProviderRefundId);
            if (!result.Success)
            {
                refund.MarkFailed();
                if (refund.RetryCount >= ReturnWorkflow.MaxRefundRetries) refund.EscalateToManagerReview();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retry cổng hoàn tiền refund {RefundId} lỗi.", refund.Id);
            if (refund.Status == RefundStatus.Processing) refund.MarkFailed();
            if (refund.RetryCount >= ReturnWorkflow.MaxRefundRetries
                && refund.Status == RefundStatus.Failed) refund.EscalateToManagerReview();
        }
    }

    private async Task SubmitPendingRefundAsync(Refund refund, CancellationToken ct)
    {
        if (refund.Status != RefundStatus.Pending) return;
        var now = DateTime.UtcNow;
        try
        {
            var rounded = (int)Math.Round(refund.Amount, MidpointRounding.AwayFromZero);
            long orderCode = 0;
            if (refund.TransactionId is not null)
            {
                var txn = await _uow.Transactions.GetLatestByOrderAsync(refund.OrderId, ct);
                if (txn is not null) orderCode = txn.OrderCode;
            }

            var result = await _gateway.RefundAsync(
                new RefundRequest(orderCode, rounded, refund.Note ?? "Hoàn tiền", refund.IdempotencyKey), ct);
            refund.MarkProcessing(result.ProviderRefundId, now);
            if (!result.Success) refund.MarkFailed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gửi refund Pending {RefundId} sang cổng lỗi.", refund.Id);
            refund.MarkProcessing(null, now);
            refund.MarkFailed();
        }
    }

    /// <summary>Đánh dấu refund Completed rồi hoàn tất ticket + tạo công nợ + báo khách.</summary>
    private async Task CompleteRefundAndTicketAsync(Refund refund, Guid? actorId, CancellationToken ct)
    {
        refund.MarkCompleted(actorId, DateTime.UtcNow);
        await CompleteTicketAndLiabilityAsync(refund, actorId, ct);
    }

    private async Task CompleteTicketAndLiabilityAsync(Refund refund, Guid? actorId, CancellationToken ct)
    {
        var ticket = await _uow.Returns.GetWithGraphAsync(refund.ReturnRequestId, ct);
        if (ticket is null) return;

        if (ticket.Status == Domain.Enums.Sales.ReturnRequestStatus.Refunding)
            ticket.CompleteRefund();

        await _liability.CreateForRefundAsync(ticket, refund, ct);

        await _uow.Notifications.AddAsync(new Notification
        {
            UserId = ticket.CustomerId,
            Type = NotificationType.RefundCompleted,
            Title = "Đã hoàn tiền",
            Message = $"Khoản hoàn tiền {refund.Amount:#,##0} đ cho yêu cầu của bạn đã được xử lý.",
            ReferenceId = ticket.Id,
            ReferenceType = ReferenceType.Refund,
            IsRead = false,
        }, ct);
    }

    private IServiceResult<RefundResponse> Ok(Refund refund)
        => ServiceResult<RefundResponse>.Success(_mapper.Map<RefundResponse>(refund));

    private static ServiceResult<RefundResponse> Fail(int code, string message)
        => ServiceResult<RefundResponse>.Failure(code, message);
}
