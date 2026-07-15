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

    // ===================== Tạo & khởi động (gọi trong transaction của ticket) =====================

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

        var now = DateTime.UtcNow;
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
        long orderCode = 0;
        if (method == RefundMethod.Original)
        {
            var txn = await _uow.Transactions.GetLatestByOrderAsync(ticket.OrderId, ct);
            if (txn is not null && txn.Status == PaymentStatus.Paid)
            {
                refund.TransactionId = txn.Id;
                orderCode = txn.OrderCode;
            }
        }

        // Gọi cổng (mock idempotent). Pending → Processing; nếu submit lỗi → Processing → Failed để worker retry.
        try
        {
            var rounded = (int)Math.Round(amount, MidpointRounding.AwayFromZero);
            var result = await _gateway.RefundAsync(new RefundRequest(orderCode, rounded, reason, key), ct);
            refund.MarkProcessing(result.ProviderRefundId, now);
            if (!result.Success)
            {
                _logger.LogWarning("Refund {Key} cổng trả thất bại: {Code} {Message}", key, result.Code, result.Message);
                refund.MarkFailed();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gọi cổng hoàn tiền thất bại cho ticket {TicketId} — đánh Failed để worker retry.", ticket.Id);
            refund.MarkProcessing(null, now);
            refund.MarkFailed();
        }

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
        if (refund.Status != RefundStatus.Processing)
            return ServiceResult.Success(ApiStatusMessages.Returns.WebhookProcessed);

        await _uow.ExecuteInTransactionAsync<object?>(async _ =>
        {
            if (verified.Success)
                await CompleteRefundAndTicketAsync(refund, actorId: null, ct);
            else
                refund.MarkFailed();
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
            await AttemptGatewayRetryAsync(refund, ct);
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

        refund.Cancel(actor.UserId);
        await _uow.SaveChangesAsync(ct);
        return Ok(refund);
    }

    public async Task<IServiceResult<PagedResult<RefundResponse>>> GetForManagerAsync(PageRequest page, CancellationToken ct = default)
    {
        var (items, total) = await _uow.Returns.GetRefundsForManagerAsync(page.Skip, page.PageSize, ct);
        return ServiceResult<PagedResult<RefundResponse>>.Success(
            new PagedResult<RefundResponse>(_mapper.Map<List<RefundResponse>>(items), page.Page, page.PageSize, total));
    }

    // ===================== Worker =====================

    public async Task<int> AutoProcessFailedRefundsAsync(CancellationToken ct = default)
    {
        var failed = await _uow.Returns.GetRetryableFailedRefundsAsync(ReturnWorkflow.MaxRefundRetries, 50, ct);
        if (failed.Count == 0) return 0;

        foreach (var refund in failed)
        {
            try
            {
                await _uow.ExecuteInTransactionAsync<object?>(async _ =>
                {
                    await AttemptGatewayRetryAsync(refund, ct);
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

    // ===================== Helpers =====================

    /// <summary>Gọi lại cổng cho một refund Failed/ManagerReview; hết lượt → escalate ManagerReview.</summary>
    private async Task AttemptGatewayRetryAsync(Refund refund, CancellationToken ct)
    {
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
                new RefundRequest(orderCode, rounded, refund.Note ?? "Retry hoàn tiền", refund.IdempotencyKey, refund.ProviderRefundId), ct);
            refund.RetryToProcessing(result.ProviderRefundId, now);
            if (!result.Success)
            {
                refund.MarkFailed();
                if (refund.RetryCount >= ReturnWorkflow.MaxRefundRetries) refund.EscalateToManagerReview();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retry cổng hoàn tiền refund {RefundId} lỗi.", refund.Id);
            // RetryToProcessing đã đưa về Processing? chưa — nên xử lý an toàn:
            if (refund.Status == RefundStatus.Processing) refund.MarkFailed();
            if (refund.RetryCount >= ReturnWorkflow.MaxRefundRetries
                && refund.Status == RefundStatus.Failed) refund.EscalateToManagerReview();
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
