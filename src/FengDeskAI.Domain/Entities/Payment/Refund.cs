using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Payment;

namespace FengDeskAI.Domain.Entities.Payment;

/// <summary>
/// Lệnh hoàn tiền cho một <see cref="ReturnRequest"/>. Với đơn PayOS hoàn về nguồn
/// (<see cref="ProviderRefundId"/> là mã do cổng trả về); với đơn COD hoàn qua chuyển khoản
/// và Admin/Finance xác nhận thủ công khi đã chuyển tiền.
/// </summary>
public class Refund : BaseEntity
{
    public Guid ReturnRequestId { get; set; }
    public Guid OrderId { get; set; }

    /// <summary>Giao dịch thanh toán gốc (nếu hoàn về nguồn) — null với COD.</summary>
    public Guid? TransactionId { get; set; }

    public decimal Amount { get; set; }
    public RefundMethod Method { get; set; } = RefundMethod.Original;
    public RefundStatus Status { get; set; } = RefundStatus.Pending;

    /// <summary>Mã hoàn tiền phía cổng thanh toán (PayOS).</summary>
    public string? ProviderRefundId { get; set; }

    public Guid? ProcessedBy { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Note { get; set; }

    public ReturnRequest ReturnRequest { get; set; } = null!;
    public Order Order { get; set; } = null!;
}
