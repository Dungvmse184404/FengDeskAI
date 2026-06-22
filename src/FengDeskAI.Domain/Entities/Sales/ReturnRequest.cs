using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.Enums.Sales;

namespace FengDeskAI.Domain.Entities.Sales;

/// <summary>
/// Yêu cầu trả hàng / đổi trả (RMA) — gắn với MỘT <see cref="Delivery"/> (phần hàng của một store).
/// Mỗi yêu cầu gồm nhiều dòng <see cref="ReturnItem"/> (trả từng sản phẩm + số lượng).
/// Người bán (store) duyệt, admin giám sát; hoàn tiền qua <see cref="Refund"/>.
/// </summary>
public class ReturnRequest : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid DeliveryId { get; set; }
    public Guid CustomerId { get; set; }

    public ReturnType Type { get; set; } = ReturnType.Refund;
    public ReturnRequestStatus Status { get; set; } = ReturnRequestStatus.Requested;
    public ReturnReason Reason { get; set; } = ReturnReason.Other;

    /// <summary>Mô tả chi tiết lý do do khách nhập.</summary>
    public string? ReasonDetail { get; set; }

    /// <summary>Tổng tiền dự kiến hoàn (snapshot theo các dòng trả).</summary>
    public decimal RefundAmount { get; set; }
    public RefundMethod RefundMethod { get; set; } = RefundMethod.Original;

    // Thông tin nhận tiền hoàn cho đơn COD (chuyển khoản) — null với đơn online hoàn về nguồn.
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }

    /// <summary>Mã vận đơn khách gửi hàng trả về (chiều logistics ngược).</summary>
    public string? ReturnTrackingCode { get; set; }

    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectedReason { get; set; }
    public DateTime? ReceivedAt { get; set; }

    /// <summary>Delivery thay thế được tạo khi đổi hàng (Type = Exchange).</summary>
    public Guid? ReplacementDeliveryId { get; set; }

    public Order Order { get; set; } = null!;
    public Delivery Delivery { get; set; } = null!;
    public ICollection<ReturnItem> Items { get; set; } = new List<ReturnItem>();
    public ICollection<ReturnRequestImage> Images { get; set; } = new List<ReturnRequestImage>();
    public ICollection<ReturnStatusLog> StatusLogs { get; set; } = new List<ReturnStatusLog>();
    public Refund? Refund { get; set; }

    /// <summary>Ghi chú khi đổi trạng thái — không map DB, dùng để truyền vào status log.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? StatusChangeNote { get; set; }
}
