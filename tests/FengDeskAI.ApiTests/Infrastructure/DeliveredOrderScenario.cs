using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FengDeskAI.ApiTests.Infrastructure;

/// <summary>Một đơn đã giao thành công, kèm các id cần để mở yêu cầu trả hàng.</summary>
public sealed record DeliveredOrder(Guid OrderId, Guid DeliveryId, Guid OrderItemId, Guid StoreId, Guid ExchangeProductItemId);

/// <summary>
/// Đưa một đơn hàng đi hết vòng đời tới trạng thái đã giao — điều kiện tiên quyết của mọi ca RMA.
///
/// Cố ý đi qua API thật (<c>PATCH /api/orders/deliveries/{id}/status</c>) thay vì ghi thẳng trạng
/// thái vào DB: chuỗi Pending → Confirmed → Preparing → Shipped → Delivered đều hợp lệ theo
/// <c>OrderWorkflow.IsValidDeliveryTransition</c>, nên đi đường thật vừa dựng được dữ liệu vừa
/// kiểm luôn máy trạng thái giao hàng. Ghi tắt vào DB sẽ bỏ lọt lỗi ở chính chuỗi này.
/// </summary>
public static class DeliveredOrderScenario
{
    public static async Task<DeliveredOrder> CreateAsync(ApiTestFixture fixture)
    {
        var data = await SalesScenario.SeedAsync(fixture, fixture.UserId(TestRole.Customer));
        var customer = fixture.ClientFor(TestRole.Customer);

        await customer.DeleteAsync("/api/cart");
        var add = await customer.PostAsJsonAsync("/api/cart/items",
            new { productItemId = data.StoreA.ProductItemId, quantity = 1 });
        Assert.True(add.IsSuccessStatusCode, await Describe(add, "thêm vào giỏ"));

        var checkout = await customer.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = data.ShippingAddressId,
            paymentMethod = "COD",
        });
        Assert.True(checkout.IsSuccessStatusCode, await Describe(checkout, "đặt hàng"));

        using var order = JsonDocument.Parse(await checkout.Content.ReadAsStringAsync());
        var root = order.RootElement.GetProperty("data");
        var orderId = root.GetProperty("id").GetGuid();
        var deliveryId = root.GetProperty("deliveries")[0].GetProperty("id").GetGuid();
        var orderItemId = root.GetProperty("items")[0].GetProperty("id").GetGuid();

        var owner = fixture.ClientFor(TestRole.GardenOwner);
        foreach (var status in new[] { "Confirmed", "Preparing", "Shipped", "Delivered" })
        {
            var response = await owner.PatchAsJsonAsync(
                $"/api/orders/deliveries/{deliveryId}/status", new { status });
            Assert.True(response.IsSuccessStatusCode, await Describe(response, $"chuyển delivery sang {status}"));
        }

        return new DeliveredOrder(orderId, deliveryId, orderItemId, data.StoreA.StoreId, data.StoreB.ProductItemId);
    }

    private static async Task<string> Describe(HttpResponseMessage response, string step)
        => $"Bước '{step}' thất bại: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}";
}
