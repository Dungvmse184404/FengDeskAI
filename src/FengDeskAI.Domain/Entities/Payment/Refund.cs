using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.StateMachines;

namespace FengDeskAI.Domain.Entities.Payment;

/// <summary>
/// Refund sub-saga cho một ticket RMA (do Manager giám sát). Chống hoàn tiền trùng bằng
/// <see cref="IdempotencyKey"/> (UNIQUE). Chuyển trạng thái được đóng gói (guard bằng
/// <see cref="RefundStateMachine"/>); <see cref="Status"/> không có setter public.
/// </summary>
public class Refund : BaseEntity
{
    /// <summary>ticket_id — ticket RMA sở hữu refund này.</summary>
    public Guid ReturnRequestId { get; set; }

    /// <summary>order_group_id — ánh xạ sang Order gốc của ticket.</summary>
    public Guid OrderId { get; set; }

    /// <summary>Giao dịch thanh toán gốc (nếu hoàn về nguồn) — null với COD.</summary>
    public Guid? TransactionId { get; set; }

    public decimal Amount { get; set; }
    public RefundMethod Method { get; set; } = RefundMethod.Original;
    public RefundStatus Status { get; private set; } = RefundStatus.Pending;

    /// <summary>Cổng thanh toán xử lý hoàn tiền.</summary>
    public string Gateway { get; set; } = "payos";

    /// <summary>Mã tham chiếu phía cổng (gateway_ref / provider refund id).</summary>
    public string? ProviderRefundId { get; set; }

    /// <summary>Khóa idempotency — UNIQUE, chống thực thi hoàn tiền 2 lần cho cùng ticket.</summary>
    public string IdempotencyKey { get; set; } = null!;

    public int RetryCount { get; private set; }

    // Can thiệp thủ công (Manager). is_manual = true BẮT BUỘC đủ 3 trường dưới (enforce ở ManagerComplete).
    public bool IsManual { get; private set; }
    public string? ManualReason { get; private set; }
    public string? EvidenceUrl { get; private set; }
    public Guid? PerformedBy { get; private set; }

    public Guid? ProcessedBy { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? Note { get; set; }

    public ReturnRequest ReturnRequest { get; set; } = null!;
    public Order Order { get; set; } = null!;

    // ===================== Transitions (đóng gói) =====================

    private void TransitionTo(RefundStatus to)
    {
        if (!RefundStateMachine.CanTransition(Status, to))
            throw new InvalidStateTransitionException(nameof(Refund), Status.ToString(), to.ToString());
        Status = to;
    }

    /// <summary>Đã gọi cổng thành công → chờ webhook.</summary>
    public void MarkProcessing(string? gatewayRef, DateTime nowUtc)
    {
        TransitionTo(RefundStatus.Processing);
        if (!string.IsNullOrWhiteSpace(gatewayRef)) ProviderRefundId = gatewayRef;
        ProcessedAt ??= nowUtc;
    }

    /// <summary>Webhook báo lỗi / timeout → Failed (không dead-end).</summary>
    public void MarkFailed() => TransitionTo(RefundStatus.Failed);

    /// <summary>Hết lượt retry → chờ Manager can thiệp.</summary>
    public void EscalateToManagerReview() => TransitionTo(RefundStatus.ManagerReview);

    /// <summary>Auto-retry (Failed) hoặc Manager retry thủ công (ManagerReview) → gọi lại cổng.</summary>
    public void RetryToProcessing(string? gatewayRef, DateTime nowUtc)
    {
        RetryCount++;
        TransitionTo(RefundStatus.Processing);
        if (!string.IsNullOrWhiteSpace(gatewayRef)) ProviderRefundId = gatewayRef;
        ProcessedAt = nowUtc;
    }

    /// <summary>Webhook thành công → đã hoàn tiền cho khách.</summary>
    public void MarkCompleted(Guid? actorId, DateTime nowUtc)
    {
        TransitionTo(RefundStatus.Completed);
        ProcessedBy ??= actorId;
        CompletedAt = nowUtc;
    }

    /// <summary>Manager xác nhận thủ công đã hoàn tiền — BẮT BUỘC audit trail đầy đủ.</summary>
    public void ManagerComplete(string manualReason, string evidenceUrl, Guid performedBy, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(manualReason))
            throw new ArgumentException("manual_reason bắt buộc khi hoàn tiền thủ công.", nameof(manualReason));
        if (string.IsNullOrWhiteSpace(evidenceUrl))
            throw new ArgumentException("evidence_url bắt buộc khi hoàn tiền thủ công.", nameof(evidenceUrl));
        if (performedBy == Guid.Empty)
            throw new ArgumentException("performed_by bắt buộc khi hoàn tiền thủ công.", nameof(performedBy));

        TransitionTo(RefundStatus.Completed);
        IsManual = true;
        ManualReason = manualReason;
        EvidenceUrl = evidenceUrl;
        PerformedBy = performedBy;
        ProcessedBy ??= performedBy;
        CompletedAt = nowUtc;
    }

    /// <summary>Manager hủy (phát hiện gian lận trước khi tiền đi) — chỉ khi còn Pending.</summary>
    public void Cancel(Guid actorId)
    {
        TransitionTo(RefundStatus.Cancelled);
        ProcessedBy = actorId;
    }
}
