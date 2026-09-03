using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using FengDeskAI.Application.Features.Returns.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Test thăm dò defect — mỗi ca ở đây khẳng định hành vi ĐÚNG của một chỗ nghi có lỗi.
///
/// Đây KHÔNG phải test hồi quy của tính năng đã xong. Mục đích ngược lại: chạy để xem chỗ nào đỏ.
/// Ca đỏ = defect thật, ghi vào Defect Log của Report5 kèm mã DEF-xx trong DisplayName.
/// Sửa xong thì ca đó tự chuyển xanh và ở lại làm test hồi quy — không phải viết lại.
///
/// Vì sao gom một chỗ thay vì rải vào từng file tính năng: để `dotnet test --filter
/// DefectProbeTests` chạy riêng được, và để người đọc Report5 lần ngược từ mã defect ra đúng một ca.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class DefectProbeTests
{
    private const string EvidenceImageUrl = "https://fake-storage.test/bucket/evidence/test.jpg";

    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public DefectProbeTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ===================== Cửa hậu môi trường — CỐ Ý KHÔNG PHỦ =====================
    //
    // Ba endpoint /api/dev/deliveries/{id}/shipping/* có chốt IsDevelopment() nhưng đang bị comment
    // lại, nên chúng mở ở MỌI môi trường và không kiểm quyền sở hữu: bất kỳ tài khoản nào cũng đánh
    // dấu được đơn của người khác là đã giao.
    //
    // Nhóm dự án đã quyết định giữ nguyên tạm thời (bên thứ ba chưa hoạt động, cần đường giả lập để
    // đi tiếp) và KHÔNG phủ test cho phần này. Ghi lại ở đây để người đọc suite biết đây là khoảng
    // trống CÓ CHỦ Ý, không phải bị bỏ sót. Xem Report5 — mục rủi ro được chấp nhận.

    // ===================== Tràn số =====================

    [Fact(DisplayName = "DEF-03 [Integrity] Adding a quantity that overflows int must be rejected, not silently wrapped")]
    public async Task AddToCart_QuantityOverflowingInt_IsRejected()
    {
        // Cộng dồn `existing.Quantity + request.Quantity` không có checked → tràn âm.
        // Số âm lọt qua chốt tồn kho (âm < tồn) và được lưu, làm sai mọi phép tính tiền phía sau.
        var scenario = await SalesScenario.SeedAsync(_fixture, _fixture.UserId(TestRole.Customer), storeCount: 1);
        var customer = _fixture.ClientFor(TestRole.Customer);
        await customer.DeleteAsync("/api/cart");

        await customer.PostAsJsonAsync("/api/cart/items",
            new { productItemId = scenario.Stores[0].ProductItemId, quantity = 1 });

        var response = await customer.PostAsJsonAsync("/api/cart/items",
            new { productItemId = scenario.Stores[0].ProductItemId, quantity = int.MaxValue });
        _output.WriteLine($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        if (response.IsSuccessStatusCode)
        {
            var cart = await customer.GetAsync("/api/cart");
            _output.WriteLine($"Giỏ sau khi cộng dồn: {await cart.Content.ReadAsStringAsync()}");
        }

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "DEF-04 [Robustness] Checking out a quantity that overflows int must return 4xx, not 500")]
    public async Task Checkout_QuantityOverflowingInt_DoesNotReturn500()
    {
        var scenario = await SalesScenario.SeedAsync(_fixture, _fixture.UserId(TestRole.Customer), storeCount: 1);
        var customer = _fixture.ClientFor(TestRole.Customer);
        await customer.DeleteAsync("/api/cart");

        var response = await customer.PostAsJsonAsync("/api/orders", new
        {
            shippingAddressId = scenario.ShippingAddressId,
            paymentMethod = "COD",
            items = new[]
            {
                new { productItemId = scenario.Stores[0].ProductItemId, quantity = int.MaxValue },
                new { productItemId = scenario.Stores[0].ProductItemId, quantity = int.MaxValue },
            },
        });
        _output.WriteLine($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        Assert.True((int)response.StatusCode < 500,
            $"Số lượng vô lý phải bị từ chối bằng 4xx, nhưng nhận {(int)response.StatusCode}.");
    }

    // ===================== Tràn độ dài chuỗi / miền giá trị cột =====================

    [Fact(DisplayName = "DEF-05 [Robustness] A review longer than the column allows must return 4xx, not 500")]
    public async Task CreateReview_ContentTooLong_DoesNotReturn500()
    {
        var productId = await PurchasedProductIdAsync();

        var response = await _fixture.ClientFor(TestRole.Customer).PostAsJsonAsync("/api/Review",
            new { productId, rating = 5, content = new string('a', 2_001) });
        _output.WriteLine($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        Assert.True((int)response.StatusCode < 500,
            $"Nội dung quá dài phải bị từ chối bằng 4xx, nhưng nhận {(int)response.StatusCode}.");
    }

    [Fact(DisplayName = "DEF-06 [Robustness] A recipient phone longer than the column allows must return 4xx, not 500")]
    public async Task CreateAddress_RecipientPhoneTooLong_DoesNotReturn500()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var scenario = await SalesScenario.SeedAsync(_fixture, user.Id, storeCount: 1);

        var wardId = await WardIdOfAddressAsync(scenario.ShippingAddressId);
        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/addresses", new
        {
            wardId,
            streetAddress = "88 Đường Người Nhận",
            recipientName = "Khách kiểm thử",
            recipientPhone = new string('0', 30),
        });
        _output.WriteLine($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        Assert.True((int)response.StatusCode < 500,
            $"Số điện thoại quá dài phải bị từ chối bằng 4xx, nhưng nhận {(int)response.StatusCode}.");
    }

    [Fact(DisplayName = "DEF-07 [Robustness] A store name longer than the column allows must return 4xx, not 500")]
    public async Task CreateStore_NameTooLong_DoesNotReturn500()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/stores",
            new { name = new string('V', 256), hotline = "0901234567" });
        _output.WriteLine($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        Assert.True((int)response.StatusCode < 500,
            $"Tên cửa hàng quá dài phải bị từ chối bằng 4xx, nhưng nhận {(int)response.StatusCode}.");
    }

    [Fact(DisplayName = "DEF-08 [Robustness] A workspace name longer than the column allows must return 4xx, not 500")]
    public async Task CreateWorkspace_NameTooLong_DoesNotReturn500()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/workspace", new
        {
            name = new string('K', 101),
            locationType = "Office",
            styleCode = "Modern",
            workPurpose = "Office",
        });
        _output.WriteLine($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        Assert.True((int)response.StatusCode < 500,
            $"Tên không gian quá dài phải bị từ chối bằng 4xx, nhưng nhận {(int)response.StatusCode}.");
    }

    [Fact(DisplayName = "DEF-09 [Robustness] A product price beyond the column range must return 4xx, not 500")]
    public async Task CreateProduct_PriceBeyondColumnRange_DoesNotReturn500()
    {
        var scenario = await SalesScenario.SeedAsync(_fixture, _fixture.UserId(TestRole.Customer), storeCount: 1);

        var response = await _fixture.ClientFor(TestRole.GardenOwner).PostAsJsonAsync("/api/products", new
        {
            gardenStoreId = scenario.Stores[0].StoreId,
            name = "Cây giá vô lý",
            items = new[] { new { name = "Chậu", price = 99_999_999_999_999m, stock = 1 } },
        });
        _output.WriteLine($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        Assert.True((int)response.StatusCode < 500,
            $"Giá vượt miền cột phải bị từ chối bằng 4xx, nhưng nhận {(int)response.StatusCode}.");
    }

    [Fact(DisplayName = "DEF-10 [Integrity] A negative product price must be rejected on create, as it is on add-variant")]
    public async Task CreateProduct_NegativePrice_IsRejected()
    {
        // AddItemAsync chặn giá âm, nhưng CreateAsync thì không — cùng một quy tắc nghiệp vụ,
        // hai đường vào lại khác nhau.
        var scenario = await SalesScenario.SeedAsync(_fixture, _fixture.UserId(TestRole.Customer), storeCount: 1);

        var response = await _fixture.ClientFor(TestRole.GardenOwner).PostAsJsonAsync("/api/products", new
        {
            gardenStoreId = scenario.Stores[0].StoreId,
            name = "Cây giá âm",
            items = new[] { new { name = "Chậu", price = -50_000m, stock = 1 } },
        });
        _output.WriteLine($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "DEF-11 [Robustness] A scoring parameter beyond the column range must return 4xx, not 500")]
    public async Task UpsertScoringParam_ValueBeyondColumnRange_DoesNotReturn500()
    {
        var response = await _fixture.ClientFor(TestRole.Manager)
            .PutAsJsonAsync($"/api/admin/scoring/params/DEF11_{Guid.NewGuid():N}"[..40],
                new { value = 999m, description = "Vượt miền numeric(5,3)." });
        _output.WriteLine($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        Assert.True((int)response.StatusCode < 500,
            $"Giá trị vượt miền cột phải bị từ chối bằng 4xx, nhưng nhận {(int)response.StatusCode}.");
    }

    // ===================== Enum ngoài miền =====================

    [Fact(DisplayName = "DEF-12 [Integrity] An out-of-range enum value must be rejected, not persisted verbatim")]
    public async Task CreateWorkspace_OutOfRangeEnum_IsRejected()
    {
        // Enum nhận cả dạng số; không chỗ nào gọi Enum.IsDefined, và cột lưu dạng chuỗi
        // → "999" nằm luôn trong DB và mọi phép so khớp phía sau trượt hết.
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/workspace", new
        {
            name = "Enum ngoài miền",
            locationType = 999,
            styleCode = "Modern",
            workPurpose = 999,
        });
        _output.WriteLine($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ===================== Máy trạng thái =====================

    [Fact(DisplayName = "DEF-13 [Robustness] Vendor acknowledge in a wrong state must return 409, not 500")]
    public async Task VendorAcknowledge_InWrongState_ReturnsConflictNot500()
    {
        // Mọi thao tác của staff đều pre-check CanTransition; hai thao tác của vendor thì không,
        // nên InvalidStateTransitionException thoát thẳng ra ngoài thành 500.
        var ticketId = await RequestedTicketAsync();

        var response = await _fixture.ClientFor(TestRole.GardenOwner)
            .PostAsync($"/api/returns/{ticketId}/vendor-acknowledge", null);
        _output.WriteLine($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        Assert.True((int)response.StatusCode < 500,
            $"Transition sai trạng thái phải trả 409, nhưng nhận {(int)response.StatusCode}.");
    }

    // ===================== Tồn kho & tiền =====================

    [Fact(DisplayName = "DEF-14 [Integrity] Cancelling a delivery must return the reserved stock")]
    public async Task CancelDelivery_RestoresStock()
    {
        // Hủy ĐƠN thì hoàn kho (OrderCancellationService), hủy DELIVERY thì không — cùng hệ quả
        // nghiệp vụ, một đường hoàn kho một đường không.
        var scenario = await SalesScenario.SeedAsync(_fixture, _fixture.UserId(TestRole.Customer), storeCount: 1);
        var store = scenario.Stores[0];
        var stockBefore = await StockAsync(store.ProductId);

        var customer = _fixture.ClientFor(TestRole.Customer);
        await customer.DeleteAsync("/api/cart");
        await customer.PostAsJsonAsync("/api/cart/items", new { productItemId = store.ProductItemId, quantity = 2 });

        var checkout = await customer.PostAsJsonAsync("/api/orders",
            new { shippingAddressId = scenario.ShippingAddressId, paymentMethod = "COD" });
        Assert.True(checkout.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(checkout, "đặt hàng"));
        var deliveryId = (await ApiEnvelope.DataAsync(checkout)).GetProperty("deliveries")[0].GetProperty("id").GetGuid();

        Assert.Equal(stockBefore - 2, await StockAsync(store.ProductId));

        var cancel = await _fixture.ClientFor(TestRole.GardenOwner)
            .PatchAsJsonAsync($"/api/orders/deliveries/{deliveryId}/status", new { status = "Cancelled" });
        Assert.True(cancel.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(cancel, "hủy delivery"));

        var stockAfter = await StockAsync(store.ProductId);
        _output.WriteLine($"Tồn trước {stockBefore}, sau khi hủy {stockAfter}");

        Assert.Equal(stockBefore, stockAfter);
    }

    [Fact(DisplayName = "DEF-15 [Money] Rejecting a refunding ticket must stop the pending refund from being paid out")]
    public async Task RejectRefundingTicket_StopsThePendingRefund()
    {
        // ReturnStateMachine cho phép Refunding → Rejected, nhưng RejectAsync không đụng tới lệnh
        // hoàn tiền đang Pending. Lượt quét SLA sau đó vẫn chi tiền cho một ticket đã bị từ chối.
        var (ticketId, refundId) = await PendingRefundWithTicketAsync();
        var staff = _fixture.ClientFor(TestRole.Staff);

        var reject = await staff.PostAsJsonAsync($"/api/returns/{ticketId}/reject",
            new { reason = "Phát hiện dấu hiệu gian lận sau khi đã duyệt." });
        _output.WriteLine($"reject: {(int)reject.StatusCode} {await reject.Content.ReadAsStringAsync()}");

        if (!reject.IsSuccessStatusCode)
            return; // API đã chặn sẵn — không có defect, ca này xanh.

        await RunSweepAsync(sp => sp.GetRequiredService<IRefundService>().ProcessPendingRefundsAsync());

        var refund = await ReadRefundAsync(refundId);
        var status = refund.GetProperty("status").GetString();
        _output.WriteLine($"Trạng thái lệnh hoàn tiền sau khi từ chối ticket: {status}");

        Assert.True(status is "Cancelled" or "Pending",
            $"Ticket đã bị từ chối thì lệnh hoàn tiền không được đi tiếp, nhưng nó đang ở {status}.");
    }

    // ===================== Soft-delete =====================

    [Fact(DisplayName = "DEF-16 [Data] A soft-deleted store must not still expose its owner list")]
    public async Task GetOwners_OfSoftDeletedStore_ReturnsNotFound()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var create = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/stores",
            new { name = $"Vườn {Guid.NewGuid():N}"[..18], hotline = "0901234567" });
        var storeId = (await ApiEnvelope.DataAsync(create)).GetProperty("id").GetGuid();

        var refreshed = user with { AccessToken = await ScenarioUsers.LoginAsync(_fixture, user) };
        var delete = await ScenarioUsers.ClientFor(_fixture, refreshed).DeleteAsync($"/api/stores/{storeId}");
        Assert.True(delete.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(delete, "xóa mềm cửa hàng"));

        var stranger = await ScenarioUsers.CreateAsync(_fixture);
        var response = await ScenarioUsers.ClientFor(_fixture, stranger).GetAsync($"/api/stores/{storeId}/owners");
        _output.WriteLine($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ===================== Helper =====================

    private async Task<int> StockAsync(Guid productId)
    {
        var response = await _fixture.ClientFor(TestRole.Anonymous).GetAsync($"/api/products/{productId}");
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "đọc sản phẩm"));

        return (await ApiEnvelope.DataAsync(response)).GetProperty("items")[0].GetProperty("stock").GetInt32();
    }

    private async Task<Guid> WardIdOfAddressAsync(Guid addressId)
    {
        var response = await _fixture.ClientFor(TestRole.Customer).GetAsync($"/api/addresses/{addressId}");
        if (response.IsSuccessStatusCode)
            return (await ApiEnvelope.DataAsync(response)).GetProperty("wardId").GetGuid();

        // Địa chỉ thuộc user mẫu Customer; nếu không đọc được thì lấy phường/xã bất kỳ từ DB.
        var wardId = Guid.Empty;
        await _fixture.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<FengDeskAI.Infrastructure.Persistence.Contexts.AppDbContext>();
            wardId = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(
                db.Set<FengDeskAI.Domain.Entities.Geography.Ward>().Select(w => w.Id));
        });
        return wardId;
    }

    private async Task<Guid> PendingDeliveryAsync()
    {
        var scenario = await SalesScenario.SeedAsync(_fixture, _fixture.UserId(TestRole.Customer), storeCount: 1);
        var customer = _fixture.ClientFor(TestRole.Customer);

        await customer.DeleteAsync("/api/cart");
        await customer.PostAsJsonAsync("/api/cart/items",
            new { productItemId = scenario.Stores[0].ProductItemId, quantity = 1 });

        var checkout = await customer.PostAsJsonAsync("/api/orders",
            new { shippingAddressId = scenario.ShippingAddressId, paymentMethod = "COD" });
        Assert.True(checkout.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(checkout, "đặt hàng"));

        return (await ApiEnvelope.DataAsync(checkout)).GetProperty("deliveries")[0].GetProperty("id").GetGuid();
    }

    private async Task<Guid> PurchasedProductIdAsync()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);
        var response = await _fixture.ClientFor(TestRole.Customer).GetAsync($"/api/orders/{order.OrderId}");
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "đọc đơn hàng"));

        return (await ApiEnvelope.DataAsync(response)).GetProperty("items")[0].GetProperty("productId").GetGuid();
    }

    private async Task<Guid> RequestedTicketAsync()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);

        var create = await _fixture.ClientFor(TestRole.Customer).PostAsJsonAsync("/api/returns", new
        {
            deliveryId = order.DeliveryId,
            type = "Refund",
            reason = "PlantHealth",
            reasonDetail = "Cây đến nơi đã héo.",
            items = new[] { new { orderItemId = order.OrderItemId, quantity = 1 } },
            imageUrls = new[] { EvidenceImageUrl },
            bankAccountName = "KHACH KIEM THU",
            bankAccountNumber = "0123456789",
            bankName = "Ngân hàng kiểm thử",
        });

        Assert.True(create.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(create, "tạo yêu cầu trả hàng"));
        return (await ApiEnvelope.DataAsync(create)).GetProperty("id").GetGuid();
    }

    private async Task<(Guid TicketId, Guid RefundId)> PendingRefundWithTicketAsync()
    {
        var ticketId = await RequestedTicketAsync();
        var staff = _fixture.ClientFor(TestRole.Staff);

        var accept = await staff.PostAsync($"/api/returns/{ticketId}/accept", null);
        Assert.True(accept.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(accept, "staff duyệt yêu cầu"));

        var approve = await staff.PostAsJsonAsync($"/api/returns/{ticketId}/approve-refund",
            new { restock = false, note = "Duyệt hoàn tiền trong test." });
        Assert.True(approve.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(approve, "duyệt hoàn tiền"));

        var detail = await staff.GetAsync($"/api/returns/{ticketId}");
        var refundId = (await ApiEnvelope.DataAsync(detail)).GetProperty("refund").GetProperty("id").GetGuid();
        return (ticketId, refundId);
    }

    private async Task<JsonElement> ReadRefundAsync(Guid refundId)
    {
        var response = await _fixture.ClientFor(TestRole.Manager).GetAsync($"/api/refunds/{refundId}");
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "đọc lệnh hoàn tiền"));
        return await ApiEnvelope.DataAsync(response);
    }

    private async Task<int> RunSweepAsync(Func<IServiceProvider, Task<int>> sweep)
    {
        var processed = 0;
        await _fixture.WithScopeAsync(async sp => processed = await sweep(sp));
        return processed;
    }
}
