using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Đợt 2 — luồng ra tiền: giỏ hàng → tách đơn theo vườn → thanh toán → giao hàng.
/// Dữ liệu nền do <see cref="SalesScenario"/> dựng (xem ghi chú ở đó về mã GHN).
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class SalesFlowTests
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SalesFlowTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ===================== Giỏ hàng =====================

    [Fact(DisplayName = "SALES-CART-01 [Normal] Add a product to the cart and read the cart back")]
    public async Task AddItemToCart_ThenReadCartBack()
    {
        var data = await SeedAsync();
        var client = _fixture.ClientFor(TestRole.Customer);
        await ClearCartAsync(client);

        var add = await client.PostAsJsonAsync("/api/cart/items",
            new { productItemId = data.StoreA.ProductItemId, quantity = 2 });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        var cart = await client.GetAsync("/api/cart");
        var body = await cart.Content.ReadAsStringAsync();
        _output.WriteLine(body);

        using var doc = JsonDocument.Parse(body);
        var items = doc.RootElement.GetProperty("data").GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(2, items[0].GetProperty("quantity").GetInt32());
        Assert.Equal(data.StoreA.Price * 2, doc.RootElement.GetProperty("data").GetProperty("subtotal").GetDecimal());
    }

    [Fact(DisplayName = "SALES-CART-02 [Abnormal] Adding to cart with quantity zero is rejected")]
    public async Task AddToCart_ZeroQuantity_IsRejected()
    {
        var data = await SeedAsync();
        var client = _fixture.ClientFor(TestRole.Customer);

        var response = await client.PostAsJsonAsync("/api/cart/items",
            new { productItemId = data.StoreA.ProductItemId, quantity = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "SALES-CART-03 [Abnormal] Adding more than the available stock is rejected")]
    public async Task AddToCart_ExceedingStock_IsRejected()
    {
        var data = await SeedAsync();
        var client = _fixture.ClientFor(TestRole.Customer);
        await ClearCartAsync(client);

        var response = await client.PostAsJsonAsync("/api/cart/items",
            new { productItemId = data.StoreA.ProductItemId, quantity = data.StoreA.Stock + 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("tồn kho", await ReadMessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "SALES-CART-04 [Abnormal] Adding an unknown product variant returns 404")]
    public async Task AddToCart_UnknownVariant_Returns404()
    {
        var client = _fixture.ClientFor(TestRole.Customer);

        var response = await client.PostAsJsonAsync("/api/cart/items",
            new { productItemId = Guid.NewGuid(), quantity = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ===================== Đặt hàng + tách đơn =====================

    [Fact(DisplayName = "SALES-ORDER-01 [Normal] A cart spanning two stores splits into two deliveries")]
    public async Task Checkout_CartFromTwoStores_SplitsIntoTwoDeliveries()
    {
        var data = await SeedAsync();
        var client = _fixture.ClientFor(TestRole.Customer);
        await ClearCartAsync(client);

        await AddToCartAsync(client, data.StoreA.ProductItemId, 1);
        await AddToCartAsync(client, data.StoreB.ProductItemId, 1);

        var checkout = await client.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = data.ShippingAddressId,
            paymentMethod = "COD",
        });

        var body = await checkout.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)checkout.StatusCode} {body}");
        Assert.True(checkout.IsSuccessStatusCode, $"Đặt hàng thất bại: {(int)checkout.StatusCode} {body}");

        // Sản phẩm của 2 vườn khác nhau → phải sinh 2 delivery riêng, mỗi vườn một đơn giao.
        var storeIds = DeliveryStoreIds(body);
        Assert.Equal(2, storeIds.Count);
        Assert.Contains(data.StoreA.StoreId, storeIds);
        Assert.Contains(data.StoreB.StoreId, storeIds);
    }

    [Fact(DisplayName = "SALES-ORDER-02 [Normal] A cart from a single store creates one delivery")]
    public async Task Checkout_CartFromOneStore_CreatesSingleDelivery()
    {
        var data = await SeedAsync();
        var client = _fixture.ClientFor(TestRole.Customer);
        await ClearCartAsync(client);
        await AddToCartAsync(client, data.StoreA.ProductItemId, 1);

        var checkout = await client.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = data.ShippingAddressId,
            paymentMethod = "COD",
        });

        var body = await checkout.Content.ReadAsStringAsync();
        Assert.True(checkout.IsSuccessStatusCode, $"Đặt hàng thất bại: {(int)checkout.StatusCode} {body}");
        Assert.Single(DeliveryStoreIds(body));
    }

    [Fact(DisplayName = "SALES-ORDER-03 [Abnormal] Checking out an empty cart is rejected")]
    public async Task Checkout_EmptyCart_IsRejected()
    {
        var data = await SeedAsync();
        var client = _fixture.ClientFor(TestRole.Customer);
        await ClearCartAsync(client);

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = data.ShippingAddressId,
            paymentMethod = "COD",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "SALES-ORDER-04 [Abnormal] Checkout with an unknown shipping address is rejected")]
    public async Task Checkout_UnknownShippingAddress_IsRejected()
    {
        var data = await SeedAsync();
        var client = _fixture.ClientFor(TestRole.Customer);
        await ClearCartAsync(client);
        await AddToCartAsync(client, data.StoreA.ProductItemId, 1);

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = Guid.NewGuid(),
            paymentMethod = "COD",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "SALES-ORDER-05 [Normal] Ordered items are removed from the cart after checkout")]
    public async Task Checkout_RemovesOrderedItemsFromCart()
    {
        var data = await SeedAsync();
        var client = _fixture.ClientFor(TestRole.Customer);
        await ClearCartAsync(client);
        await AddToCartAsync(client, data.StoreA.ProductItemId, 1);

        var checkout = await client.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = data.ShippingAddressId,
            paymentMethod = "COD",
        });
        Assert.True(checkout.IsSuccessStatusCode);

        var cart = await client.GetAsync("/api/cart");
        using var doc = JsonDocument.Parse(await cart.Content.ReadAsStringAsync());
        Assert.Equal(0, doc.RootElement.GetProperty("data").GetProperty("items").GetArrayLength());
    }

    [Fact(DisplayName = "SALES-ORDER-06 [Normal] A placed order appears in the customer order history")]
    public async Task PlacedOrder_AppearsInCustomerOrderHistory()
    {
        var data = await SeedAsync();
        var client = _fixture.ClientFor(TestRole.Customer);
        await ClearCartAsync(client);
        await AddToCartAsync(client, data.StoreA.ProductItemId, 1);

        var checkout = await client.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = data.ShippingAddressId,
            paymentMethod = "COD",
        });
        var orderId = await ReadOrderIdAsync(checkout);

        var mine = await client.GetAsync("/api/orders?page=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        Assert.Contains(orderId.ToString(), await mine.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var detail = await client.GetAsync($"/api/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
    }

    [Fact(DisplayName = "SALES-ORDER-07 [Abnormal] An order is not visible to another customer")]
    public async Task Order_IsNotVisibleToAnotherCustomer()
    {
        var data = await SeedAsync();
        var owner = _fixture.ClientFor(TestRole.Customer);
        await ClearCartAsync(owner);
        await AddToCartAsync(owner, data.StoreA.ProductItemId, 1);

        var checkout = await owner.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = data.ShippingAddressId,
            paymentMethod = "COD",
        });
        var orderId = await ReadOrderIdAsync(checkout);

        // Phải dùng một Customer KHÁC: Staff/Manager/Admin xem được mọi đơn theo thiết kế
        // (OrderService.GetByIdAsync bỏ lọc chủ sở hữu khi isPrivileged), nên họ không chứng minh
        // được tính cách ly giữa các khách hàng.
        var otherCustomer = await NewCustomerClientAsync();
        var response = await otherCustomer.GetAsync($"/api/orders/{orderId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ===================== Thanh toán =====================

    [Fact(DisplayName = "SALES-PAY-01 [Normal] Create a payment link for an online order")]
    public async Task CreatePaymentLink_ForOnlineOrder_Succeeds()
    {
        var orderId = await PlaceOrderAsync("PayOS");
        var client = _fixture.ClientFor(TestRole.Customer);

        var pay = await client.PostAsync($"/api/payments/{orderId}", null);
        var body = await pay.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)pay.StatusCode} {body}");

        Assert.True(pay.IsSuccessStatusCode, $"Tạo link thanh toán thất bại: {(int)pay.StatusCode} {body}");
        Assert.Contains("fake-gateway.test", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "SALES-PAY-02 [Abnormal] A COD order cannot create an online payment link")]
    public async Task CreatePaymentLink_ForCodOrder_IsRejected()
    {
        var orderId = await PlaceOrderAsync("COD");

        var pay = await _fixture.ClientFor(TestRole.Customer).PostAsync($"/api/payments/{orderId}", null);

        Assert.Equal(HttpStatusCode.BadRequest, pay.StatusCode);
        Assert.Contains("COD", await ReadMessageAsync(pay), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "SALES-PAY-03 [Abnormal] Creating a payment link for an unknown order returns 404")]
    public async Task CreatePaymentLink_UnknownOrder_Returns404()
    {
        var pay = await _fixture.ClientFor(TestRole.Customer).PostAsync($"/api/payments/{Guid.NewGuid()}", null);

        Assert.Equal(HttpStatusCode.NotFound, pay.StatusCode);
    }

    [Fact(DisplayName = "SALES-PAY-04 [Abnormal] Payment webhook with a malformed payload does not return 5xx")]
    public async Task PaymentWebhook_MalformedPayload_DoesNotReturn5xx()
    {
        var response = await _fixture.ClientFor(TestRole.Anonymous)
            .PostAsJsonAsync("/api/payments/payos/webhook", new { khong = "hop le" });

        // Chấp nhận mọi phản hồi trừ 5xx — webhook không được sập vì payload lạ.
        Assert.True((int)response.StatusCode < 500,
            $"Webhook trả {(int)response.StatusCode} với payload rác — không được nổ 5xx.");
    }

    // ===================== Hủy đơn =====================

    [Fact(DisplayName = "SALES-CANCEL-01 [Normal] Cancel an order while it is still pending")]
    public async Task CancelOrder_WhilePending_Succeeds()
    {
        var orderId = await PlaceOrderAsync("COD");
        var client = _fixture.ClientFor(TestRole.Customer);

        var cancel = await client.PostAsync($"/api/orders/{orderId}/cancel", null);
        var body = await cancel.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)cancel.StatusCode} {body}");

        Assert.True(cancel.IsSuccessStatusCode, $"Hủy đơn thất bại: {(int)cancel.StatusCode} {body}");
    }

    [Fact(DisplayName = "SALES-CANCEL-02 [Abnormal] Cancelling the same order twice is rejected")]
    public async Task CancelOrder_Twice_IsRejected()
    {
        var orderId = await PlaceOrderAsync("COD");
        var client = _fixture.ClientFor(TestRole.Customer);

        var first = await client.PostAsync($"/api/orders/{orderId}/cancel", null);
        Assert.True(first.IsSuccessStatusCode);

        var second = await client.PostAsync($"/api/orders/{orderId}/cancel", null);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    // ===================== Giao hàng =====================

    [Fact(DisplayName = "SALES-DELIVERY-01 [Normal] A store owner can list deliveries of their own store")]
    public async Task StoreOwner_CanListOwnStoreDeliveries()
    {
        var data = await SeedAsync();
        var customer = _fixture.ClientFor(TestRole.Customer);
        await ClearCartAsync(customer);
        await AddToCartAsync(customer, data.StoreA.ProductItemId, 1);
        await customer.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = data.ShippingAddressId,
            paymentMethod = "COD",
        });

        var response = await _fixture.ClientFor(TestRole.GardenOwner)
            .GetAsync($"/api/orders/stores/{data.StoreA.StoreId}/deliveries?page=1&pageSize=50");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {body}");
        _output.WriteLine(body);
    }

    [Fact(DisplayName = "SALES-DELIVERY-02 [Abnormal] A customer cannot list store deliveries")]
    public async Task Customer_CannotListStoreDeliveries()
    {
        var data = await SeedAsync();

        var response = await _fixture.ClientFor(TestRole.Customer)
            .GetAsync($"/api/orders/stores/{data.StoreA.StoreId}/deliveries?page=1&pageSize=50");

        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Mong đợi 403/401, nhận {(int)response.StatusCode}");
    }

    // ===================== Tiện ích =====================

    private Task<SalesScenarioData> SeedAsync()
        => SalesScenario.SeedAsync(_fixture, _fixture.UserId(TestRole.Customer));

    private async Task<Guid> PlaceOrderAsync(string paymentMethod)
    {
        var data = await SeedAsync();
        var client = _fixture.ClientFor(TestRole.Customer);
        await ClearCartAsync(client);
        await AddToCartAsync(client, data.StoreA.ProductItemId, 1);

        var checkout = await client.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = data.ShippingAddressId,
            paymentMethod,
        });

        var body = await checkout.Content.ReadAsStringAsync();
        Assert.True(checkout.IsSuccessStatusCode, $"Đặt hàng thất bại: {(int)checkout.StatusCode} {body}");
        return await ReadOrderIdAsync(checkout);
    }

    /// <summary>
    /// Đăng ký một khách hàng mới qua đúng luồng 3 bước và trả client đã đăng nhập.
    /// Cần một Customer THỨ HAI để chứng minh tính cách ly dữ liệu giữa các khách — user mẫu
    /// dùng chung không làm được việc đó.
    /// </summary>
    private async Task<HttpClient> NewCustomerClientAsync()
    {
        var anonymous = _fixture.ClientFor(TestRole.Anonymous);
        var email = ApiTestFixture.NewEmail();

        await anonymous.PostAsJsonAsync("/api/Auth/register/initiate", new { email });
        var otp = _fixture.Factory.Emails.LatestOtpFor(email);
        var verify = await anonymous.PostAsJsonAsync("/api/Auth/register/verify", new { email, otp });

        using var verifyDoc = JsonDocument.Parse(await verify.Content.ReadAsStringAsync());
        var registrationToken = verifyDoc.RootElement.GetProperty("data").GetProperty("registrationToken").GetString();

        var finalize = await anonymous.PostAsJsonAsync("/api/Auth/register/finalize", new
        {
            registrationToken,
            password = _fixture.Password,
            fullName = "Khách hàng thứ hai",
        });
        Assert.True(finalize.IsSuccessStatusCode,
            $"Tạo khách hàng phụ thất bại: {(int)finalize.StatusCode} {await finalize.Content.ReadAsStringAsync()}");

        using var doc = JsonDocument.Parse(await finalize.Content.ReadAsStringAsync());
        var accessToken = doc.RootElement.GetProperty("data").GetProperty("accessToken").GetString();

        var client = _fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static async Task ClearCartAsync(HttpClient client) => await client.DeleteAsync("/api/cart");

    private static async Task AddToCartAsync(HttpClient client, Guid productItemId, int quantity)
    {
        var response = await client.PostAsJsonAsync("/api/cart/items", new { productItemId, quantity });
        Assert.True(response.IsSuccessStatusCode,
            $"Thêm vào giỏ thất bại: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task<Guid> ReadOrderIdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    /// <summary>Gom storeId của mọi delivery trong response đặt hàng — dùng để đếm số đơn đã tách.</summary>
    private static List<Guid> DeliveryStoreIds(string checkoutBody)
    {
        using var doc = JsonDocument.Parse(checkoutBody);
        var data = doc.RootElement.GetProperty("data");

        if (!data.TryGetProperty("deliveries", out var deliveries) || deliveries.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Response đặt hàng không có mảng deliveries: {checkoutBody}");

        return deliveries.EnumerateArray()
            .Select(d => d.GetProperty("gardenStoreId").GetGuid())
            .Distinct()
            .ToList();
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
    }
}
