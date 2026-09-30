using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using FengDeskAI.Application.Features.Vendor.Services;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Promotion;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.Enums.Promotion;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using FengDeskAI.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Voucher miễn phí vận chuyển FREESHIP500 (docs/adr/voucher-freeship.md) qua luồng thật: xem trước → đặt →
/// hủy/giao. Mỗi ca dùng khách dùng-một-lần (<see cref="ScenarioUsers"/>) để giới hạn lượt theo người không
/// dính nhau. Sản phẩm của <see cref="SalesScenario"/> giá 150 000đ ⇒ 4 cái = 600 000đ (qua ngưỡng), 3 cái = 450 000đ.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class VoucherFlowTests
{
    private readonly ApiTestFixture _fixture;

    public VoucherFlowTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "VOUCHER-01 [Normal] Preview auto-applies FREESHIP500 at 500k and the total already reflects it")]
    public async Task Preview_OverThreshold_AutoAppliesFreeShip()
    {
        var (client, data) = await CustomerWithStoresAsync();

        var preview = await PreviewAsync(client, data.ShippingAddressId, (data.StoreA.ProductItemId, 4));

        var fee = preview.GetProperty("totalShippingFee").GetDecimal();
        Assert.True(fee > 0);
        Assert.Equal(fee, preview.GetProperty("shippingDiscount").GetDecimal());
        Assert.Equal(600_000m, preview.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(VoucherSeeder.FreeShip500Code, preview.GetProperty("appliedVoucher").GetProperty("code").GetString());
    }

    [Fact(DisplayName = "VOUCHER-02 [Boundary] Below 500k no voucher is applied")]
    public async Task Preview_BelowThreshold_NoDiscount()
    {
        var (client, data) = await CustomerWithStoresAsync();

        var preview = await PreviewAsync(client, data.ShippingAddressId, (data.StoreA.ProductItemId, 3));

        Assert.Equal(0m, preview.GetProperty("shippingDiscount").GetDecimal());
        Assert.Equal(JsonValueKind.Null, preview.GetProperty("appliedVoucher").ValueKind);
    }

    [Fact(DisplayName = "VOUCHER-03 [Boundary] A small store's discount is capped at its own 8% commission")]
    public async Task Preview_MultiStore_SmallStoreCappedByCommission()
    {
        var (client, data) = await CustomerWithStoresAsync();

        var preview = await PreviewAsync(client, data.ShippingAddressId,
            (data.StoreA.ProductItemId, 4), (data.StoreB.ProductItemId, 1));

        var storeB = preview.GetProperty("stores").EnumerateArray()
            .Single(s => s.GetProperty("storeId").GetGuid() == data.StoreB.StoreId);
        var commissionB = PlatformFeePolicy.ComputeCommission(150_000m, PlatformFeePolicy.DefaultCommissionRate);
        Assert.Equal(Math.Min(storeB.GetProperty("shippingFee").GetDecimal(), commissionB),
            storeB.GetProperty("shippingDiscount").GetDecimal());
    }

    [Fact(DisplayName = "VOUCHER-04 [Normal] COD checkout charges exactly the previewed total and holds one usage")]
    public async Task Checkout_Cod_MatchesPreview_AndRedeems()
    {
        var (client, data) = await CustomerWithStoresAsync();
        var usedBefore = await UsedCountAsync(VoucherSeeder.FreeShip500Code);
        var preview = await PreviewAsync(client, data.ShippingAddressId, (data.StoreA.ProductItemId, 4));

        var order = await CheckoutAsync(client, data.ShippingAddressId, "COD", null, (data.StoreA.ProductItemId, 4));

        Assert.Equal(preview.GetProperty("totalAmount").GetDecimal(), order.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(preview.GetProperty("shippingDiscount").GetDecimal(), order.GetProperty("shippingDiscount").GetDecimal());
        Assert.Equal(VoucherSeeder.FreeShip500Code, order.GetProperty("voucherCode").GetString());
        Assert.Equal(order.GetProperty("shippingDiscount").GetDecimal(),
            order.GetProperty("deliveries")[0].GetProperty("shippingDiscount").GetDecimal());

        Assert.Equal(usedBefore + 1, await UsedCountAsync(VoucherSeeder.FreeShip500Code));
        Assert.Equal(VoucherRedemptionStatus.Applied, (await RedemptionAsync(order.GetProperty("id").GetGuid()))!.Status);
    }

    [Fact(DisplayName = "VOUCHER-05 [Abnormal] An unknown code blocks checkout instead of silently charging a different total")]
    public async Task Checkout_UnknownCode_Rejected_NoOrder()
    {
        var (client, data) = await CustomerWithStoresAsync();

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = data.ShippingAddressId,
            paymentMethod = "COD",
            voucherCode = "KHONG-TON-TAI",
            items = new[] { new { productItemId = data.StoreA.ProductItemId, quantity = 4 } },
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(data.StoreA.Stock, await StockAsync(data.StoreA.ProductItemId));
    }

    [Fact(DisplayName = "VOUCHER-06 [Normal] Cancelling the order gives the usage back")]
    public async Task Cancel_ReleasesUsage()
    {
        var (client, data) = await CustomerWithStoresAsync();
        var order = await CheckoutAsync(client, data.ShippingAddressId, "COD", null, (data.StoreA.ProductItemId, 4));
        var orderId = order.GetProperty("id").GetGuid();
        var usedAfterCheckout = await UsedCountAsync(VoucherSeeder.FreeShip500Code);

        var cancel = await client.PostAsync($"/api/orders/{orderId}/cancel", null);
        Assert.True(cancel.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(cancel, "hủy đơn"));

        Assert.Equal(usedAfterCheckout - 1, await UsedCountAsync(VoucherSeeder.FreeShip500Code));
        Assert.Equal(VoucherRedemptionStatus.Released, (await RedemptionAsync(orderId))!.Status);
    }

    [Fact(DisplayName = "VOUCHER-07 [Boundary] A voucher with one usage left serves exactly one customer")]
    public async Task UsageLimit_SecondCustomer_Rejected()
    {
        var code = await CreateVoucherAsync(new { usageLimit = 1, minOrderSubtotal = 0 });

        var (first, firstData) = await CustomerWithStoresAsync();
        var ok = await CheckoutAsync(first, firstData.ShippingAddressId, "COD", code, (firstData.StoreA.ProductItemId, 1));
        Assert.Equal(code, ok.GetProperty("voucherCode").GetString());

        var (second, secondData) = await CustomerWithStoresAsync();
        var rejected = await second.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = secondData.ShippingAddressId,
            paymentMethod = "COD",
            voucherCode = code,
            items = new[] { new { productItemId = secondData.StoreA.ProductItemId, quantity = 1 } },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        Assert.Equal(1, await UsedCountAsync(code));
    }

    [Fact(DisplayName = "VOUCHER-08 [Normal] On delivery the platform books the subsidy and the garden is untouched")]
    public async Task Delivered_PlatformBearsSubsidy_MoneyConserved()
    {
        var (client, data) = await CustomerWithStoresAsync();
        var order = await CheckoutAsync(client, data.ShippingAddressId, "COD", null, (data.StoreA.ProductItemId, 4));
        var deliveryId = order.GetProperty("deliveries")[0].GetProperty("id").GetGuid();
        var discount = order.GetProperty("shippingDiscount").GetDecimal();

        var owner = _fixture.ClientFor(TestRole.GardenOwner);
        foreach (var status in new[] { "Confirmed", "Preparing", "Shipped", "Delivered" })
        {
            var r = await owner.PatchAsJsonAsync($"/api/orders/deliveries/{deliveryId}/status", new { status });
            Assert.True(r.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(r, $"delivery → {status}"));
        }

        var entries = await LedgerAsync(deliveryId);
        Assert.Equal(-discount, entries.Single(e => e.Type == LedgerEntryType.ShippingVoucherSubsidy).Amount);
        var commission = PlatformFeePolicy.ComputeCommission(600_000m, PlatformFeePolicy.DefaultCommissionRate);
        Assert.Equal(600_000m - commission, entries.Where(e => e.Account == LedgerAccount.GardenStore).Sum(e => e.Amount));
        // Hai sổ cộng lại = đúng số khách trả (Mock không báo phí nhà vận chuyển).
        Assert.Equal(order.GetProperty("totalAmount").GetDecimal(), entries.Sum(e => e.Amount));
    }

    [Fact(DisplayName = "VOUCHER-10 [Normal] A paid PayOS order gets each store's fee and discount exactly as locked at checkout")]
    public async Task PayOs_Paid_DeliveriesUseLockedStoreCharges()
    {
        var (client, data) = await CustomerWithStoresAsync();
        var preview = await PreviewAsync(client, data.ShippingAddressId,
            (data.StoreA.ProductItemId, 4), (data.StoreB.ProductItemId, 1));
        var order = await CheckoutAsync(client, data.ShippingAddressId, "PayOS", null,
            (data.StoreA.ProductItemId, 4), (data.StoreB.ProductItemId, 1));
        var orderId = order.GetProperty("id").GetGuid();

        // Webhook PayOS không giả được trong môi trường Testing (chữ ký) ⇒ gọi đúng hàm dùng chung của webhook.
        await _fixture.WithScopeAsync(async sp =>
        {
            var result = await sp.GetRequiredService<Application.Features.Payment.Services.IPaymentService>()
                .SimulatePaidAsync(orderId);
            Assert.True(result.IsSuccess, result.Message);
        });

        var paid = await ApiEnvelope.DataAsync(await client.GetAsync($"/api/orders/{orderId}"));
        foreach (var store in preview.GetProperty("stores").EnumerateArray())
        {
            var delivery = paid.GetProperty("deliveries").EnumerateArray()
                .Single(d => d.GetProperty("gardenStoreId").GetGuid() == store.GetProperty("storeId").GetGuid());
            Assert.Equal(store.GetProperty("shippingFee").GetDecimal(), delivery.GetProperty("shippingFee").GetDecimal());
            Assert.Equal(store.GetProperty("shippingDiscount").GetDecimal(), delivery.GetProperty("shippingDiscount").GetDecimal());
        }
        Assert.Equal(paid.GetProperty("totalShippingFee").GetDecimal(),
            paid.GetProperty("deliveries").EnumerateArray().Sum(d => d.GetProperty("shippingFee").GetDecimal()));
        Assert.Equal(paid.GetProperty("shippingDiscount").GetDecimal(),
            paid.GetProperty("deliveries").EnumerateArray().Sum(d => d.GetProperty("shippingDiscount").GetDecimal()));
    }

    [Fact(DisplayName = "VOUCHER-09 [Normal] Available vouchers are public; creating one validates input")]
    public async Task Available_Public_Create_Validates()
    {
        var available = await _fixture.Factory.CreateClient().GetAsync("/api/vouchers/available");
        Assert.Equal(HttpStatusCode.OK, available.StatusCode);
        Assert.Contains((await ApiEnvelope.DataAsync(available)).EnumerateArray(),
            v => v.GetProperty("code").GetString() == VoucherSeeder.FreeShip500Code);

        var manager = _fixture.ClientFor(TestRole.Manager);
        var bad = await manager.PostAsJsonAsync("/api/vouchers", new { code = "x", name = "" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var duplicate = await manager.PostAsJsonAsync("/api/vouchers", new { code = VoucherSeeder.FreeShip500Code, name = "Trùng" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    // ===================== Hạ tầng =====================

    private async Task<(HttpClient Client, SalesScenarioData Data)> CustomerWithStoresAsync()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var data = await SalesScenario.SeedAsync(_fixture, user.Id);
        return (ScenarioUsers.ClientFor(_fixture, user), data);
    }

    private static async Task<JsonElement> PreviewAsync(HttpClient client, Guid addressId, params (Guid ProductItemId, int Quantity)[] items)
    {
        var response = await client.PostAsJsonAsync("/api/orders/shipping-fee-preview", new
        {
            shippingAddressId = addressId,
            items = items.Select(i => new { productItemId = i.ProductItemId, quantity = i.Quantity }),
        });
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "xem trước phí"));
        return await ApiEnvelope.DataAsync(response);
    }

    private static async Task<JsonElement> CheckoutAsync(HttpClient client, Guid addressId, string paymentMethod,
        string? voucherCode, params (Guid ProductItemId, int Quantity)[] items)
    {
        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = addressId,
            paymentMethod,
            voucherCode,
            items = items.Select(i => new { productItemId = i.ProductItemId, quantity = i.Quantity }),
        });
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "đặt hàng"));
        return await ApiEnvelope.DataAsync(response);
    }

    private async Task<string> CreateVoucherAsync(object overrides)
    {
        var code = $"E2E{Guid.NewGuid():N}"[..14].ToUpperInvariant();
        var body = JsonSerializer.SerializeToNode(overrides)!.AsObject();
        body["code"] = code;
        body["name"] = "Voucher test";
        var response = await _fixture.ClientFor(TestRole.Manager).PostAsJsonAsync("/api/vouchers", body);
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "tạo voucher"));
        return code;
    }

    private Task<int> UsedCountAsync(string code) => QueryAsync(db =>
        db.Vouchers.AsNoTracking().Where(v => v.Code == code).Select(v => v.UsedCount).SingleAsync());

    private Task<VoucherRedemption?> RedemptionAsync(Guid orderId) => QueryAsync(db =>
        db.VoucherRedemptions.AsNoTracking().FirstOrDefaultAsync(r => r.OrderId == orderId));

    private Task<int> StockAsync(Guid productItemId) => QueryAsync(db =>
        db.Set<Domain.Entities.Catalog.ProductItem>().AsNoTracking()
            .Where(p => p.Id == productItemId).Select(p => p.Stock).SingleAsync());

    private Task<List<LedgerEntry>> LedgerAsync(Guid deliveryId) => QueryAsync(db =>
        db.LedgerEntries.AsNoTracking().Where(e => e.DeliveryId == deliveryId).ToListAsync());

    private async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        T result = default!;
        await _fixture.WithScopeAsync(async sp => result = await query(sp.GetRequiredService<AppDbContext>()));
        return result;
    }
}
