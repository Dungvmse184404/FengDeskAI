using FengDeskAI.Application.Features.Vendor.DTOs;

namespace FengDeskAI.Application.Features.Vendor.Services;

/// <summary>
/// Chính sách phí sàn — MỘT nguồn sự thật cho BE (chốt vào từng delivery, ghi sổ cái) lẫn FE (người bán xem
/// trước "giá thực nhận" qua <c>GET /api/platform/fee-policy</c>). FE không được tự viết cứng tỉ lệ.
///
/// <para>
/// Tiền của mọi đơn đi qua sàn (PayOS vào tài khoản sàn; COD do nhà vận chuyển thu hộ theo token GHN của sàn),
/// nên sàn là bên chi lại cho nhà vườn:
/// <c>nhà vườn thực nhận = tiền hàng − phí sàn</c>; phí ship thuộc về sàn vì sàn là bên trả nhà vận chuyển.
/// </para>
///
/// <para>
/// Voucher do SÀN tài trợ (giai đoạn 2) được trừ vào chính phần phí sàn: tổng giảm giá sàn gánh cho một
/// delivery không vượt <see cref="MaxPlatformFundedDiscountRate"/> × tiền hàng — tức sàn không bao giờ bù lỗ
/// quá phần mình thu, và nhà vườn luôn nhận đủ <c>tiền hàng − phí sàn</c> bất kể khách dùng voucher gì.
/// </para>
///
/// Chi tiết & ví dụ: docs/adr/platform-fee-ledger.md.
/// </summary>
public static class PlatformFeePolicy
{
    /// <summary>Phí sàn trên tiền hàng của delivery đã giao thành công. Chốt vào <c>deliveries.commission_rate</c> lúc tạo.</summary>
    public const decimal CommissionRate = 0.08m;

    /// <summary>Trần giảm giá do sàn tài trợ trên mỗi delivery — bằng đúng phí sàn (giai đoạn 2: voucher).</summary>
    public const decimal MaxPlatformFundedDiscountRate = CommissionRate;

    /// <summary>
    /// Phí sàn của một khoản tiền hàng. Làm tròn tới đồng, nửa đồng làm tròn LÊN — FE dùng đúng quy tắc này
    /// để số "thực nhận" người bán thấy khớp từng đồng với số ghi sổ.
    /// </summary>
    public static decimal ComputeCommission(decimal subtotal, decimal rate)
        => Math.Round(subtotal * rate, 0, MidpointRounding.AwayFromZero);

    /// <summary>Nhà vườn thực nhận cho một khoản tiền hàng.</summary>
    public static decimal VendorNet(decimal subtotal, decimal rate) => subtotal - ComputeCommission(subtotal, rate);

    /// <summary>Bản công khai cho FE — <c>GET /api/platform/fee-policy</c>.</summary>
    public static PlatformFeePolicyResponse Describe() => new()
    {
        CommissionRate = CommissionRate,
        MaxPlatformFundedDiscountRate = MaxPlatformFundedDiscountRate,
        PayoutHoldDays = PayoutPolicy.HoldDays,
    };
}
