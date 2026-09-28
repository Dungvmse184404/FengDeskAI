using FengDeskAI.Application.Features.Payment.Services;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.Application.Features.Vendor.Services;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Payment;
using Moq;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// Chính sách phí sàn + quy tắc ghi sổ cái (docs/adr/platform-fee-ledger.md). Bất biến quan trọng nhất:
/// mỗi sự kiện chỉ chuyển tiền GIỮA hai sổ hoặc ra/vào từ bên ngoài (khách, nhà vận chuyển) — không có
/// bút toán nào tự sinh ra hay làm mất tiền.
/// </summary>
public class PlatformFeeLedgerTests
{
    // ===================== PlatformFeePolicy =====================

    [Theory]
    [InlineData(150_000, 12_000)]
    [InlineData(99_999, 8_000)]   // 7 999.92 → 8 000
    [InlineData(6_250, 500)]      // 500.00 đúng
    [InlineData(6_256, 500)]      // 500.48 → 500
    [InlineData(6_257, 501)]      // 500.56 → 501
    [InlineData(0, 0)]
    public void ComputeCommission_EightPercent_RoundsToWholeDong(decimal subtotal, decimal expected)
        => Assert.Equal(expected, PlatformFeePolicy.ComputeCommission(subtotal, PlatformFeePolicy.CommissionRate));

    [Fact]
    public void ComputeCommission_HalfDong_RoundsUp()
        // 25 × 0.1 = 2.5 → 3 (không phải 2 như banker's rounding mặc định của .NET) — FE dùng đúng quy tắc này.
        => Assert.Equal(3m, PlatformFeePolicy.ComputeCommission(25m, 0.1m));

    [Fact]
    public void VendorNet_IsSubtotalMinusCommission()
        => Assert.Equal(138_000m, PlatformFeePolicy.VendorNet(150_000m, 0.08m));

    [Fact]
    public void PlatformFundedDiscountCap_NeverExceedsCommission()
        => Assert.True(PlatformFeePolicy.MaxPlatformFundedDiscountRate <= PlatformFeePolicy.CommissionRate);

    // ===================== Chia phí ship cho đơn PayOS =====================

    [Fact]
    public void AllocateOrderShippingFee_MultiStore_SumsExactlyToOrderFee()
    {
        var order = new Order { TotalShippingFee = 50_000m };
        order.Deliveries.Add(new Delivery { Subtotal = 100_000m });
        order.Deliveries.Add(new Delivery { Subtotal = 200_000m });
        order.Deliveries.Add(new Delivery { Subtotal = 33_333m });

        OrderWorkflow.AllocateOrderShippingFee(order);

        Assert.Equal(50_000m, order.Deliveries.Sum(d => d.ShippingFee));
        Assert.All(order.Deliveries, d => Assert.True(d.ShippingFee > 0));
    }

    [Fact]
    public void AllocateOrderShippingFee_SingleStore_GetsWholeFee()
    {
        var order = new Order { TotalShippingFee = 30_000m };
        order.Deliveries.Add(new Delivery { Subtotal = 90_000m });

        OrderWorkflow.AllocateOrderShippingFee(order);

        Assert.Equal(30_000m, order.Deliveries.Single().ShippingFee);
    }

    [Fact]
    public void GroupItemsIntoDeliveries_SnapshotsCurrentCommissionRate()
    {
        var order = new Order();
        var storeId = Guid.NewGuid();
        order.Items.Add(new OrderItem
        {
            UnitPrice = 100_000m,
            Quantity = 1,
            ProductItem = new Domain.Entities.Catalog.ProductItem
            {
                Product = new Domain.Entities.Catalog.Product { GardenStoreId = storeId },
            },
        });

        OrderWorkflow.GroupItemsIntoDeliveries(order);

        Assert.Equal(PlatformFeePolicy.CommissionRate, order.Deliveries.Single().CommissionRate);
    }

    // ===================== LedgerService =====================

