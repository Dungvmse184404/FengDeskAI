using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Promotion;

namespace FengDeskAI.Domain.Entities.Promotion;

/// <summary>Một lượt dùng voucher của một đơn — căn cứ đếm giới hạn theo người dùng và để trả lượt khi hủy đơn.</summary>
public class VoucherRedemption : BaseEntity
{
    public Guid VoucherId { get; set; }
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal DiscountAmount { get; set; }
    public VoucherRedemptionStatus Status { get; set; } = VoucherRedemptionStatus.Applied;

    public Voucher Voucher { get; set; } = null!;
}
