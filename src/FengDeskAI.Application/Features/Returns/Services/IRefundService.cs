using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Returns.DTOs;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Payment;

namespace FengDeskAI.Application.Features.Returns.Services;

/// <summary>
/// Refund sub-saga (do Manager giám sát). Nền tảng ứng tiền hoàn cho khách ngay khi Staff duyệt;
/// chống hoàn trùng bằng idempotency key; <see cref="RefundStatus.Failed"/> không dead-end
/// (auto-retry rồi escalate Manager). Xác nhận thủ công BẮT BUỘC audit trail.
/// </summary>
public interface IRefundService
{
    /// <summary>
    /// Tạo & khởi động lệnh hoàn tiền cho ticket (PHẢI gọi trong transaction của ticket).
    /// Idempotent theo ticket: nếu đã có refund thì trả về refund cũ, không tạo mới.
    /// </summary>
    Task<Refund> CreateRefundAsync(ReturnRequest ticket, decimal amount, RefundMethod method, string reason, CancellationToken ct = default);

    /// <summary>Webhook cổng báo kết quả hoàn tiền — verify chữ ký + xử lý idempotent.</summary>
    Task<IServiceResult> HandleWebhookAsync(string rawJsonBody, CancellationToken ct = default);

    /// <summary>Manager retry thủ công một refund (Failed hoặc ManagerReview).</summary>
    Task<IServiceResult<RefundResponse>> RetryRefundAsync(Guid refundId, RmaActor actor, CancellationToken ct = default);

    /// <summary>Manager xác nhận thủ công đã hoàn tiền (từ ManagerReview) — BẮT BUỘC manual_reason + evidence_url.</summary>
    Task<IServiceResult<RefundResponse>> ManagerConfirmRefundAsync(Guid refundId, RmaActor actor, ManagerConfirmRefundRequest request, CancellationToken ct = default);

    /// <summary>Manager hủy refund khi phát hiện gian lận (chỉ khi còn Pending).</summary>
    Task<IServiceResult<RefundResponse>> ManagerCancelRefundAsync(Guid refundId, RmaActor actor, CancellationToken ct = default);

    /// <summary>Danh sách refund cần Manager (Failed / ManagerReview).</summary>
    Task<IServiceResult<PagedResult<RefundResponse>>> GetForManagerAsync(PageRequest page, CancellationToken ct = default);

    /// <summary>Worker: auto-retry các refund Failed còn lượt; hết lượt → escalate ManagerReview. Trả số đã xử lý.</summary>
    Task<int> AutoProcessFailedRefundsAsync(CancellationToken ct = default);
}
