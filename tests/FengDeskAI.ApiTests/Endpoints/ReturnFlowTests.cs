using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Đợt 3 — RMA: khách tạo yêu cầu trả hàng → staff nền tảng quyết định → hoàn tiền / đổi hàng /
/// từ chối, kèm kiểm tra phân quyền giữa khách, chủ vườn và staff.
///
/// Phạm vi: luồng chính + phân quyền. KHÔNG phủ SLA worker (worker đã tắt trong test) và công nợ
/// nhà cung cấp (VendorLiability) — hai phần đó cần đợt riêng.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ReturnFlowTests
{
    /// <summary>Ảnh bằng chứng: API nhận URL đã upload sẵn, không nhận file ở bước tạo ticket.</summary>
    private const string EvidenceImageUrl = "https://fake-storage.test/bucket/evidence/test.jpg";

    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ReturnFlowTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ===================== Tạo yêu cầu =====================

    [Fact(DisplayName = "RMA-01 [Normal] Customer opens a return request on a delivered order")]
    public async Task CreateReturn_OnDeliveredOrder_Succeeds()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);

        var response = await CreateReturnAsync(order);
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)response.StatusCode} {body}");

        Assert.True(response.IsSuccessStatusCode, $"Tạo yêu cầu thất bại: {(int)response.StatusCode} {body}");
        Assert.Equal("Requested", ReadStatus(body));
    }

    [Fact(DisplayName = "RMA-02 [Abnormal] A return request without evidence images is rejected")]
    public async Task CreateReturn_WithoutEvidence_IsRejected()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);

        var response = await _fixture.ClientFor(TestRole.Customer).PostAsJsonAsync("/api/returns", new
        {
            deliveryId = order.DeliveryId,
            type = "Refund",
            reason = "PlantHealth",
            reasonDetail = "Cây đến nơi đã héo.",
            items = new[] { new { orderItemId = order.OrderItemId, quantity = 1 } },
            imageUrls = Array.Empty<string>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("bằng chứng", await ReadMessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "RMA-03 [Abnormal] A return request on a delivery that was never delivered is rejected")]
    public async Task CreateReturn_OnUndeliveredDelivery_IsRejected()
    {
        // Đơn vừa đặt đang ở Pending — chưa giao thì chưa được mở yêu cầu trả hàng.
        var data = await SalesScenario.SeedAsync(_fixture, _fixture.UserId(TestRole.Customer));
        var customer = _fixture.ClientFor(TestRole.Customer);
        await customer.DeleteAsync("/api/cart");
        await customer.PostAsJsonAsync("/api/cart/items",
            new { productItemId = data.StoreA.ProductItemId, quantity = 1 });

        var checkout = await customer.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = data.ShippingAddressId,
            paymentMethod = "COD",
        });
        using var doc = JsonDocument.Parse(await checkout.Content.ReadAsStringAsync());
        var root = doc.RootElement.GetProperty("data");

        var response = await customer.PostAsJsonAsync("/api/returns", new
        {
            deliveryId = root.GetProperty("deliveries")[0].GetProperty("id").GetGuid(),
            type = "Refund",
            reason = "PlantHealth",
            items = new[] { new { orderItemId = root.GetProperty("items")[0].GetProperty("id").GetGuid(), quantity = 1 } },
            imageUrls = new[] { EvidenceImageUrl },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "RMA-04 [Abnormal] Returning more units than were delivered is rejected")]
    public async Task CreateReturn_QuantityAboveDelivered_IsRejected()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);

        var response = await CreateReturnAsync(order, quantity: 99);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "RMA-05 [Boundary] Returning zero units is rejected")]
    public async Task CreateReturn_ZeroQuantity_IsRejected()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);

        var response = await CreateReturnAsync(order, quantity: 0);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ===================== Khách tự hủy =====================

    [Fact(DisplayName = "RMA-06 [Normal] Customer cancels their own request while it is still pending")]
    public async Task CancelReturn_WhileRequested_Succeeds()
    {
        var ticketId = await OpenTicketAsync();

        var cancel = await _fixture.ClientFor(TestRole.Customer).PostAsync($"/api/returns/{ticketId}/cancel", null);

        Assert.True(cancel.IsSuccessStatusCode, await Describe(cancel));
        Assert.Equal("Cancelled", ReadStatus(await cancel.Content.ReadAsStringAsync()));
    }

    [Fact(DisplayName = "RMA-07 [Abnormal] Cancelling the same request twice is rejected")]
    public async Task CancelReturn_Twice_IsRejected()
    {
        var ticketId = await OpenTicketAsync();
        var customer = _fixture.ClientFor(TestRole.Customer);

        var first = await customer.PostAsync($"/api/returns/{ticketId}/cancel", null);
        Assert.True(first.IsSuccessStatusCode, await Describe(first));

        var second = await customer.PostAsync($"/api/returns/{ticketId}/cancel", null);
        Assert.True((int)second.StatusCode is 400 or 409,
            $"Mong đợi 400/409, nhận {(int)second.StatusCode}");
    }

    // ===================== Staff quyết định =====================

    [Fact(DisplayName = "RMA-08 [Normal] Platform staff accepts the request for review")]
    public async Task AcceptReturn_ByStaff_MovesOutOfRequested()
    {
        var ticketId = await OpenTicketAsync();

        var accept = await _fixture.ClientFor(TestRole.Staff).PostAsync($"/api/returns/{ticketId}/accept", null);
        var body = await accept.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)accept.StatusCode} {body}");

        Assert.True(accept.IsSuccessStatusCode, await Describe(accept));
        Assert.NotEqual("Requested", ReadStatus(body));
    }

    [Fact(DisplayName = "RMA-09 [Normal] Platform staff rejects the request with a reason")]
    public async Task RejectReturn_WithReason_MovesToRejected()
    {
        var ticketId = await OpenTicketAsync();
        var staff = _fixture.ClientFor(TestRole.Staff);
        await staff.PostAsync($"/api/returns/{ticketId}/accept", null);

        var reject = await staff.PostAsJsonAsync($"/api/returns/{ticketId}/reject",
            new { reason = "Bằng chứng không cho thấy sản phẩm bị lỗi." });
        var body = await reject.Content.ReadAsStringAsync();

        Assert.True(reject.IsSuccessStatusCode, await Describe(reject));
        Assert.Equal("Rejected", ReadStatus(body));
    }

    [Fact(DisplayName = "RMA-10 [Abnormal] Rejecting without a reason is refused")]
    public async Task RejectReturn_WithoutReason_IsRejected()
    {
        var ticketId = await OpenTicketAsync();
        var staff = _fixture.ClientFor(TestRole.Staff);
        await staff.PostAsync($"/api/returns/{ticketId}/accept", null);

        var reject = await staff.PostAsJsonAsync($"/api/returns/{ticketId}/reject", new { reason = "" });

        Assert.Equal(HttpStatusCode.BadRequest, reject.StatusCode);
    }

    [Fact(DisplayName = "RMA-11 [Normal] Dead-plant refund reaches the refunding stage without recalling goods")]
    public async Task ApproveRefund_ForPlantHealth_SkipsPhysicalReturn()
    {
        var ticketId = await OpenTicketAsync();
        var staff = _fixture.ClientFor(TestRole.Staff);

        await staff.PostAsync($"/api/returns/{ticketId}/accept", null);

        var approve = await staff.PostAsJsonAsync($"/api/returns/{ticketId}/approve-refund",
            new { restock = false, note = "Cây chết — hoàn tiền, không thu hồi hàng." });
        var body = await approve.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)approve.StatusCode} {body}");

        Assert.True(approve.IsSuccessStatusCode, await Describe(approve));

        // BR-09: cây chết KHÔNG bị thu hồi → ticket không bao giờ đi qua ReturnInTransit/ItemReceived.
        var status = ReadStatus(body);
        Assert.True(status is "Refunding" or "Completed",
            $"Mong đợi Refunding/Completed, nhận {status}");
    }

    // ===================== Phân quyền =====================

    [Fact(DisplayName = "RMA-12 [Abnormal] A customer cannot accept their own return request")]
    public async Task AcceptReturn_ByCustomer_IsForbidden()
    {
        var ticketId = await OpenTicketAsync();

        var response = await _fixture.ClientFor(TestRole.Customer).PostAsync($"/api/returns/{ticketId}/accept", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "RMA-13 [Abnormal] A store owner cannot decide the outcome of a return request")]
    public async Task ApproveRefund_ByStoreOwner_IsForbidden()
    {
        var ticketId = await OpenTicketAsync();

        var response = await _fixture.ClientFor(TestRole.GardenOwner)
            .PostAsJsonAsync($"/api/returns/{ticketId}/approve-refund", new { restock = false });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "RMA-14 [Normal] The customer can see their own request in the list")]
    public async Task OwnReturnRequest_AppearsInCustomerList()
    {
        var ticketId = await OpenTicketAsync();

        var response = await _fixture.ClientFor(TestRole.Customer).GetAsync("/api/returns/mine?page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(ticketId.ToString(), await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "RMA-15 [Normal] Platform staff can list pending return requests")]
    public async Task PendingReturnRequests_AreVisibleToStaff()
    {
        await OpenTicketAsync();

        var response = await _fixture.ClientFor(TestRole.Staff).GetAsync("/api/returns/pending?page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ===================== Tiện ích =====================

    /// <summary>Tạo một ticket RMA mới trên một đơn đã giao và trả về id của nó.</summary>
    private async Task<Guid> OpenTicketAsync()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);
        var response = await CreateReturnAsync(order);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Tạo yêu cầu thất bại: {(int)response.StatusCode} {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> CreateReturnAsync(DeliveredOrder order, int quantity = 1)
        => _fixture.ClientFor(TestRole.Customer).PostAsJsonAsync("/api/returns", new
        {
            deliveryId = order.DeliveryId,
            type = "Refund",
            reason = "PlantHealth",
            reasonDetail = "Cây đến nơi đã héo, lá rụng gần hết.",
            items = new[] { new { orderItemId = order.OrderItemId, quantity } },
            imageUrls = new[] { EvidenceImageUrl },
            bankAccountName = "KHACH KIEM THU",
            bankAccountNumber = "0123456789",
            bankName = "Vietcombank",
        });

    private static string ReadStatus(string body)
    {
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("data").GetProperty("status").GetString() ?? "";
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
    }

    private static async Task<string> Describe(HttpResponseMessage response)
        => $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}";
}
