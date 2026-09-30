using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using FengDeskAI.Application.Features.Vendor.Services;
using FengDeskAI.Domain.Enums;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Phí sàn cấu hình được (docs/adr/platform-fee-ledger.md §Cấu hình): Manager đổi tỉ lệ → đơn MỚI chốt tỉ lệ mới,
/// trần voucher sàn tài trợ đi theo, đơn cũ giữ nguyên. Ca nào đổi tỉ lệ đều trả về mặc định trong <c>finally</c>
/// — cả bộ chạy tuần tự trong một collection nên các ca khác không thấy tỉ lệ tạm.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class PlatformFeeFlowTests
{
    private const decimal TemporaryRate = 0.02m;

    private readonly ApiTestFixture _fixture;

    public PlatformFeeFlowTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "PLATFORMFEE-01 [Security] Customers cannot change the platform fee or read its history")]
    public async Task UpdateFee_Customer_Forbidden()
    {
        var client = ScenarioUsers.ClientFor(_fixture, await ScenarioUsers.CreateAsync(_fixture));

        var put = await client.PutAsJsonAsync("/api/platform/fee-policy", new { commissionRate = 0.05m });
        var history = await client.GetAsync("/api/platform/fee-policy/history");

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, history.StatusCode);
    }

    [Theory(DisplayName = "PLATFORMFEE-02 [Boundary] Out-of-range or over-precise rates are rejected")]
    [InlineData(-0.01)]
    [InlineData(0.31)]
    [InlineData(0.08125)]
    public async Task UpdateFee_InvalidRate_BadRequest(decimal rate)
    {
        var manager = await ManagerClientAsync();

        var response = await manager.PutAsJsonAsync("/api/platform/fee-policy", new { commissionRate = rate });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "PLATFORMFEE-02b [Boundary] A body without the rate is rejected instead of meaning 0%")]
    public async Task UpdateFee_MissingRate_BadRequest()
    {
        var manager = await ManagerClientAsync();

        var response = await manager.PutAsJsonAsync("/api/platform/fee-policy", new { note = "thiếu tỉ lệ" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var policy = await ApiEnvelope.DataAsync(await _fixture.Factory.CreateClient().GetAsync("/api/platform/fee-policy"));
        Assert.Equal(PlatformFeePolicy.DefaultCommissionRate, policy.GetProperty("commissionRate").GetDecimal());
    }

    [Fact(DisplayName = "PLATFORMFEE-03 [Normal] New rate applies to new orders and voucher caps; old orders keep theirs")]
    public async Task UpdateFee_AppliesToNewOrdersOnly()
    {
        var manager = await ManagerClientAsync();
        var customer = await ScenarioUsers.CreateAsync(_fixture);
        var client = ScenarioUsers.ClientFor(_fixture, customer);
        var data = await SalesScenario.SeedAsync(_fixture, customer.Id);

        var before = await CheckoutAsync(client, data.ShippingAddressId, data.StoreA.ProductItemId, 1);
        try
        {
            var put = await manager.PutAsJsonAsync("/api/platform/fee-policy",
                new { commissionRate = TemporaryRate, note = "API test" });
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);

            var policy = await ApiEnvelope.DataAsync(await _fixture.Factory.CreateClient().GetAsync("/api/platform/fee-policy"));
            Assert.Equal(TemporaryRate, policy.GetProperty("commissionRate").GetDecimal());
            Assert.Equal(TemporaryRate, policy.GetProperty("maxPlatformFundedDiscountRate").GetDecimal());

            var history = await ApiEnvelope.DataAsync(await manager.GetAsync("/api/platform/fee-policy/history"));
            Assert.Equal(TemporaryRate, history[0].GetProperty("commissionRate").GetDecimal());
            Assert.Equal("API test", history[0].GetProperty("note").GetString());

            // 4 × 150 000đ = 600 000đ ⇒ FREESHIP500 tự áp, trần giảm = 2% × 600 000đ = 12 000đ.
            var after = await CheckoutAsync(client, data.ShippingAddressId, data.StoreA.ProductItemId, 4);
            var fee = after.GetProperty("totalShippingFee").GetDecimal();
            Assert.Equal(Math.Min(fee, PlatformFeePolicy.ComputeCommission(600_000m, TemporaryRate)),
                after.GetProperty("shippingDiscount").GetDecimal());

            Assert.Equal(TemporaryRate, await OrderRateAsync(after.GetProperty("id").GetGuid()));
            Assert.Equal(TemporaryRate, await DeliveryRateAsync(after.GetProperty("id").GetGuid()));
            Assert.Equal(PlatformFeePolicy.DefaultCommissionRate, await DeliveryRateAsync(before.GetProperty("id").GetGuid()));

            var same = await manager.PutAsJsonAsync("/api/platform/fee-policy", new { commissionRate = TemporaryRate });
            Assert.Equal(HttpStatusCode.Conflict, same.StatusCode);
        }
        finally
        {
            await manager.PutAsJsonAsync("/api/platform/fee-policy",
                new { commissionRate = PlatformFeePolicy.DefaultCommissionRate, note = "API test: trả về mặc định" });
        }
    }

    private async Task<HttpClient> ManagerClientAsync()
        => ScenarioUsers.ClientFor(_fixture, await ScenarioUsers.CreateAsync(_fixture, UserRole.Manager));

    private static async Task<JsonElement> CheckoutAsync(HttpClient client, Guid addressId, Guid productItemId, int quantity)
    {
        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = addressId,
            paymentMethod = "COD",
            items = new[] { new { productItemId, quantity } },
        });
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "đặt hàng"));
        return await ApiEnvelope.DataAsync(response);
    }

    private async Task<decimal> OrderRateAsync(Guid orderId)
    {
        decimal rate = 0;
        await _fixture.WithScopeAsync(async sp => rate = await sp.GetRequiredService<AppDbContext>().Orders
            .Where(o => o.Id == orderId).Select(o => o.CommissionRate).SingleAsync());
        return rate;
    }

    private async Task<decimal> DeliveryRateAsync(Guid orderId)
    {
        decimal rate = 0;
        await _fixture.WithScopeAsync(async sp => rate = await sp.GetRequiredService<AppDbContext>().Deliveries
            .Where(d => d.OrderId == orderId).Select(d => d.CommissionRate).SingleAsync());
        return rate;
    }
}
