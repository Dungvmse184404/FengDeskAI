using FengDeskAI.Application.Common.Formatting;
using FengDeskAI.Application.Features.Vendor.Services;
using FengDeskAI.Domain.Entities.Promotion;
using FengDeskAI.Domain.Enums.Promotion;

namespace FengDeskAI.Application.Features.Promotion.Services;

/// <summary>Tiền của một vườn trong đơn — đầu vào để chia khoản giảm.</summary>
public sealed record StoreChargeInput(Guid StoreId, decimal Subtotal, decimal ShippingFee);

/// <summary>
/// Khoản giảm của MỘT vườn, tách theo nơi bị trừ. Tách ba ngăn vì mỗi ngăn đi vào một bút toán khác
/// nhau: ship và tiền hàng do sàn tài trợ thì sàn chịu, còn phần người bán tài trợ trừ thẳng vào tiền
/// họ nhận (xem <c>LedgerService.PostDeliveryCompletedAsync</c>).
/// </summary>
public sealed record StoreDiscount(decimal Shipping, decimal PlatformItem, decimal SellerItem)
{
    public static readonly StoreDiscount Zero = new(0m, 0m, 0m);

    public decimal Total => Shipping + PlatformItem + SellerItem;
}

/// <summary>Kết quả tính voucher: đủ điều kiện hay không (kèm lý do), và khoản giảm của từng vườn.</summary>
public sealed record VoucherQuote(bool IsEligible, string? RejectReason, decimal TotalDiscount,
    IReadOnlyDictionary<Guid, StoreDiscount> DiscountByStore)
{
    public static VoucherQuote Reject(string reason)
        => new(false, reason, 0m, new Dictionary<Guid, StoreDiscount>());
}

/// <summary>
/// Tính khoản giảm của voucher — THUẦN, không đụng DB (giới hạn lượt dùng kiểm ở <c>VoucherService</c>).
///
/// <para>Điều kiện chung xét trên CẢ đơn: voucher đang bật, trong thời hạn, đúng tỉnh giao (nếu giới hạn),
/// tổng tiền hàng ≥ <see cref="Voucher.MinOrderSubtotal"/>.</para>
///
/// <para>Trần giảm phụ thuộc <see cref="VoucherType"/> — mỗi loại trừ vào một chỗ khác nhau:</para>
/// <list type="table">
/// <item><term><see cref="VoucherType.FreeShipping"/></term><description>trừ phí ship, sàn chịu;
///   trần <c>min(phí ship, phí sàn)</c> — sàn không bù lỗ quá phần mình thu.</description></item>
/// <item><term><see cref="VoucherType.PlatformDiscount"/></term><description>trừ tiền hàng, sàn chịu;
///   trần = phí sàn của vườn đó. Nhà vườn vẫn nhận đủ <c>tiền hàng − phí sàn</c>.</description></item>
/// <item><term><see cref="VoucherType.SellerDiscount"/></term><description>trừ tiền hàng, NGƯỜI BÁN chịu;
///   trần = tiền hàng của vườn. Hoa hồng sàn vẫn tính trên tiền hàng GỐC.</description></item>
/// <item><term><see cref="VoucherType.DemoFlatTotal"/></term><description>[DEMO] kéo tổng đơn về
///   <see cref="DemoFlatTotalTargetVnd"/>, trừ lần lượt ship → hoa hồng sàn → tiền người bán.</description></item>
/// </list>
///
/// <para><see cref="Voucher.MaxDiscountAmount"/> (nếu có) chặn TỔNG; chia lần lượt theo vườn có tiền hàng
/// lớn trước — thứ tự cố định để preview và checkout ra cùng một con số.</para>
/// </summary>
public static class ShippingVoucherCalculator
{
    /// <summary>
    /// [DEMO] Tổng tiền khách phải trả sau khi áp voucher <see cref="VoucherType.DemoFlatTotal"/>.
    /// Con số này chỉ phục vụ trình bày; đổi ở đây là đổi cho mọi mã loại đó.
    /// </summary>
    public const decimal DemoFlatTotalTargetVnd = 10_000m;

