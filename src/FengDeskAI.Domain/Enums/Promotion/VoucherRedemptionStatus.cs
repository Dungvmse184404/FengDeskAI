namespace FengDeskAI.Domain.Enums.Promotion;

public enum VoucherRedemptionStatus
{
    /// <summary>Đang giữ một lượt dùng của voucher.</summary>
    Applied,

    /// <summary>Đơn hủy/hết hạn — lượt dùng đã trả lại.</summary>
    Released,
}
