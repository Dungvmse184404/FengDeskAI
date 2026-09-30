using System.Net;
using System.Net.Http.Json;
using FengDeskAI.ApiTests.Infrastructure;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Khách xác nhận đã nhận hàng (<c>POST /api/orders/{id}/confirm-received</c>) — thay cho việc FE gọi endpoint dev
/// <c>/api/dev/deliveries/…</c>, vốn đã bị gỡ chốt Development và cho BẤT KỲ ai ép đơn BẤT KỲ sang Delivered.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ConfirmReceivedFlowTests
{
    private readonly ApiTestFixture _fixture;

    public ConfirmReceivedFlowTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "RECV-01 [Normal] The customer confirms a shipped delivery; it goes through the full delivered path")]
    public async Task ConfirmReceived_ShippedDelivery_BecomesDelivered_AndLedgerWritten()
    {
        var (customer, orderId, deliveryId) = await PlaceCodOrderAsync();
        await AdvanceAsync(deliveryId, "Confirmed", "Preparing", "Shipped");

        var response = await customer.PostAsync($"/api/orders/{orderId}/confirm-received", null);

        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "xác nhận đã nhận"));
        var order = await ApiEnvelope.DataAsync(response);
        Assert.Equal("Completed", order.GetProperty("status").GetString());
        Assert.Equal("Delivered", order.GetProperty("deliveries")[0].GetProperty("status").GetString());
        Assert.Contains(await LedgerTypesAsync(deliveryId), t => t == LedgerEntryType.SaleCredit);
    }

    [Fact(DisplayName = "RECV-02 [Abnormal] Goods not yet shipped cannot be confirmed (would open the refund window early)")]
    public async Task ConfirmReceived_PendingDelivery_Conflict()
    {
        var (customer, orderId, deliveryId) = await PlaceCodOrderAsync();

        var response = await customer.PostAsync($"/api/orders/{orderId}/confirm-received", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(await LedgerTypesAsync(deliveryId));
    }

    [Fact(DisplayName = "RECV-03 [Abnormal] Another customer's order is invisible (404) and stays untouched")]
    public async Task ConfirmReceived_OtherCustomersOrder_NotFound()
    {
        var (_, orderId, deliveryId) = await PlaceCodOrderAsync();
        await AdvanceAsync(deliveryId, "Confirmed", "Preparing", "Shipped");
        var stranger = ScenarioUsers.ClientFor(_fixture, await ScenarioUsers.CreateAsync(_fixture));

        var response = await stranger.PostAsync($"/api/orders/{orderId}/confirm-received", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await LedgerTypesAsync(deliveryId));
    }

    [Theory(DisplayName = "RECV-04 [Security] Dev delivery shortcuts are closed outside Development")]
    [InlineData("/api/dev/deliveries/orders/{order}/shipping/delivered")]
    [InlineData("/api/dev/deliveries/orders/{order}/delivered")]
    [InlineData("/api/dev/deliveries/{delivery}/shipping/delivered")]
    [InlineData("/api/dev/deliveries/{delivery}/delivered")]
    public async Task DevDeliveryEndpoints_OutsideDevelopment_NotFound(string template)
    {
        var (customer, orderId, deliveryId) = await PlaceCodOrderAsync();
        var path = template.Replace("{order}", orderId.ToString()).Replace("{delivery}", deliveryId.ToString());

        var response = await customer.PostAsync(path, null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await LedgerTypesAsync(deliveryId));
    }

    // ===================== Hạ tầng =====================

    private async Task<(HttpClient Customer, Guid OrderId, Guid DeliveryId)> PlaceCodOrderAsync()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var data = await SalesScenario.SeedAsync(_fixture, user.Id);
        var client = ScenarioUsers.ClientFor(_fixture, user);
        var checkout = await client.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = data.ShippingAddressId,
            paymentMethod = "COD",
            items = new[] { new { productItemId = data.StoreA.ProductItemId, quantity = 1 } },
        });
        Assert.True(checkout.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(checkout, "đặt hàng"));
        var order = await ApiEnvelope.DataAsync(checkout);
        return (client, order.GetProperty("id").GetGuid(), order.GetProperty("deliveries")[0].GetProperty("id").GetGuid());
    }

    private async Task AdvanceAsync(Guid deliveryId, params string[] statuses)
    {
        var owner = _fixture.ClientFor(TestRole.GardenOwner);
        foreach (var status in statuses)
        {
            var r = await owner.PatchAsJsonAsync($"/api/orders/deliveries/{deliveryId}/status", new { status });
            Assert.True(r.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(r, $"delivery → {status}"));
        }
    }

    private async Task<List<LedgerEntryType>> LedgerTypesAsync(Guid deliveryId)
    {
        var types = new List<LedgerEntryType>();
        await _fixture.WithScopeAsync(async sp =>
            types = await sp.GetRequiredService<AppDbContext>().Set<LedgerEntry>().AsNoTracking()
                .Where(e => e.DeliveryId == deliveryId).Select(e => e.Type).ToListAsync());
        return types;
    }
}
