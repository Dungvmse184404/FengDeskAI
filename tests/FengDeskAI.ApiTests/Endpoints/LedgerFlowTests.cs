using System.Net;
using System.Net.Http.Json;
using FengDeskAI.ApiTests.Infrastructure;
using FengDeskAI.Application.Features.Vendor.Services;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Sổ cái + phí sàn (docs/adr/platform-fee-ledger.md) đi qua luồng THẬT: đặt → giao → hoàn tiền → công nợ →
/// miễn công nợ. Mỗi kịch bản dựng một cửa hàng mới nên số dư của vườn chỉ gồm đúng các đơn trong kịch bản.
///
/// Bộ dữ liệu của <see cref="DeliveredOrderScenario"/>: một món 150 000đ, COD, phí sàn 8% ⇒ phí 12 000đ,
/// vườn thực nhận 138 000đ.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class LedgerFlowTests
{
    private const decimal Price = 150_000m;
    private static readonly decimal Commission = PlatformFeePolicy.ComputeCommission(Price, PlatformFeePolicy.DefaultCommissionRate);

    private readonly ApiTestFixture _fixture;

    public LedgerFlowTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "LEDGER-01 [Normal] Fee policy is public and matches the backend constant")]
    public async Task FeePolicy_Anonymous_ReturnsCurrentRate()
    {
        var response = await _fixture.Factory.CreateClient().GetAsync("/api/platform/fee-policy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal(PlatformFeePolicy.DefaultCommissionRate, data.GetProperty("commissionRate").GetDecimal());
        Assert.Equal(PayoutPolicy.HoldDays, data.GetProperty("payoutHoldDays").GetInt32());
        Assert.Equal(PlatformFeePolicy.DefaultCommissionRate, data.GetProperty("maxPlatformFundedDiscountRate").GetDecimal());
    }

    [Fact(DisplayName = "LEDGER-02 [Normal] Checkout snapshots the commission rate on each delivery")]
    public async Task Checkout_SnapshotsCommissionRate()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);

        var delivery = await LoadDeliveryAsync(order.DeliveryId);
        Assert.Equal(PlatformFeePolicy.DefaultCommissionRate, delivery.CommissionRate);
    }

    [Fact(DisplayName = "LEDGER-03 [Normal] A delivered order credits the garden net of commission, held for the payout window")]
    public async Task Delivered_CreditsGardenNetOfCommission()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);

        var entries = await EntriesAsync(e => e.DeliveryId == order.DeliveryId);
        var garden = entries.Where(e => e.Account == LedgerAccount.GardenStore).ToList();
        Assert.Equal(Price - Commission, garden.Sum(e => e.Amount));
        Assert.Equal(Commission, entries.Single(e => e.Account == LedgerAccount.Platform && e.Type == LedgerEntryType.Commission).Amount);

        var delivery = await LoadDeliveryAsync(order.DeliveryId);
        var shipping = entries.Single(e => e.Type == LedgerEntryType.ShippingCollected);
        Assert.Equal(delivery.ShippingFee, shipping.Amount);

        // Tiền vừa giao hôm nay còn trong khoảng giữ — chưa "có thể chi".
        var stats = await StatisticsAsync(order.StoreId);
        Assert.Equal(Price - Commission, stats.GetProperty("ledgerBalance").GetDecimal());
        Assert.Equal(Price - Commission, stats.GetProperty("ledgerPending").GetDecimal());
        Assert.Equal(0m, stats.GetProperty("ledgerAvailable").GetDecimal());
        Assert.Equal(Commission, stats.GetProperty("platformCommission").GetDecimal());
        Assert.Equal(PlatformFeePolicy.DefaultCommissionRate, stats.GetProperty("commissionRate").GetDecimal());
    }

    [Fact(DisplayName = "LEDGER-04 [Normal] A fully refunded item leaves the garden with zero and returns the commission")]
    public async Task FullRefund_GardenNetsZero_CommissionReturned()
    {
        var refunded = await RefundedReturnScenario.CreateAsync(_fixture);
        Assert.Equal(Price, refunded.Amount);

        var stats = await StatisticsAsync(refunded.StoreId);
        // 150 000 tiền hàng − 12 000 phí sàn − 150 000 công nợ + 12 000 trả lại phí sàn = 0.
        Assert.Equal(0m, stats.GetProperty("ledgerBalance").GetDecimal());
        Assert.Equal(0m, stats.GetProperty("platformCommission").GetDecimal());
        // Hoàn trong khoảng giữ: khoản trừ triệt tiêu đúng khoản tiền hàng còn chờ — "có thể chi" không bị âm.
        Assert.Equal(0m, stats.GetProperty("ledgerAvailable").GetDecimal());
        Assert.Equal(0m, stats.GetProperty("ledgerPending").GetDecimal());

        // Sàn: tiền ra đúng bằng tiền hoàn cho khách, thu lại đủ từ vườn.
        var platform = await EntriesAsync(e => e.Account == LedgerAccount.Platform
                                               && (e.RefundId == refunded.RefundId || e.VendorLiabilityId == refunded.LiabilityId));
        Assert.Equal(-Price, platform.Single(e => e.Type == LedgerEntryType.RefundPaidOut).Amount);
        Assert.Equal(Price, platform.Single(e => e.Type == LedgerEntryType.RefundLiability).Amount);
    }

    [Fact(DisplayName = "LEDGER-05 [Normal] Waiving a liability restores the garden to its pre-refund balance")]
    public async Task LiabilityWaived_RestoresGardenBalance()
    {
        var refunded = await RefundedReturnScenario.CreateAsync(_fixture);
        await _fixture.ClientFor(TestRole.GardenOwner).PostAsJsonAsync(
            $"/api/vendor-liabilities/{refunded.LiabilityId}/dispute", new { reason = "Hư hại do vận chuyển." });

        var resolve = await _fixture.ClientFor(TestRole.Manager).PostAsJsonAsync(
            $"/api/vendor-liabilities/{refunded.LiabilityId}/resolve", new { vendorWins = true });
        Assert.True(resolve.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(resolve, "miễn công nợ"));

        var stats = await StatisticsAsync(refunded.StoreId);
        Assert.Equal(Price - Commission, stats.GetProperty("ledgerBalance").GetDecimal());
        Assert.Equal(Commission, stats.GetProperty("platformCommission").GetDecimal());
    }

    [Fact(DisplayName = "LEDGER-06 [Boundary] Every business event nets to money that actually left or entered the platform")]
    public async Task RefundLifecycle_ConservesMoney()
    {
        var refunded = await RefundedReturnScenario.CreateAsync(_fixture);
        var delivery = await LoadDeliveryAsync(await DeliveryIdOfTicketAsync(refunded.TicketId));

        var all = await EntriesAsync(e => e.DeliveryId == delivery.Id
                                          || e.RefundId == refunded.RefundId
                                          || e.VendorLiabilityId == refunded.LiabilityId);

        // Tiền vào hệ thống: khách trả tiền hàng + phí ship. Tiền ra: nhà vận chuyển + tiền hoàn cho khách.
        var customerPaid = delivery.Subtotal + delivery.ShippingFee;
        var expected = customerPaid - (delivery.CarrierShippingFee ?? 0m) - refunded.Amount;
        Assert.Equal(expected, all.Sum(e => e.Amount));
    }

    // ===================== Hạ tầng =====================

    [Fact(DisplayName = "LEDGER-07 [Normal] Owner balance lists a just-delivered sale as held, not yet withdrawable")]
    public async Task MyBalance_JustDelivered_IsPendingNotAvailable()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);

        var response = await _fixture.ClientFor(TestRole.GardenOwner).GetAsync("/api/stores/mine/balance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        var store = data.GetProperty("stores").EnumerateArray()
            .Single(s => s.GetProperty("storeId").GetGuid() == order.StoreId);
        Assert.Equal(Price - Commission, store.GetProperty("pending").GetDecimal());
        Assert.Equal(0m, store.GetProperty("available").GetDecimal());
        Assert.Equal(PayoutPolicy.HoldDays, data.GetProperty("payoutHoldDays").GetInt32());
        Assert.Equal(data.GetProperty("stores").EnumerateArray().Sum(s => s.GetProperty("balance").GetDecimal()),
            data.GetProperty("balance").GetDecimal());
    }

    [Fact(DisplayName = "LEDGER-08 [Security] A user owning no store sees an empty balance")]
    public async Task MyBalance_NonOwner_IsEmpty()
    {
        var client = ScenarioUsers.ClientFor(_fixture, await ScenarioUsers.CreateAsync(_fixture));

        var data = await ApiEnvelope.DataAsync(await client.GetAsync("/api/stores/mine/balance"));

        Assert.Empty(data.GetProperty("stores").EnumerateArray());
        Assert.Equal(0m, data.GetProperty("balance").GetDecimal());
    }

    private async Task<Delivery> LoadDeliveryAsync(Guid deliveryId)
    {
        Delivery? delivery = null;
        await _fixture.WithScopeAsync(async sp =>
            delivery = await sp.GetRequiredService<AppDbContext>().Deliveries.AsNoTracking().SingleAsync(d => d.Id == deliveryId));
        return delivery!;
    }

    private async Task<Guid> DeliveryIdOfTicketAsync(Guid ticketId)
    {
        var id = Guid.Empty;
        await _fixture.WithScopeAsync(async sp =>
            id = await sp.GetRequiredService<AppDbContext>().Set<ReturnRequest>()
                .Where(t => t.Id == ticketId).Select(t => t.DeliveryId).SingleAsync());
        return id;
    }

    private async Task<List<LedgerEntry>> EntriesAsync(System.Linq.Expressions.Expression<Func<LedgerEntry, bool>> predicate)
    {
        var rows = new List<LedgerEntry>();
        await _fixture.WithScopeAsync(async sp =>
            rows = await sp.GetRequiredService<AppDbContext>().LedgerEntries.AsNoTracking().Where(predicate).ToListAsync());
        return rows;
    }

    private async Task<System.Text.Json.JsonElement> StatisticsAsync(Guid storeId)
    {
        var response = await _fixture.ClientFor(TestRole.GardenOwner).GetAsync($"/api/stores/{storeId}/statistics");
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "đọc thống kê"));
        return await ApiEnvelope.DataAsync(response);
    }
}