    [Fact]
    public async Task PostDeliveryCompleted_WritesSaleCommissionAndShipping_Balanced()
    {
        var (service, written, _) = CreateService();
        var delivery = new Delivery
        {
            OrderId = Guid.NewGuid(),
            GardenStoreId = Guid.NewGuid(),
            Subtotal = 150_000m,
            ShippingFee = 15_000m,
            CarrierShippingFee = 17_500m,
            CommissionRate = 0.08m,
            DeliveredAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        await service.PostDeliveryCompletedAsync(delivery);

        Assert.Equal(150_000m - 12_000m, GardenSum(written));
        Assert.Equal(12_000m + 15_000m - 17_500m, PlatformSum(written));
        // Hai sổ cộng lại = tiền khách trả − tiền nhà vận chuyển lấy: không có đồng nào tự sinh.
        Assert.Equal(150_000m + 15_000m - 17_500m, written.Sum(e => e.Amount));
        // Tiền của vườn chỉ khả dụng sau khoảng giữ.
        Assert.All(written.Where(e => e.Account == LedgerAccount.GardenStore),
            e => Assert.Equal(delivery.DeliveredAt!.Value.AddDays(PayoutPolicy.HoldDays), e.AvailableAt));
    }

    [Fact]
    public async Task PostDeliveryCompleted_WithPlatformVoucher_PlatformBearsDiscount_GardenUnchanged()
    {
        var (service, written, _) = CreateService();
        var delivery = new Delivery
        {
            GardenStoreId = Guid.NewGuid(),
            Subtotal = 600_000m,
            ShippingFee = 30_000m,
            ShippingDiscount = 30_000m,
            CommissionRate = 0.08m,
            DeliveredAt = DateTime.UtcNow,
        };

        await service.PostDeliveryCompletedAsync(delivery);

        // Vườn nhận đủ tiền hàng − phí sàn, voucher không đụng tới.
        Assert.Equal(600_000m - 48_000m, GardenSum(written));
        Assert.Equal(-30_000m, written.Single(e => e.Type == LedgerEntryType.ShippingVoucherSubsidy).Amount);
        // Tổng hai sổ = đúng số khách trả (tiền hàng + phí ship − giảm).
        Assert.Equal(600_000m + 30_000m - 30_000m, written.Sum(e => e.Amount));
    }

    [Fact]
    public async Task PostDeliveryCompleted_ExchangeDelivery_WritesNothing()
    {
        var (service, written, _) = CreateService();

        await service.PostDeliveryCompletedAsync(new Delivery { IsExchange = true, Subtotal = 0m });

        Assert.Empty(written);
    }

    [Fact]
    public async Task PostDeliveryCompleted_AlreadyPosted_IsIdempotent()
    {
        var delivery = new Delivery { GardenStoreId = Guid.NewGuid(), Subtotal = 100_000m, CommissionRate = 0.08m };
        var allKeys = new HashSet<string>
        {
            LedgerService.Key(LedgerAccount.GardenStore, LedgerEntryType.SaleCredit, "delivery", delivery.Id),
            LedgerService.Key(LedgerAccount.GardenStore, LedgerEntryType.Commission, "delivery", delivery.Id),
            LedgerService.Key(LedgerAccount.Platform, LedgerEntryType.Commission, "delivery", delivery.Id),
        };
        var (service, written, _) = CreateService(existingKeys: allKeys);

        await service.PostDeliveryCompletedAsync(delivery);

        Assert.Empty(written);
    }

    [Fact]
    public async Task RefundThenLiability_VendorBearsGoodsMinusReturnedCommission()
    {
        var (service, written, _) = CreateService();
        var garden = Guid.NewGuid();
        var refund = new Refund { OrderId = Guid.NewGuid(), Amount = 80_000m, IdempotencyKey = "k" };
        var liability = new VendorLiability { GardenStoreId = garden, RefundId = refund.Id, Amount = 80_000m };

        await service.PostRefundPaidOutAsync(refund, garden);
        await service.PostLiabilityRaisedAsync(liability, DeliveredDaysAgo(2));

        // Vườn chịu 80 000 nhưng được trả lại 6 400 phí sàn đã thu trên món đó.
        Assert.Equal(-80_000m + 6_400m, GardenSum(written));
        // Sàn: trả khách 80 000, thu lại 80 000 từ vườn, trả lại 6 400 phí sàn.
        Assert.Equal(-80_000m + 80_000m - 6_400m, PlatformSum(written));
        // Tổng hai sổ = đúng số tiền đã ra khỏi hệ thống về tay khách.
        Assert.Equal(-80_000m, written.Sum(e => e.Amount));
    }

    [Fact]
    public async Task LiabilityWaived_ReversesExactlyWhatWasRaised()
    {
        var (service, written, ledger) = CreateService();
        var liability = new VendorLiability { GardenStoreId = Guid.NewGuid(), Amount = 80_000m };
        await service.PostLiabilityRaisedAsync(liability, DeliveredDaysAgo(2));
        var reversal = written.Single(e => e.Account == LedgerAccount.GardenStore && e.Type == LedgerEntryType.CommissionReversal);
        ledger.Setup(l => l.GetByKeyAsync(reversal.IdempotencyKey, It.IsAny<CancellationToken>())).ReturnsAsync(reversal);

        await service.PostLiabilityWaivedAsync(liability);

        Assert.Equal(0m, GardenSum(written));
        Assert.Equal(0m, PlatformSum(written));
    }

    [Fact]
    public async Task LiabilityRaised_DuringHold_TakesEffectWhenSaleClears_SoAvailableNeverGoesNegative()
    {
        var (service, written, _) = CreateService();
        var delivery = DeliveredDaysAgo(2);
        var liability = new VendorLiability { GardenStoreId = Guid.NewGuid(), Amount = 80_000m };

        await service.PostLiabilityRaisedAsync(liability, delivery);

        var clearsAt = delivery.DeliveredAt!.Value.AddDays(PayoutPolicy.HoldDays);
        Assert.All(written.Where(e => e.Account == LedgerAccount.GardenStore), e => Assert.Equal(clearsAt, e.AvailableAt));
    }

    [Fact]
    public async Task LiabilityRaised_AfterHold_TakesEffectImmediately()
    {
        var (service, written, _) = CreateService();
        var before = DateTime.UtcNow;

        await service.PostLiabilityRaisedAsync(
            new VendorLiability { GardenStoreId = Guid.NewGuid(), Amount = 80_000m },
            DeliveredDaysAgo(PayoutPolicy.HoldDays + 3));

        Assert.All(written.Where(e => e.Account == LedgerAccount.GardenStore), e => Assert.True(e.AvailableAt >= before));
        Assert.All(written.Where(e => e.Account == LedgerAccount.GardenStore), e => Assert.True(e.AvailableAt <= DateTime.UtcNow));
    }

    // ===================== Hạ tầng =====================

    private static Delivery DeliveredDaysAgo(int days) => new()
    {
        CommissionRate = 0.08m,
        DeliveredAt = DateTime.UtcNow.AddDays(-days),
    };

    private static decimal GardenSum(IEnumerable<LedgerEntry> entries)
        => entries.Where(e => e.Account == LedgerAccount.GardenStore).Sum(e => e.Amount);

    private static decimal PlatformSum(IEnumerable<LedgerEntry> entries)
        => entries.Where(e => e.Account == LedgerAccount.Platform).Sum(e => e.Amount);

    private static (LedgerService Service, List<LedgerEntry> Written, Mock<ILedgerRepository> Ledger) CreateService(
        HashSet<string>? existingKeys = null)
    {
        var written = new List<LedgerEntry>();
        var ledger = new Mock<ILedgerRepository>();
        ledger.Setup(l => l.GetExistingKeysAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string> keys, CancellationToken _) =>
                keys.Where(k => (existingKeys?.Contains(k) ?? false) || written.Any(w => w.IdempotencyKey == k)).ToHashSet());
        ledger.Setup(l => l.AddRangeAsync(It.IsAny<IEnumerable<LedgerEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<LedgerEntry> entries, CancellationToken _) => written.AddRange(entries))
            .Returns(Task.CompletedTask);

        var uow = new Mock<IUnitOfWork>();
        uow.SetupGet(u => u.Ledger).Returns(ledger.Object);
        return (new LedgerService(uow.Object), written, ledger);
    }
}
