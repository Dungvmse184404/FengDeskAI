using FengDeskAI.Application.Features.Promotion.Services;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Promotion;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Promotion;
using Moq;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// Voucher miễn phí vận chuyển (docs/adr/voucher-freeship.md). Luật cốt lõi do chủ dự án chốt: voucher sàn tài
/// trợ trừ vào phí sàn ⇒ khoản giảm của MỖI vườn ≤ min(phí ship của vườn, 8% tiền hàng của vườn).
/// </summary>
public class ShippingVoucherCalculatorTests
{
    private const decimal Rate = 0.08m;
    private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private static Voucher FreeShip500(Action<Voucher>? tweak = null)
    {
        var v = new Voucher
        {
            Code = "FREESHIP500",
            Name = "Freeship 500k",
            MinOrderSubtotal = 500_000m,
            IsAutoApply = true,
            IsActive = true,
        };
        tweak?.Invoke(v);
        return v;
    }

    private static StoreChargeInput Store(decimal subtotal, decimal fee) => new(Guid.NewGuid(), subtotal, fee);

    [Fact]
    public void Quote_SubtotalOneDongBelowThreshold_IsRejected()
    {
        var quote = ShippingVoucherCalculator.Quote(FreeShip500(), [Store(499_999m, 30_000m)], null, Now, Rate);

        Assert.False(quote.IsEligible);
        Assert.Equal(0m, quote.TotalDiscount);
    }

    [Fact]
    public void Quote_SubtotalExactlyAtThreshold_WaivesWholeShippingFee()
    {
        var store = Store(500_000m, 30_000m);

        var quote = ShippingVoucherCalculator.Quote(FreeShip500(), [store], null, Now, Rate);

        Assert.True(quote.IsEligible);
        Assert.Equal(30_000m, quote.DiscountByStore[store.StoreId]); // 8% × 500k = 40k ≥ 30k
    }

    [Fact]
    public void Quote_MultiStore_EachStoreCappedByItsOwnCommission()
    {
        // Đơn 510k qua ngưỡng, nhưng vườn nhỏ (60k) chỉ gánh được 8% × 60k = 4 800 — sàn không bù lỗ quá phần mình thu.
        var big = Store(450_000m, 30_000m);
        var small = Store(60_000m, 30_000m);

        var quote = ShippingVoucherCalculator.Quote(FreeShip500(), [big, small], null, Now, Rate);

        Assert.Equal(30_000m, quote.DiscountByStore[big.StoreId]);
        Assert.Equal(4_800m, quote.DiscountByStore[small.StoreId]);
        Assert.Equal(34_800m, quote.TotalDiscount);
    }

