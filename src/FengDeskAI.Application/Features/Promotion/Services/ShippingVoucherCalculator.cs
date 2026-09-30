using FengDeskAI.Application.Common.Formatting;
using FengDeskAI.Application.Features.Vendor.Services;
using FengDeskAI.Domain.Entities.Promotion;
using FengDeskAI.Domain.Enums.Promotion;

namespace FengDeskAI.Application.Features.Promotion.Services;

/// <summary>Tiền của một vườn trong đơn — đầu vào để chia khoản giảm.</summary>
public sealed record StoreChargeInput(Guid StoreId, decimal Subtotal, decimal ShippingFee);

/// <summary>Kết quả tính voucher: đủ điều kiện hay không (kèm lý do), và khoản giảm của từng vườn.</summary>
public sealed record VoucherQuote(bool IsEligible, string? RejectReason, decimal TotalDiscount,
    IReadOnlyDictionary<Guid, decimal> DiscountByStore)
{
    public static VoucherQuote Reject(string reason)
        => new(false, reason, 0m, new Dictionary<Guid, decimal>());
}

/// <summary>
/// Tính voucher miễn phí vận chuyển — THUẦN, không đụng DB (giới hạn lượt dùng kiểm ở <c>VoucherService</c>).
///
/// Luật (docs/adr/voucher-freeship.md):
/// <list type="number">
/// <item>Điều kiện xét trên CẢ đơn: voucher đang bật, trong thời hạn, đúng tỉnh giao (nếu giới hạn), tổng tiền
///   hàng ≥ <see cref="Voucher.MinOrderSubtotal"/>.</item>
/// <item>Mỗi vườn được giảm tối đa <c>min(phí ship của vườn, phí sàn của vườn)</c> — sàn tài trợ nên khoản giảm
///   trừ vào đúng phần 8% sàn thu trên vườn đó, không bao giờ vượt (sàn không bù lỗ quá phần mình thu).</item>
/// <item><see cref="Voucher.MaxDiscountAmount"/> (nếu có) chặn tổng; chia lần lượt theo vườn có tiền hàng lớn
///   trước — thứ tự cố định để preview và checkout ra cùng một con số.</item>
/// </list>
/// </summary>
public static class ShippingVoucherCalculator
{
    public static VoucherQuote Quote(Voucher voucher, IReadOnlyList<StoreChargeInput> stores,
        Guid? destinationProvinceId, DateTime nowUtc, decimal commissionRate)
    {
        if (!voucher.IsActive) return VoucherQuote.Reject("Mã giảm giá đã ngừng áp dụng.");
        if (voucher.StartsAt is { } start && nowUtc < start) return VoucherQuote.Reject("Mã giảm giá chưa tới thời gian áp dụng.");
        if (voucher.EndsAt is { } end && nowUtc > end) return VoucherQuote.Reject("Mã giảm giá đã hết hạn.");
        if (voucher.Type != VoucherType.FreeShipping || voucher.FundedBy != VoucherFundingSource.Platform)
            return VoucherQuote.Reject("Loại mã giảm giá này chưa được hỗ trợ.");
        if (voucher.ProvinceId is { } province && destinationProvinceId != province)
            return VoucherQuote.Reject("Mã giảm giá không áp dụng cho khu vực giao hàng này.");

        var orderSubtotal = stores.Sum(s => s.Subtotal);
        if (orderSubtotal < voucher.MinOrderSubtotal)
            return VoucherQuote.Reject($"Đơn cần tối thiểu {Vnd.Of(voucher.MinOrderSubtotal)} tiền hàng để dùng mã này.");

        var remaining = voucher.MaxDiscountAmount ?? decimal.MaxValue;
        var byStore = new Dictionary<Guid, decimal>();
        foreach (var store in stores.OrderByDescending(s => s.Subtotal).ThenBy(s => s.StoreId))
        {
            var cap = Math.Min(store.ShippingFee, PlatformFeePolicy.ComputeCommission(store.Subtotal, commissionRate));
            var discount = Math.Max(0m, Math.Min(cap, remaining));
            byStore[store.StoreId] = discount;
            remaining -= discount;
        }

        var total = byStore.Values.Sum();
        return total > 0m
            ? new VoucherQuote(true, null, total, byStore)
            : VoucherQuote.Reject("Đơn không có phí vận chuyển để giảm.");
    }
}
