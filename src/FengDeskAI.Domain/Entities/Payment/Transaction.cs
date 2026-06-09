using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Payment;

namespace FengDeskAI.Domain.Entities.Payment;

/// <summary>
/// Giao dịch thanh toán cho một order. <see cref="OrderCode"/> là mã số PayOS dùng
/// đối soát webhook (PayOS yêu cầu orderCode kiểu số, không dùng được Guid).
/// </summary>
public class Transaction : BaseEntity
{
    public Guid OrderId { get; set; }

    /// <summary>Mã số gửi cho PayOS (orderCode) — khóa đối soát khi nhận webhook.</summary>
    public long OrderCode { get; set; }

    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.PayOS;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>Mã giao dịch phía provider (PayOS reference).</summary>
    public string? ProviderTransactionId { get; set; }
    public DateTime? PaidAt { get; set; }

    public Order Order { get; set; } = null!;
}
