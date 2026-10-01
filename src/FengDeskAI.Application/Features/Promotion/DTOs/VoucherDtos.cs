using FengDeskAI.Domain.Enums.Promotion;

namespace FengDeskAI.Application.Features.Promotion.DTOs;

public class VoucherResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public VoucherType Type { get; set; }
    public VoucherFundingSource FundedBy { get; set; }
    public decimal MinOrderSubtotal { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public Guid? ProvinceId { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public int? UsageLimit { get; set; }
    public int? UsageLimitPerUser { get; set; }
    public int UsedCount { get; set; }
    public bool IsAutoApply { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Manager tạo voucher. Hiện chỉ hỗ trợ miễn phí vận chuyển do sàn tài trợ.</summary>
public class CreateVoucherRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal MinOrderSubtotal { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public Guid? ProvinceId { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public int? UsageLimit { get; set; }
    public int? UsageLimitPerUser { get; set; }
    public bool IsAutoApply { get; set; }

    /// <summary>Loại giảm giá — quyết định khoản giảm trừ vào đâu. Bỏ trống = <c>FreeShipping</c> (hành vi cũ).</summary>
    public VoucherType? Type { get; set; }

    /// <summary>Ai tài trợ. Bỏ trống = suy ra từ <see cref="Type"/>.</summary>
    public VoucherFundingSource? FundedBy { get; set; }
}

public class SetVoucherActiveRequest
{
    public bool IsActive { get; set; }
}

/// <summary>Voucher đã áp vào một đơn / một lần xem trước.</summary>
public class AppliedVoucherResponse
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal ShippingDiscount { get; set; }
}
