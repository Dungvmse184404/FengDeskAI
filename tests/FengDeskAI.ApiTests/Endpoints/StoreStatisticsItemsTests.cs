using System.Net.Http.Json;
using FengDeskAI.ApiTests.Infrastructure;
using Xunit;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// <c>itemsByStatus</c> gộp theo (SẢN PHẨM × TRẠNG THÁI) — tài liệu 15-stores.md. Lớp "Ordered" có HAI nguồn
/// (đơn PayOS chưa trả + đơn COD đang giao); trước đây mỗi nguồn gộp riêng rồi nối lại ⇒ cùng một sản phẩm ra
/// hai dòng "Ordered", FE dùng <c>productId-status</c> làm React key nên trùng key (E2E bắt được).
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class StoreStatisticsItemsTests
{
    private readonly ApiTestFixture _fixture;

    public StoreStatisticsItemsTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "STATS-ITEMS-01 [Normal] Unpaid PayOS and in-flight COD of one product merge into a single Ordered row")]
    public async Task ItemsByStatus_PayOsUnpaidAndCodRunning_OneOrderedRow()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var data = await SalesScenario.SeedAsync(_fixture, user.Id);
        var client = ScenarioUsers.ClientFor(_fixture, user);

        foreach (var (method, quantity) in new[] { ("PayOS", 2), ("COD", 1) })
        {
            var r = await client.PostAsJsonAsync("/api/orders", new
            {
                shippingAddressId = data.ShippingAddressId,
                paymentMethod = method,
                items = new[] { new { productItemId = data.StoreA.ProductItemId, quantity } },
            });
            Assert.True(r.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(r, $"đặt {method}"));
        }

        var stats = await ApiEnvelope.DataAsync(
            await _fixture.ClientFor(TestRole.GardenOwner).GetAsync($"/api/stores/{data.StoreA.StoreId}/statistics"));

        var ordered = stats.GetProperty("itemsByStatus").EnumerateArray()
            .Where(r => r.GetProperty("productId").GetGuid() == data.StoreA.ProductId
                        && r.GetProperty("status").GetString() == "Ordered")
            .ToList();
        var row = Assert.Single(ordered);
        Assert.Equal(3, row.GetProperty("quantity").GetInt32());
        Assert.Equal(2, row.GetProperty("orderCount").GetInt32());
        Assert.Equal(3 * data.StoreA.Price, row.GetProperty("value").GetDecimal());
    }
}