    [Theory]
    [InlineData(500_000, 30_000)]
    [InlineData(700_000, 15_000)]
    [InlineData(123_457, 99_000)]
    public void Quote_DiscountNeverExceedsCommissionOrFee(decimal subtotal, decimal fee)
    {
        var store = Store(subtotal, fee);
        var quote = ShippingVoucherCalculator.Quote(FreeShip500(v => v.MinOrderSubtotal = 0), [store], null, Now, Rate);

        var discount = quote.DiscountByStore[store.StoreId];
        Assert.True(discount <= fee);
        Assert.True(discount <= Math.Round(subtotal * Rate, 0, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void Quote_MaxDiscount_CapsOrderTotal()
    {
        var a = Store(600_000m, 30_000m);
        var b = Store(400_000m, 30_000m);

        var quote = ShippingVoucherCalculator.Quote(FreeShip500(v => v.MaxDiscountAmount = 40_000m), [a, b], null, Now, Rate);

        Assert.Equal(40_000m, quote.TotalDiscount);
        Assert.Equal(30_000m, quote.DiscountByStore[a.StoreId]); // vườn tiền hàng lớn được chia trước
        Assert.Equal(10_000m, quote.DiscountByStore[b.StoreId]);
    }

    [Fact]
    public void Quote_ProvinceScoped_OtherProvince_IsRejected()
    {
        var hcm = Guid.NewGuid();
        var voucher = FreeShip500(v => v.ProvinceId = hcm);

        Assert.False(ShippingVoucherCalculator.Quote(voucher, [Store(600_000m, 30_000m)], Guid.NewGuid(), Now, Rate).IsEligible);
        Assert.True(ShippingVoucherCalculator.Quote(voucher, [Store(600_000m, 30_000m)], hcm, Now, Rate).IsEligible);
    }

    [Theory]
    [InlineData(false, null, null)]
    [InlineData(true, 1, null)]   // chưa bắt đầu
    [InlineData(true, null, -1)]  // đã hết hạn
    public void Quote_InactiveOrOutsideWindow_IsRejected(bool active, int? startsInDays, int? endsInDays)
    {
        var voucher = FreeShip500(v =>
        {
            v.IsActive = active;
            v.StartsAt = startsInDays is { } s ? Now.AddDays(s) : null;
            v.EndsAt = endsInDays is { } e ? Now.AddDays(e) : null;
        });

        Assert.False(ShippingVoucherCalculator.Quote(voucher, [Store(600_000m, 30_000m)], null, Now, Rate).IsEligible);
    }

    [Fact]
    public void Quote_NoShippingFee_IsNotEligible()
        => Assert.False(ShippingVoucherCalculator.Quote(FreeShip500(), [Store(600_000m, 0m)], null, Now, Rate).IsEligible);

    // ===================== VoucherService: giới hạn lượt =====================

    [Fact]
    public async Task Select_ExplicitCodeExhausted_ReturnsError()
    {
        var voucher = FreeShip500(v => { v.UsageLimit = 5; v.UsedCount = 5; });
        var service = ServiceWith(voucher);

        var selection = await service.SelectAsync(Guid.NewGuid(), " freeship500 ", [Store(600_000m, 30_000m)], null, Rate);

        Assert.NotNull(selection.Error);
        Assert.Equal(0m, selection.TotalDiscount);
    }

    [Fact]
    public async Task Select_NoCode_PerUserLimitReached_FallsBackToNoVoucher()
    {
        var voucher = FreeShip500(v => v.UsageLimitPerUser = 1);
        var service = ServiceWith(voucher, userRedemptions: 1);

        var selection = await service.SelectAsync(Guid.NewGuid(), null, [Store(600_000m, 30_000m)], null, Rate);

        Assert.Null(selection.Error);   // tự áp không được thì im lặng, không chặn đặt hàng
        Assert.Null(selection.Voucher);
    }

    [Fact]
    public async Task TryRedeem_LastSlotTakenByAnotherCustomer_ReturnsFalse()
    {
        var voucher = FreeShip500();
        var repo = new Mock<IVoucherRepository>();
        repo.Setup(r => r.TryIncrementUsageAsync(voucher.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var service = new VoucherService(UowWith(repo.Object), null!);
        var selection = new VoucherSelection(voucher,
            ShippingVoucherCalculator.Quote(voucher, [Store(600_000m, 30_000m)], null, Now, Rate), null);

        Assert.False(await service.TryRedeemAsync(selection, new Order()));
        repo.Verify(r => r.AddRedemptionAsync(It.IsAny<VoucherRedemption>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static VoucherService ServiceWith(Voucher voucher, int userRedemptions = 0)
    {
        var repo = new Mock<IVoucherRepository>();
        repo.Setup(r => r.GetByCodeAsync("FREESHIP500", It.IsAny<CancellationToken>())).ReturnsAsync(voucher);
        repo.Setup(r => r.GetActiveAutoApplyAsync(It.IsAny<CancellationToken>())).ReturnsAsync([voucher]);
        repo.Setup(r => r.CountActiveRedemptionsAsync(voucher.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userRedemptions);
        return new VoucherService(UowWith(repo.Object), null!);
    }

    private static IUnitOfWork UowWith(IVoucherRepository repo)
    {
        var uow = new Mock<IUnitOfWork>();
        uow.SetupGet(u => u.Vouchers).Returns(repo);
        return uow.Object;
    }
}

public class VndFormatTests
{
    [Theory]
    [InlineData(500_000, "500.000đ")]
    [InlineData(0, "0đ")]
    [InlineData(1_234_567, "1.234.567đ")]
    public void Of_UsesVietnameseGrouping_RegardlessOfServerCulture(decimal amount, string expected)
        => Assert.Equal(expected, FengDeskAI.Application.Common.Formatting.Vnd.Of(amount));

    [Fact]
    public void QuoteRejectReason_ShowsVietnameseAmount()
    {
        var voucher = new Voucher { Code = "X", Name = "X", MinOrderSubtotal = 500_000m, IsActive = true };
        var quote = ShippingVoucherCalculator.Quote(voucher, [new StoreChargeInput(Guid.NewGuid(), 100_000m, 15_000m)],
            null, DateTime.UtcNow, 0.08m);
        Assert.Contains("500.000đ", quote.RejectReason);
    }
}