    public static VoucherQuote Quote(Voucher voucher, IReadOnlyList<StoreChargeInput> stores,
        Guid? destinationProvinceId, DateTime nowUtc, decimal commissionRate)
    {
        if (!voucher.IsActive) return VoucherQuote.Reject("Mã giảm giá đã ngừng áp dụng.");
        if (voucher.StartsAt is { } start && nowUtc < start) return VoucherQuote.Reject("Mã giảm giá chưa tới thời gian áp dụng.");
        if (voucher.EndsAt is { } end && nowUtc > end) return VoucherQuote.Reject("Mã giảm giá đã hết hạn.");
        if (voucher.ProvinceId is { } province && destinationProvinceId != province)
            return VoucherQuote.Reject("Mã giảm giá không áp dụng cho khu vực giao hàng này.");

        var orderSubtotal = stores.Sum(s => s.Subtotal);
        if (orderSubtotal < voucher.MinOrderSubtotal)
            return VoucherQuote.Reject($"Đơn cần tối thiểu {Vnd.Of(voucher.MinOrderSubtotal)} tiền hàng để dùng mã này.");

        var byStore = voucher.Type switch
        {
            VoucherType.FreeShipping => SplitByCap(voucher, stores, commissionRate, Bucket.Shipping),
            VoucherType.PlatformDiscount => SplitByCap(voucher, stores, commissionRate, Bucket.PlatformItem),
            VoucherType.SellerDiscount => SplitByCap(voucher, stores, commissionRate, Bucket.SellerItem),
            VoucherType.DemoFlatTotal => SplitToFlatTotal(stores, commissionRate),
            _ => null,
        };

        if (byStore is null) return VoucherQuote.Reject("Loại mã giảm giá này chưa được hỗ trợ.");

        var total = byStore.Values.Sum(d => d.Total);
        return total > 0m
            ? new VoucherQuote(true, null, total, byStore)
            : VoucherQuote.Reject("Đơn này không có khoản nào để mã giảm giá trừ vào.");
    }

    private enum Bucket { Shipping, PlatformItem, SellerItem }

    /// <summary>Chia khoản giảm vào MỘT ngăn, trần theo loại, tổng bị chặn bởi <c>MaxDiscountAmount</c>.</summary>
    private static Dictionary<Guid, StoreDiscount> SplitByCap(
        Voucher voucher, IReadOnlyList<StoreChargeInput> stores, decimal commissionRate, Bucket bucket)
    {
        var remaining = voucher.MaxDiscountAmount ?? decimal.MaxValue;
        var byStore = new Dictionary<Guid, StoreDiscount>();

        foreach (var store in stores.OrderByDescending(s => s.Subtotal).ThenBy(s => s.StoreId))
        {
            var commission = PlatformFeePolicy.ComputeCommission(store.Subtotal, commissionRate);
            var cap = bucket switch
            {
                Bucket.Shipping => Math.Min(store.ShippingFee, commission),
                Bucket.PlatformItem => Math.Min(store.Subtotal, commission),
                _ => store.Subtotal,
            };

            var amount = Math.Max(0m, Math.Min(cap, remaining));
            byStore[store.StoreId] = bucket switch
            {
                Bucket.Shipping => new StoreDiscount(amount, 0m, 0m),
                Bucket.PlatformItem => new StoreDiscount(0m, amount, 0m),
                _ => new StoreDiscount(0m, 0m, amount),
            };
            remaining -= amount;
        }

        return byStore;
    }

    /// <summary>
    /// [DEMO] Kéo tổng đơn về <see cref="DemoFlatTotalTargetVnd"/>. Trừ theo thứ tự ship → hoa hồng sàn →
    /// tiền người bán, vì đó là thứ tự "ai chịu thiệt trước" hợp lý nhất khi trình bày: sàn gánh trước,
    /// hết phần sàn mới tới người bán.
    /// </summary>
    private static Dictionary<Guid, StoreDiscount> SplitToFlatTotal(
        IReadOnlyList<StoreChargeInput> stores, decimal commissionRate)
    {
        var payable = stores.Sum(s => s.Subtotal + s.ShippingFee);
        var remaining = Math.Max(0m, payable - DemoFlatTotalTargetVnd);
        var byStore = new Dictionary<Guid, StoreDiscount>();

        foreach (var store in stores.OrderByDescending(s => s.Subtotal).ThenBy(s => s.StoreId))
        {
            var shipping = Take(ref remaining, store.ShippingFee);
            var platform = Take(ref remaining, PlatformFeePolicy.ComputeCommission(store.Subtotal, commissionRate));
            var seller = Take(ref remaining, store.Subtotal - platform);
            byStore[store.StoreId] = new StoreDiscount(shipping, platform, seller);
        }

        return byStore;
    }

    /// <summary>Lấy nhiều nhất <paramref name="available"/> từ phần còn phải trừ, trừ luôn vào bộ đếm.</summary>
    private static decimal Take(ref decimal remaining, decimal available)
    {
        var taken = Math.Max(0m, Math.Min(available, remaining));
        remaining -= taken;
        return taken;
    }
}
