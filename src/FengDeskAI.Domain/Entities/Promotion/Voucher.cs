using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Promotion;

namespace FengDeskAI.Domain.Entities.Promotion;

/// <summary>
/// Mã giảm giá. Điều kiện áp dụng + trần giảm nằm ở đây; cách chia khoản giảm xuống từng vườn nằm ở
/// <c>ShippingVoucherCalculator</c> (Application) vì phụ thuộc chính sách phí sàn.
/// </summary>
public class Voucher : BaseEntity
{
    /// <summary>Mã khách nhập — lưu CHỮ HOA, duy nhất.</summary>
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public VoucherType Type { get; set; } = VoucherType.FreeShipping;
    public VoucherFundingSource FundedBy { get; set; } = VoucherFundingSource.Platform;

    /// <summary>Tổng tiền hàng tối thiểu của CẢ đơn (mọi vườn cộng lại).</summary>
    public decimal MinOrderSubtotal { get; set; }

    /// <summary>Trần giảm trên cả đơn; null = chỉ bị chặn bởi phí ship và phí sàn.</summary>
    public decimal? MaxDiscountAmount { get; set; }

    /// <summary>Chỉ áp cho địa chỉ giao thuộc tỉnh này; null = toàn quốc.</summary>
    public Guid? ProvinceId { get; set; }

    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }

    /// <summary>Tổng lượt dùng tối đa; null = không giới hạn.</summary>
    public int? UsageLimit { get; set; }
    public int? UsageLimitPerUser { get; set; }

    /// <summary>Số lượt đang giữ (đơn chưa hủy). Chỉ đổi bằng câu UPDATE nguyên tử ở repository.</summary>
    public int UsedCount { get; set; }

    /// <summary>Tự áp khi khách không nhập mã và đơn đủ điều kiện.</summary>
    public bool IsAutoApply { get; set; }
    public bool IsActive { get; set; } = true;
}
