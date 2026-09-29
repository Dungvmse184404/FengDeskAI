using FengDeskAI.Application.Features.Vendor.DTOs;

namespace FengDeskAI.Application.Features.Vendor.Services;

/// <summary>
/// Quy tắc tính phí sàn (thuần, không DB). Tỉ lệ ĐANG ÁP do Manager đặt ở <c>platform_fee_rates</c> và đọc qua
/// <see cref="IPlatformFeeService"/>; FE lấy qua <c>GET /api/platform/fee-policy</c>, không tự viết cứng tỉ lệ.
///
/// <para>
/// Tiền của mọi đơn đi qua sàn (PayOS vào tài khoản sàn; COD do nhà vận chuyển thu hộ theo token GHN của sàn),
/// nên sàn là bên chi lại cho nhà vườn:
/// <c>nhà vườn thực nhận = tiền hàng − phí sàn</c>; phí ship thuộc về sàn vì sàn là bên trả nhà vận chuyển.
/// </para>
///
/// <para>
/// Voucher do SÀN tài trợ (giai đoạn 2) được trừ vào chính phần phí sàn: tổng giảm giá sàn gánh cho một
/// delivery không vượt phí sàn của delivery đó — tức sàn không bao giờ bù lỗ
/// quá phần mình thu, và nhà vườn luôn nhận đủ <c>tiền hàng − phí sàn</c> bất kể khách dùng voucher gì.
/// </para>
///
/// Chi tiết & ví dụ: docs/adr/platform-fee-ledger.md.
/// </summary>
public static class PlatformFeePolicy
{
    /// <summary>
    /// Tỉ lệ khi bảng <c>platform_fee_rates</c> chưa có dòng nào (DB mới trước khi có ai đặt).
    /// Có cấu hình rồi thì không dùng tới.
    /// </summary>
    public const decimal DefaultCommissionRate = 0.08m;

    /// <summary>Trần tỉ lệ Manager được đặt — khớp CHECK ở DB.</summary>
    public const decimal MaxCommissionRate = 0.30m;

    /// <summary>
    /// Phí sàn của một khoản tiền hàng. Làm tròn tới đồng, nửa đồng làm tròn LÊN — FE dùng đúng quy tắc này
    /// để số "thực nhận" người bán thấy khớp từng đồng với số ghi sổ.
    /// </summary>
    public static decimal ComputeCommission(decimal subtotal, decimal rate)
        => Math.Round(subtotal * rate, 0, MidpointRounding.AwayFromZero);

    /// <summary>Nhà vườn thực nhận cho một khoản tiền hàng.</summary>
    public static decimal VendorNet(decimal subtotal, decimal rate) => subtotal - ComputeCommission(subtotal, rate);

    /// <summary>Bản công khai cho FE — <c>GET /api/platform/fee-policy</c>.</summary>
    public static PlatformFeePolicyResponse Describe(decimal commissionRate, DateTime? effectiveFrom = null) => new()
    {
        CommissionRate = commissionRate,
        // Voucher sàn tài trợ trừ vào chính phí sàn ⇒ trần giảm luôn bằng tỉ lệ phí.
        MaxPlatformFundedDiscountRate = commissionRate,
        PayoutHoldDays = PayoutPolicy.HoldDays,
        EffectiveFrom = effectiveFrom,
    };
}
