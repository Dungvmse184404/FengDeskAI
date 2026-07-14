using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Domain.StateMachines;

namespace FengDeskAI.Domain.Entities.Sales;

/// <summary>
/// Ticket RMA (trả hàng / hoàn tiền / đổi trả) — gắn với MỘT <see cref="Delivery"/> (phần hàng của một garden).
/// Quyết định do Staff nền tảng; Vendor chỉ góp ý/xác nhận nhận hàng (non-blocking). Việc chuyển trạng thái
/// được ĐÓNG GÓI qua các phương thức bên dưới (guard bằng <see cref="ReturnStateMachine"/>) — <see cref="Status"/>
/// không có setter public để không nơi nào set trạng thái tùy tiện.
/// </summary>
public class ReturnRequest : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid DeliveryId { get; set; }
    public Guid CustomerId { get; set; }

    public ReturnType Type { get; set; } = ReturnType.Refund;
    public ReturnRequestStatus Status { get; private set; } = ReturnRequestStatus.Requested;
    public ReturnReason Reason { get; set; } = ReturnReason.WrongItem;

    /// <summary>Mô tả chi tiết lý do do khách nhập.</summary>
    public string? ReasonDetail { get; set; }

    /// <summary>Tổng tiền dự kiến hoàn (snapshot theo các dòng trả).</summary>
    public decimal RefundAmount { get; set; }
    public RefundMethod RefundMethod { get; set; } = RefundMethod.Original;

    // Thông tin nhận tiền hoàn cho đơn COD (chuyển khoản) — null với đơn online hoàn về nguồn.
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }

    /// <summary>Mã vận đơn khách gửi hàng trả về (chỉ với lý do hàng vật lý).</summary>
    public string? ReturnTrackingCode { get; set; }

    // Phản hồi của vendor trong khung SLA (non-blocking).
    public VendorResponse VendorResponse { get; private set; } = VendorResponse.Pending;
    public DateTime? VendorResponseDeadline { get; private set; }

    /// <summary>Hạn chót khách bổ sung bằng chứng khi ticket ở NeedMoreEvidence.</summary>
    public DateTime? EvidenceDeadline { get; private set; }

    // Người ra quyết định cuối — LUÔN là Staff nền tảng (không bao giờ là Vendor).
    public Guid? DecidedBy { get; private set; }
    public DateTime? DecidedAt { get; private set; }

    // Cột cũ giữ lại để migration không destructive (flow v2 không dùng).
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectedReason { get; private set; }
    public DateTime? ReceivedAt { get; private set; }

    /// <summary>Delivery thay thế được tạo khi đổi hàng (Type = Exchange, IsExchange = true).</summary>
    public Guid? ReplacementDeliveryId { get; set; }

    public Order Order { get; set; } = null!;
    public Delivery Delivery { get; set; } = null!;
    public ICollection<ReturnItem> Items { get; set; } = new List<ReturnItem>();
    public ICollection<ReturnRequestImage> Images { get; set; } = new List<ReturnRequestImage>();
    public ICollection<ReturnStatusLog> StatusLogs { get; set; } = new List<ReturnStatusLog>();
    public Refund? Refund { get; set; }
    public VendorLiability? VendorLiability { get; set; }

    /// <summary>Ghi chú khi đổi trạng thái — không map DB, dùng để truyền vào status log.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? StatusChangeNote { get; set; }

    // ===================== Transitions (đóng gói) =====================

    private void TransitionTo(ReturnRequestStatus to)
    {
        if (!ReturnStateMachine.CanTransition(Status, to, Reason))
            throw new InvalidStateTransitionException(nameof(ReturnRequest), Status.ToString(), to.ToString());
        Status = to;
    }

    /// <summary>Khách tự hủy (chỉ khi còn Requested).</summary>
    public void Cancel() => TransitionTo(ReturnRequestStatus.Cancelled);

    /// <summary>Staff yêu cầu bổ sung bằng chứng, đặt hạn chót.</summary>
    public void RequestMoreEvidence(DateTime deadlineUtc)
    {
        TransitionTo(ReturnRequestStatus.NeedMoreEvidence);
        EvidenceDeadline = deadlineUtc;
    }

    /// <summary>Khách bổ sung bằng chứng → quay lại chờ duyệt.</summary>
    public void ResubmitEvidence()
    {
        TransitionTo(ReturnRequestStatus.Requested);
        EvidenceDeadline = null;
    }

    /// <summary>Quá hạn bổ sung bằng chứng → từ chối (worker gọi).</summary>
    public void RejectForEvidenceTimeout()
    {
        TransitionTo(ReturnRequestStatus.Rejected);
        RejectedReason = "Quá hạn bổ sung bằng chứng.";
    }

    /// <summary>Staff tiếp nhận: sang UnderReview, thông báo vendor + đặt SLA phản hồi (non-blocking).</summary>
    public void Accept(DateTime vendorResponseDeadlineUtc)
    {
        TransitionTo(ReturnRequestStatus.UnderReview);
        VendorResponse = VendorResponse.Pending;
        VendorResponseDeadline = vendorResponseDeadlineUtc;
    }

    /// <summary>Rẽ nhánh sau tiếp nhận theo lý do: cây chết → Reviewing (bỏ thu hồi), hàng vật lý → ReturnInTransit.</summary>
    public void RouteAfterAccept()
        => TransitionTo(Reason == ReturnReason.PlantHealth
            ? ReturnRequestStatus.Reviewing
            : ReturnRequestStatus.ReturnInTransit);

    /// <summary>Vendor xác nhận đã nhận hàng trả (KHÔNG quyết định kết quả).</summary>
    public void ConfirmItemReceived(DateTime nowUtc)
    {
        TransitionTo(ReturnRequestStatus.ItemReceived);
        ReceivedAt = nowUtc;
    }

    /// <summary>Chuyển sang bước Staff ra quyết định.</summary>
    public void MoveToReviewing() => TransitionTo(ReturnRequestStatus.Reviewing);

    /// <summary>Staff duyệt hoàn tiền.</summary>
    public void ApproveRefund(Guid staffId, DateTime nowUtc)
    {
        TransitionTo(ReturnRequestStatus.Refunding);
        DecidedBy = staffId;
        DecidedAt = nowUtc;
    }

    /// <summary>Staff duyệt đổi hàng.</summary>
    public void ApproveExchange(Guid staffId, DateTime nowUtc)
    {
        TransitionTo(ReturnRequestStatus.Exchanging);
        DecidedBy = staffId;
        DecidedAt = nowUtc;
    }

    /// <summary>Staff từ chối (không đủ căn cứ).</summary>
    public void Reject(Guid staffId, DateTime nowUtc, string reason)
    {
        TransitionTo(ReturnRequestStatus.Rejected);
        DecidedBy = staffId;
        DecidedAt = nowUtc;
        RejectedReason = reason;
    }

    /// <summary>Đổi hàng: đã tạo đơn thay thế → hoàn tất.</summary>
    public void CompleteExchange() => TransitionTo(ReturnRequestStatus.Completed);

    /// <summary>Đổi hàng nhưng hết hàng thay thế → fallback sang hoàn tiền.</summary>
    public void FallbackToRefund() => TransitionTo(ReturnRequestStatus.Refunding);

    /// <summary>Refund saga đạt Completed → ticket hoàn tất.</summary>
    public void CompleteRefund() => TransitionTo(ReturnRequestStatus.Completed);

    // Phản hồi vendor: KHÔNG đổi Status, không chặn khách.
    public void VendorAcknowledge()
    {
        EnsureVendorCanRespond();
        VendorResponse = VendorResponse.Acknowledged;
    }

    public void VendorDispute()
    {
        EnsureVendorCanRespond();
        VendorResponse = VendorResponse.Disputed;
    }

    private void EnsureVendorCanRespond()
    {
        if (ReturnStateMachine.IsTerminal(Status))
            throw new InvalidStateTransitionException(nameof(ReturnRequest), Status.ToString(), "VendorResponse");
    }
}
