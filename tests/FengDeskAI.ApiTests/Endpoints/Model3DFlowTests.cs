using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Đợt 5 — Hàng chờ model 3D: chủ vườn gửi yêu cầu → nhân viên nền tảng sinh model → xem trước →
/// chấp nhận hoặc từ chối, kèm máy trạng thái và ranh giới phân quyền.
///
/// <c>FakeModel3DGenerator</c> trả task "xong ngay" nên chuỗi generate → accept chạy trọn trong
/// một ca test, không phải chờ poll thật.
///
/// Máy trạng thái đang được khẳng định ở đây:
/// <code>
/// (tạo)          → AwaitingStaff
/// AwaitingStaff  → InProgress   (generate)
/// AwaitingStaff  → Rejected     (reject)
/// InProgress     → Succeeded    (accept)
/// InProgress     → InProgress   (retry)
/// Succeeded      → terminal — mọi thao tác của staff đều 409
/// </code>
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class Model3DFlowTests
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public Model3DFlowTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ===================== Chủ vườn gửi yêu cầu =====================

    [Fact(DisplayName = "M3D-01 [Normal] A store owner requests a 3D model and it lands in the staff queue")]
    public async Task RequestModel3D_AsOwner_IsQueuedForStaff()
    {
        var productId = await ProductWithImageAsync();

        var response = await RequestModelAsync(productId);
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)response.StatusCode} {body}");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal("AwaitingStaff", data.GetProperty("status").GetString());
        Assert.Equal("Initial", data.GetProperty("requestType").GetString());
    }

    [Fact(DisplayName = "M3D-02 [Abnormal] Requesting a 3D model for a product without images is rejected")]
    public async Task RequestModel3D_ProductWithoutImage_IsRejected()
    {
        var productId = await CreateProductAsync();

        var response = await RequestModelAsync(productId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ảnh", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "M3D-03 [Abnormal] A second open request for the same image returns 409")]
    public async Task RequestModel3D_Twice_ReturnsConflict()
    {
        var productId = await ProductWithImageAsync();
        var first = await RequestModelAsync(productId);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        var second = await RequestModelAsync(productId);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact(DisplayName = "M3D-04 [Abnormal] A customer cannot request a 3D model for another store's product")]
    public async Task RequestModel3D_AsCustomer_IsForbidden()
    {
        var productId = await ProductWithImageAsync();

        var response = await _fixture.ClientFor(TestRole.Customer)
            .PostAsync($"/api/products/{productId}/model-3d/requests", EmptyForm());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "M3D-05 [Normal] The owner sees their own request in the product request list")]
    public async Task ListRequests_AsOwner_ContainsOwnRequest()
    {
        var productId = await ProductWithImageAsync();
        var requestId = await RequestIdAsync(productId);

        var response = await Owner().GetAsync($"/api/products/{productId}/model-3d/requests");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Contains(data.EnumerateArray(), r => r.GetProperty("id").GetGuid() == requestId);
    }

    // ===================== Hàng chờ của nhân viên =====================

    [Fact(DisplayName = "M3D-06 [Normal] Platform staff sees the pending request in the queue")]
    public async Task Queue_AsStaff_ContainsPendingRequest()
    {
        var productId = await ProductWithImageAsync();
        var requestId = await RequestIdAsync(productId);

        var response = await Staff().GetAsync("/api/model3d-requests?status=AwaitingStaff&take=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Contains(data.GetProperty("items").EnumerateArray(),
            r => r.GetProperty("id").GetGuid() == requestId);
    }

    [Fact(DisplayName = "M3D-07 [Abnormal] A garden owner cannot open the platform-wide 3D queue")]
    public async Task Queue_AsGardenOwner_IsForbidden()
    {
        var response = await Owner().GetAsync("/api/model3d-requests");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ===================== Sinh model → xem trước → chấp nhận =====================

    [Fact(DisplayName = "M3D-08 [Normal] Staff generates the model and the request moves to InProgress")]
    public async Task Generate_FromAwaitingStaff_MovesToInProgress()
    {
        var requestId = await RequestIdAsync(await ProductWithImageAsync());

        var response = await GenerateAsync(requestId);
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)response.StatusCode} {body}");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal("InProgress", data.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("meshyTaskId").GetString()));
    }

    [Fact(DisplayName = "M3D-09 [Normal] The preview of an in-progress request reports the provider state")]
    public async Task Preview_WhileInProgress_ReturnsProviderState()
    {
        var requestId = await RequestIdAsync(await ProductWithImageAsync());
        await GenerateAsync(requestId);

        var response = await Staff().GetAsync($"/api/model3d-requests/{requestId}/preview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal("Succeeded", data.GetProperty("state").GetString());
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("glbUrl").GetString()));
    }

    [Fact(DisplayName = "M3D-10 [Abnormal] Previewing a request that was never generated returns 409")]
    public async Task Preview_BeforeGenerate_ReturnsConflict()
    {
        var requestId = await RequestIdAsync(await ProductWithImageAsync());

        var response = await Staff().GetAsync($"/api/model3d-requests/{requestId}/preview");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact(DisplayName = "M3D-11 [Normal] Accepting an in-progress request publishes the model on the product")]
    public async Task Accept_AfterGenerate_PublishesModel()
    {
        var productId = await ProductWithImageAsync();
        var requestId = await RequestIdAsync(productId);
        await GenerateAsync(requestId);

        var accept = await Staff().PostAsync($"/api/model3d-requests/{requestId}/accept", null);
        var body = await accept.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)accept.StatusCode} {body}");

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var accepted = await ApiEnvelope.DataAsync(accept);
        Assert.Equal("Succeeded", accepted.GetProperty("status").GetString());
        Assert.True(accepted.GetProperty("isEnabled").GetBoolean());

        // Model đã chấp nhận phải thấy được ở endpoint công khai của sản phẩm.
        var models = await _fixture.ClientFor(TestRole.Anonymous).GetAsync($"/api/products/{productId}/model-3d");
        var data = await ApiEnvelope.DataAsync(models);
        Assert.Contains(data.EnumerateArray(), m => m.GetProperty("status").GetString() == "Succeeded");
    }

    [Fact(DisplayName = "M3D-12 [Abnormal] Accepting a request that has not been generated returns 409")]
    public async Task Accept_BeforeGenerate_ReturnsConflict()
    {
        var requestId = await RequestIdAsync(await ProductWithImageAsync());

        var response = await Staff().PostAsync($"/api/model3d-requests/{requestId}/accept", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact(DisplayName = "M3D-13 [Abnormal] Accepting the same request twice returns 409")]
    public async Task Accept_Twice_ReturnsConflict()
    {
        var requestId = await RequestIdAsync(await ProductWithImageAsync());
        await GenerateAsync(requestId);
        var first = await Staff().PostAsync($"/api/model3d-requests/{requestId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await Staff().PostAsync($"/api/model3d-requests/{requestId}/accept", null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact(DisplayName = "M3D-14 [Normal] Retrying an in-progress request issues a new provider task")]
    public async Task Retry_WhileInProgress_IssuesNewTask()
    {
        var requestId = await RequestIdAsync(await ProductWithImageAsync());
        var generated = await GenerateAsync(requestId);
        var firstTaskId = (await ApiEnvelope.DataAsync(generated)).GetProperty("meshyTaskId").GetString();

        var retry = await Staff().PostAsync($"/api/model3d-requests/{requestId}/retry", EmptyForm());

        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
        var data = await ApiEnvelope.DataAsync(retry);
        Assert.Equal("InProgress", data.GetProperty("status").GetString());
        Assert.NotEqual(firstTaskId, data.GetProperty("meshyTaskId").GetString());
    }

    [Fact(DisplayName = "M3D-15 [Abnormal] Retrying a request still awaiting staff returns 409")]
    public async Task Retry_WhileAwaitingStaff_ReturnsConflict()
    {
        var requestId = await RequestIdAsync(await ProductWithImageAsync());

        var response = await Staff().PostAsync($"/api/model3d-requests/{requestId}/retry", EmptyForm());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ===================== Từ chối =====================

    [Fact(DisplayName = "M3D-16 [Normal] Staff rejects a pending request with a reason")]
    public async Task Reject_WithReason_Succeeds()
    {
        var requestId = await RequestIdAsync(await ProductWithImageAsync());

        var response = await Staff().PostAsJsonAsync($"/api/model3d-requests/{requestId}/reject",
            new { reason = "Ảnh nguồn quá mờ, không sinh được model." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "M3D-17 [Abnormal] Rejecting without a reason is refused")]
    public async Task Reject_WithoutReason_IsRejected()
    {
        var requestId = await RequestIdAsync(await ProductWithImageAsync());

        var response = await Staff().PostAsJsonAsync($"/api/model3d-requests/{requestId}/reject",
            new { reason = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "M3D-18 [Abnormal] A rejected request can no longer be generated")]
    public async Task Generate_AfterReject_ReturnsConflict()
    {
        var requestId = await RequestIdAsync(await ProductWithImageAsync());
        await Staff().PostAsJsonAsync($"/api/model3d-requests/{requestId}/reject", new { reason = "Không đạt." });

        var response = await GenerateAsync(requestId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact(DisplayName = "M3D-19 [Abnormal] Acting on an unknown request returns 404")]
    public async Task Generate_UnknownRequest_ReturnsNotFound()
    {
        var response = await GenerateAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ===================== Hiển thị model trên sản phẩm =====================

    [Fact(DisplayName = "M3D-20 [Normal] The owner hides an accepted model with the visibility toggle")]
    public async Task ToggleModel_AsOwner_HidesModel()
    {
        var productId = await ProductWithImageAsync();
        var requestId = await RequestIdAsync(productId);
        await GenerateAsync(requestId);
        var accept = await Staff().PostAsync($"/api/model3d-requests/{requestId}/accept", null);
        var modelId = (await ApiEnvelope.DataAsync(accept)).GetProperty("id").GetGuid();

        var toggle = await Owner().PatchAsJsonAsync(
            $"/api/products/{productId}/model-3d/{modelId}/toggle", new { isEnabled = false });

        Assert.Equal(HttpStatusCode.OK, toggle.StatusCode);

        var models = await _fixture.ClientFor(TestRole.Anonymous).GetAsync($"/api/products/{productId}/model-3d");
        var data = await ApiEnvelope.DataAsync(models);
        Assert.All(data.EnumerateArray(), m => Assert.False(m.GetProperty("isEnabled").GetBoolean()));
    }

    [Fact(DisplayName = "M3D-21 [Abnormal] An image that carries a 3D model cannot be deleted")]
    public async Task DeleteImage_WithModel3D_ReturnsConflict()
    {
        var productId = await ProductWithImageAsync();
        var imageId = await FirstImageIdAsync(productId);
        var requestId = await RequestIdAsync(productId);
        await GenerateAsync(requestId);
        await Staff().PostAsync($"/api/model3d-requests/{requestId}/accept", null);

        var response = await Owner().DeleteAsync($"/api/products/{productId}/images/{imageId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ===================== Helper =====================

    private HttpClient Owner() => _fixture.ClientFor(TestRole.GardenOwner);

    private HttpClient Staff() => _fixture.ClientFor(TestRole.Staff);

    /// <summary>
    /// Form multipart "không chọn ảnh nào" — hợp lệ về nghiệp vụ: service tự lấy ảnh có SortOrder
    /// nhỏ nhất của sản phẩm.
    ///
    /// Bẫy đã gặp: <c>new MultipartFormDataContent()</c> RỖNG sinh thân request không có section
    /// nào, ASP.NET Core từ chối ngay ở khâu đọc form với 400
    /// "Form section has invalid Content-Disposition value" — chưa vào tới action, nên test tưởng
    /// nghiệp vụ sai trong khi lỗi nằm ở chính request. Phải có ít nhất một section; trường dưới
    /// đây không khớp thuộc tính nào của <c>Model3DRequestFormModel</c> nên model binding bỏ qua.
    /// </summary>
    private static MultipartFormDataContent EmptyForm()
        => new() { { new StringContent(string.Empty), "unused" } };

    private Task<HttpResponseMessage> RequestModelAsync(Guid productId)
        => Owner().PostAsync($"/api/products/{productId}/model-3d/requests", EmptyForm());

    private Task<HttpResponseMessage> GenerateAsync(Guid requestId)
        => Staff().PostAsync($"/api/model3d-requests/{requestId}/generate", EmptyForm());

    private async Task<Guid> RequestIdAsync(Guid productId)
    {
        var response = await RequestModelAsync(productId);
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "gửi yêu cầu model 3D"));
        return (await ApiEnvelope.DataAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateProductAsync()
    {
        var scenario = await SalesScenario.SeedAsync(_fixture, _fixture.UserId(TestRole.Customer), storeCount: 1);

        var response = await Owner().PostAsJsonAsync("/api/products", new
        {
            gardenStoreId = scenario.Stores[0].StoreId,
            name = $"Sản phẩm 3D {Guid.NewGuid():N}"[..26],
            items = new[] { new { name = "Chậu tiêu chuẩn", price = 199_000m, stock = 5 } },
        });

        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "tạo sản phẩm"));
        return (await ApiEnvelope.DataAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> ProductWithImageAsync()
    {
        var productId = await CreateProductAsync();

        var image = await Owner().PostAsJsonAsync($"/api/products/{productId}/images/link",
            new { url = $"https://fake-storage.test/bucket/product/{Guid.NewGuid():N}.jpg", sortOrder = 0 });
        Assert.True(image.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(image, "gắn ảnh"));

        return productId;
    }

    private async Task<Guid> FirstImageIdAsync(Guid productId)
    {
        var response = await _fixture.ClientFor(TestRole.Anonymous).GetAsync($"/api/products/{productId}");
        var data = await ApiEnvelope.DataAsync(response);
        return data.GetProperty("images")[0].GetProperty("id").GetGuid();
    }
}
