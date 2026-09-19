using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Webhook nhà vận chuyển và thông báo cho khách — cặp này đi liền nhau vì thông báo giao hàng chỉ
/// sinh ra khi webhook đẩy trạng thái mới về.
///
/// Vì sao webhook phải có test riêng: <c>AuthorizationMatrixTests</c> CỐ Ý bỏ qua mọi route chứa
/// "webhook" (chúng dùng cơ chế xác thực riêng, không phải JWT) — đúng/sai secret là việc của tầng 2,
/// tức file này.
///
/// Xác thực webhook KHÔNG phải chữ ký HMAC: chỉ là so sánh chuỗi. Bên chung dùng header
/// <c>X-Webhook-Secret</c>, riêng GHN đọc từ query <c>?key=</c>. Secret sinh ngẫu nhiên mỗi lần chạy
/// và lấy qua <c>ApiTestFactory.ShippingWebhookSecret</c>.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ShippingFlowTests
{
    private const string SecretHeader = "X-Webhook-Secret";

    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ShippingFlowTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ===================== Xác thực webhook =====================

    [Fact(DisplayName = "SHIP-01 [Normal] A webhook carrying the right secret advances the delivery")]
    public async Task Webhook_WithValidSecret_AdvancesDelivery()
    {
        var deliveryId = await PendingDeliveryAsync();

        var response = await SendWebhookAsync(deliveryId, "Confirmed", _fixture.Factory.ShippingWebhookSecret);
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)response.StatusCode} {body}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "SHIP-02 [Abnormal] A webhook with the wrong secret is rejected")]
    public async Task Webhook_WithWrongSecret_IsUnauthorized()
    {
        var deliveryId = await PendingDeliveryAsync();

        var response = await SendWebhookAsync(deliveryId, "Confirmed", "day-la-secret-sai");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("secret", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "SHIP-03 [Abnormal] A webhook with no secret header at all is rejected")]
    public async Task Webhook_WithoutSecret_IsUnauthorized()
    {
        var deliveryId = await PendingDeliveryAsync();

        var response = await SendWebhookAsync(deliveryId, "Confirmed", secret: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "SHIP-04 [Abnormal] A webhook for an unknown delivery is accepted for reconciliation")]
    public async Task Webhook_UnknownDelivery_IsAcceptedForLaterMatching()
    {
        var response = await SendWebhookAsync(Guid.NewGuid(), "Confirmed", _fixture.Factory.ShippingWebhookSecret);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact(DisplayName = "SHIP-05 [Abnormal] A webhook proposing an illegal transition returns 409")]
    public async Task Webhook_IllegalTransition_ReturnsConflict()
    {
        var deliveryId = await PendingDeliveryAsync();

        // Pending không nhảy thẳng sang Delivered được — phải qua Confirmed → Preparing → Shipped.
        var response = await SendWebhookAsync(deliveryId, "Delivered", _fixture.Factory.ShippingWebhookSecret);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact(DisplayName = "SHIP-06 [Abnormal] The GHN webhook rejects a wrong key in the query string")]
    public async Task GhnWebhook_WithWrongKey_IsUnauthorized()
    {
        var response = await _fixture.ClientFor(TestRole.Anonymous)
            .PostAsJsonAsync("/api/shipping/ghn/webhook?key=sai-key", new { Type = "switch_status", Status = "delivered" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "SHIP-07 [Normal] The GHN webhook ignores events that do not change status")]
    public async Task GhnWebhook_NonStatusEvent_IsIgnored()
    {
        var secret = _fixture.Factory.ShippingWebhookSecret;

        var response = await _fixture.ClientFor(TestRole.Anonymous)
            .PostAsJsonAsync($"/api/shipping/ghn/webhook?key={Uri.EscapeDataString(secret)}",
                new { Type = "update_cod", OrderCode = "GHN-TEST" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ===================== Thông báo sinh ra từ webhook =====================

    [Fact(DisplayName = "SHIP-08 [Normal] A delivery webhook notifies the customer")]
    public async Task Webhook_Confirmed_NotifiesCustomer()
    {
        var deliveryId = await PendingDeliveryAsync();

        await SendWebhookAsync(deliveryId, "Confirmed", _fixture.Factory.ShippingWebhookSecret);

        var notifications = await Customer().GetAsync("/api/notifications?pageSize=100");
        var items = (await ApiEnvelope.DataAsync(notifications)).GetProperty("items");

        Assert.Contains(items.EnumerateArray(), n =>
            n.GetProperty("type").GetString() == "DeliveryConfirmed"
            && n.GetProperty("referenceId").ValueKind != JsonValueKind.Null
            && n.GetProperty("referenceId").GetGuid() == deliveryId);
    }

    [Fact(DisplayName = "SHIP-09 [Normal] A new notification raises the unread count")]
    public async Task Webhook_Confirmed_RaisesUnreadCount()
    {
        var before = await UnreadCountAsync();
        var deliveryId = await PendingDeliveryAsync();

        await SendWebhookAsync(deliveryId, "Confirmed", _fixture.Factory.ShippingWebhookSecret);

        Assert.True(await UnreadCountAsync() > before,
            "Webhook giao hàng phải đẩy số thông báo chưa đọc lên.");
    }

    [Fact(DisplayName = "NOTI-01 [Normal] Marking one notification read lowers the unread count")]
    public async Task MarkRead_OneNotification_LowersUnreadCount()
    {
        var notificationId = await UnreadNotificationIdAsync();
        var before = await UnreadCountAsync();

        var response = await Customer().PatchAsync($"/api/notifications/{notificationId}/read", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(before - 1, await UnreadCountAsync());
    }

    [Fact(DisplayName = "NOTI-02 [Normal] Marking a notification read twice is harmless")]
    public async Task MarkRead_Twice_IsIdempotent()
    {
        var notificationId = await UnreadNotificationIdAsync();
        await Customer().PatchAsync($"/api/notifications/{notificationId}/read", null);

        var second = await Customer().PatchAsync($"/api/notifications/{notificationId}/read", null);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact(DisplayName = "NOTI-03 [Normal] Mark-all-read clears the unread count")]
    public async Task MarkAllRead_ClearsUnreadCount()
    {
        await UnreadNotificationIdAsync();

        var response = await Customer().PatchAsync("/api/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(0, await UnreadCountAsync());
    }

    [Fact(DisplayName = "NOTI-04 [Normal] The unreadOnly filter hides notifications already read")]
    public async Task ListNotifications_UnreadOnly_ExcludesReadOnes()
    {
        var notificationId = await UnreadNotificationIdAsync();
        await Customer().PatchAsync($"/api/notifications/{notificationId}/read", null);

        var response = await Customer().GetAsync("/api/notifications?unreadOnly=true&pageSize=100");

        var items = (await ApiEnvelope.DataAsync(response)).GetProperty("items");
        Assert.DoesNotContain(items.EnumerateArray(), n => n.GetProperty("id").GetGuid() == notificationId);
    }

    [Fact(DisplayName = "NOTI-05 [Abnormal] A notification of another user is invisible — 404, not 403")]
    public async Task MarkRead_OfAnotherUser_ReturnsNotFound()
    {
        var notificationId = await UnreadNotificationIdAsync();
        var stranger = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, stranger)
            .PatchAsync($"/api/notifications/{notificationId}/read", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "NOTI-06 [Normal] A brand-new customer has no notifications")]
    public async Task ListNotifications_ForNewUser_IsEmpty()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user).GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await ApiEnvelope.DataAsync(response)).GetProperty("items").EnumerateArray());
    }

    // ===================== Tiến trình giao hàng & sẵn sàng giao =====================

    [Fact(DisplayName = "SHIP-10 [Normal] The customer reads the progress log of their own delivery")]
    public async Task Progress_AsOrderCustomer_Succeeds()
    {
        var deliveryId = await PendingDeliveryAsync();
        await SendWebhookAsync(deliveryId, "Confirmed", _fixture.Factory.ShippingWebhookSecret);

        var response = await Customer().GetAsync($"/api/shipping/deliveries/{deliveryId}/progress");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty((await ApiEnvelope.DataAsync(response)).EnumerateArray());
    }

    [Fact(DisplayName = "SHIP-11 [Abnormal] A stranger cannot read another order's delivery progress")]
    public async Task Progress_AsStranger_IsForbidden()
    {
        var deliveryId = await PendingDeliveryAsync();
        var stranger = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, stranger)
            .GetAsync($"/api/shipping/deliveries/{deliveryId}/progress");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "SHIP-12 [Abnormal] Reading the progress of an unknown delivery returns 404")]
    public async Task Progress_UnknownDelivery_ReturnsNotFound()
    {
        var response = await Customer().GetAsync($"/api/shipping/deliveries/{Guid.NewGuid()}/progress");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "SHIP-13 [Abnormal] Requesting redelivery on a delivery that has not failed is refused")]
    public async Task Redeliver_WhilePending_IsRejected()
    {
        var deliveryId = await PendingDeliveryAsync();

        var response = await _fixture.ClientFor(TestRole.GardenOwner)
            .PostAsync($"/api/shipping/deliveries/{deliveryId}/redeliver", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "SHIP-14 [Normal] A store with a complete pickup address is ready to ship")]
    public async Task Readiness_ForSeededStore_ReportsReady()
    {
        var scenario = await SalesScenario.SeedAsync(_fixture, _fixture.UserId(TestRole.Customer), storeCount: 1);

        var response = await _fixture.ClientFor(TestRole.GardenOwner)
            .GetAsync($"/api/shipping/stores/{scenario.Stores[0].StoreId}/readiness");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.True(data.GetProperty("isReady").GetBoolean(),
            $"Cửa hàng do SalesScenario dựng phải đủ điều kiện giao: {data}");
    }

    // ===================== Helper =====================

    private HttpClient Customer() => _fixture.ClientFor(TestRole.Customer);

    /// <summary>Một delivery vừa đặt, còn ở trạng thái Pending.</summary>
    private async Task<Guid> PendingDeliveryAsync()
    {
        var data = await SalesScenario.SeedAsync(_fixture, _fixture.UserId(TestRole.Customer), storeCount: 1);
        var customer = Customer();

        await customer.DeleteAsync("/api/cart");
        var add = await customer.PostAsJsonAsync("/api/cart/items",
            new { productItemId = data.Stores[0].ProductItemId, quantity = 1 });
        Assert.True(add.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(add, "thêm vào giỏ"));

        var checkout = await customer.PostAsJsonAsync("/api/orders",
            new { shippingAddressId = data.ShippingAddressId, paymentMethod = "COD" });
        Assert.True(checkout.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(checkout, "đặt hàng"));

        return (await ApiEnvelope.DataAsync(checkout)).GetProperty("deliveries")[0].GetProperty("id").GetGuid();
    }

    /// <summary>Gửi webhook chung. <paramref name="secret"/> null nghĩa là không gắn header nào cả.</summary>
    private async Task<HttpResponseMessage> SendWebhookAsync(Guid deliveryId, string newStatus, string? secret)
    {
        var payload = JsonSerializer.Serialize(new
        {
            provider = "ShopeeExpress",
            eventType = "status_update",
            deliveryId,
            newStatus,
            trackingCode = $"VN{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/shipping/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        if (secret is not null) request.Headers.Add(SecretHeader, secret);

        return await _fixture.ClientFor(TestRole.Anonymous).SendAsync(request);
    }

    private async Task<int> UnreadCountAsync()
    {
        var response = await Customer().GetAsync("/api/notifications/unread-count");
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "đọc số thông báo chưa đọc"));
        return (await ApiEnvelope.DataAsync(response)).GetInt32();
    }

    /// <summary>Đảm bảo khách mẫu có ít nhất một thông báo chưa đọc, rồi trả về id của nó.</summary>
    private async Task<Guid> UnreadNotificationIdAsync()
    {
        var deliveryId = await PendingDeliveryAsync();
        await SendWebhookAsync(deliveryId, "Confirmed", _fixture.Factory.ShippingWebhookSecret);

        var response = await Customer().GetAsync("/api/notifications?unreadOnly=true&pageSize=100");
        var items = (await ApiEnvelope.DataAsync(response)).GetProperty("items").EnumerateArray().ToList();

        Assert.True(items.Count > 0, "Webhook giao hàng phải sinh ra thông báo chưa đọc cho khách.");
        return items[0].GetProperty("id").GetGuid();
    }
}
