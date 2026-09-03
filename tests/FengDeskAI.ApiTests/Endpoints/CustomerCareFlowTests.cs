using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Gợi ý sản phẩm và đánh giá — hai mặt tiếp xúc chính của khách sau khi đã có không gian và đơn hàng.
///
/// Về gợi ý: lớp diễn giải AI đã được thay bằng <c>MockAiRecommendationClient</c>
/// (<c>AiRecommendationSettings__UseMock=true</c>), và mock **giữ nguyên thứ hạng** do engine chấm.
/// Nghĩa là phần đang kiểm ở đây là **engine deterministic**, không phải chất lượng câu chữ của AI —
/// đúng ranh giới đã chốt trong kiến trúc: AI chỉ giải thích, không xếp hạng.
///
/// Về đánh giá: "đã mua" tính theo trạng thái ĐƠN (Paid/Processing/Shipping/Completed), không phụ
/// thuộc đã giao hay chưa.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class CustomerCareFlowTests
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public CustomerCareFlowTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ===================== Gợi ý theo không gian =====================

    [Fact(DisplayName = "REC-01 [Normal] A workspace profile yields a ranked recommendation set")]
    public async Task Recommend_ForWorkspace_ReturnsRankedItems()
    {
        var profileId = await ProfileAsync();

        var response = await Customer().PostAsJsonAsync("/api/recommendations",
            new { workspaceProfileId = profileId, topN = 5 });
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)response.StatusCode} {body[..Math.Min(400, body.Length)]}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal("Workspace", data.GetProperty("kind").GetString());

        var ranks = data.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("rank").GetInt32()).ToList();
        Assert.NotEmpty(ranks);
        Assert.Equal(ranks.OrderBy(r => r), ranks);
    }

    [Fact(DisplayName = "REC-02 [Boundary] topN caps the number of returned items")]
    public async Task Recommend_TopN_LimitsItemCount()
    {
        var profileId = await ProfileAsync();

        var response = await Customer().PostAsJsonAsync("/api/recommendations",
            new { workspaceProfileId = profileId, topN = 2 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True((await ApiEnvelope.DataAsync(response)).GetProperty("items").GetArrayLength() <= 2);
    }

    [Fact(DisplayName = "REC-03 [Boundary] A topN above the cap is clamped instead of rejected")]
    public async Task Recommend_TopNAboveCap_IsClamped()
    {
        var profileId = await ProfileAsync();

        var response = await Customer().PostAsJsonAsync("/api/recommendations",
            new { workspaceProfileId = profileId, topN = 9999 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True((await ApiEnvelope.DataAsync(response)).GetProperty("items").GetArrayLength() <= 20,
            "topN phải bị kẹp ở 20, không được truyền thẳng xuống truy vấn.");
    }

    [Fact(DisplayName = "REC-04 [Normal] Every recommended item carries a rationale for the customer")]
    public async Task Recommend_Items_CarryExplanation()
    {
        var profileId = await ProfileAsync();

        var response = await Customer().PostAsJsonAsync("/api/recommendations",
            new { workspaceProfileId = profileId, topN = 3 });
        var items = (await ApiEnvelope.DataAsync(response)).GetProperty("items").EnumerateArray().ToList();

        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.False(string.IsNullOrWhiteSpace(i.GetProperty("explanation").GetString())));
    }

    [Fact(DisplayName = "REC-05 [Normal] A recommendation session can be read back by its id")]
    public async Task GetRecommendation_ById_ReturnsSameSession()
    {
        var profileId = await ProfileAsync();
        var created = await Customer().PostAsJsonAsync("/api/recommendations",
            new { workspaceProfileId = profileId, topN = 3 });
        var id = (await ApiEnvelope.DataAsync(created)).GetProperty("id").GetGuid();

        var response = await Customer().GetAsync($"/api/recommendations/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(id, (await ApiEnvelope.DataAsync(response)).GetProperty("id").GetGuid());
    }

    [Fact(DisplayName = "REC-06 [Abnormal] Another customer cannot read someone else's recommendation session")]
    public async Task GetRecommendation_OfAnotherUser_ReturnsNotFound()
    {
        var profileId = await ProfileAsync();
        var created = await Customer().PostAsJsonAsync("/api/recommendations",
            new { workspaceProfileId = profileId, topN = 3 });
        var id = (await ApiEnvelope.DataAsync(created)).GetProperty("id").GetGuid();

        var stranger = await ScenarioUsers.CreateAsync(_fixture);
        var response = await ScenarioUsers.ClientFor(_fixture, stranger).GetAsync($"/api/recommendations/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "REC-07 [Abnormal] Recommending for an unknown workspace returns 404")]
    public async Task Recommend_UnknownWorkspace_ReturnsNotFound()
    {
        var response = await Customer().PostAsJsonAsync("/api/recommendations",
            new { workspaceProfileId = Guid.NewGuid(), topN = 5 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "REC-08 [Abnormal] Carry-item recommendations need the customer's birth date")]
    public async Task RecommendPersonal_WithoutBirthDate_IsRejected()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user)
            .PostAsJsonAsync("/api/recommendations/personal", new { topN = 5 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("ngày sinh", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "REC-09 [Normal] The product-fit endpoint scores one product against one workspace")]
    public async Task ProductFit_ForOwnedWorkspace_ReturnsScore()
    {
        var profileId = await ProfileAsync();
        var recommended = await Customer().PostAsJsonAsync("/api/recommendations",
            new { workspaceProfileId = profileId, topN = 1 });
        var items = (await ApiEnvelope.DataAsync(recommended)).GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        var productId = items[0].GetProperty("productId").GetGuid();

        var response = await Customer()
            .GetAsync($"/api/recommendations/fit?productId={productId}&workspaceProfileId={profileId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal(productId, data.GetProperty("productId").GetGuid());
        Assert.Equal(5, data.GetProperty("gap").GetArrayLength());
    }

    [Fact(DisplayName = "REC-10 [Abnormal] The product-fit endpoint rejects a product with no feng-shui attributes")]
    public async Task ProductFit_ProductWithoutFengShui_ReturnsNotFound()
    {
        var profileId = await ProfileAsync();

        var response = await Customer()
            .GetAsync($"/api/recommendations/fit?productId={Guid.NewGuid()}&workspaceProfileId={profileId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ===================== Đánh giá sản phẩm =====================

    [Fact(DisplayName = "REV-01 [Normal] A customer reviews a product they bought")]
    public async Task CreateReview_AfterPurchase_Succeeds()
    {
        var productId = await PurchasedProductIdAsync();

        var response = await Customer().PostAsJsonAsync("/api/Review",
            new { productId, content = "Cây khỏe, chậu đẹp.", rating = 5 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal(5, data.GetProperty("rating").GetInt32());
        Assert.Equal(productId, data.GetProperty("productId").GetGuid());
    }

    [Fact(DisplayName = "REV-02 [Abnormal] A customer cannot review a product they never bought")]
    public async Task CreateReview_WithoutPurchase_IsForbidden()
    {
        var productId = await UnpurchasedProductIdAsync();

        var response = await Customer().PostAsJsonAsync("/api/Review",
            new { productId, content = "Chưa mua mà vẫn đánh giá.", rating = 5 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("chưa mua", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "REV-03 [Abnormal] Reviewing the same product twice returns 409")]
    public async Task CreateReview_Twice_ReturnsConflict()
    {
        var productId = await PurchasedProductIdAsync();
        var first = await Customer().PostAsJsonAsync("/api/Review",
            new { productId, content = "Lần một.", rating = 4 });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await Customer().PostAsJsonAsync("/api/Review",
            new { productId, content = "Lần hai.", rating = 3 });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact(DisplayName = "REV-04 [Boundary] A rating of zero is rejected")]
    public async Task CreateReview_RatingZero_IsRejected()
    {
        var productId = await PurchasedProductIdAsync();

        var response = await Customer().PostAsJsonAsync("/api/Review",
            new { productId, content = "Điểm 0.", rating = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "REV-05 [Boundary] A rating above five is rejected")]
    public async Task CreateReview_RatingAboveFive_IsRejected()
    {
        var productId = await PurchasedProductIdAsync();

        var response = await Customer().PostAsJsonAsync("/api/Review",
            new { productId, content = "Điểm 6.", rating = 6 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "REV-06 [Abnormal] A review with no content is rejected")]
    public async Task CreateReview_BlankContent_IsRejected()
    {
        var productId = await PurchasedProductIdAsync();

        var response = await Customer().PostAsJsonAsync("/api/Review",
            new { productId, content = "   ", rating = 5 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "REV-07 [Abnormal] Reviewing an unknown product returns 404")]
    public async Task CreateReview_UnknownProduct_ReturnsNotFound()
    {
        var response = await Customer().PostAsJsonAsync("/api/Review",
            new { productId = Guid.NewGuid(), content = "Sản phẩm không có.", rating = 5 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "REV-08 [Normal] The author updates their own review")]
    public async Task UpdateReview_AsAuthor_Succeeds()
    {
        var reviewId = await ReviewIdAsync();

        var response = await Customer().PutAsJsonAsync($"/api/Review/{reviewId}",
            new { content = "Đã dùng thêm một tuần, vẫn tốt.", rating = 4 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4, (await ApiEnvelope.DataAsync(response)).GetProperty("rating").GetInt32());
    }

    [Fact(DisplayName = "REV-09 [Abnormal] Someone else cannot edit another customer's review")]
    public async Task UpdateReview_AsAnotherUser_IsForbidden()
    {
        var reviewId = await ReviewIdAsync();
        var stranger = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, stranger)
            .PutAsJsonAsync($"/api/Review/{reviewId}", new { content = "Sửa trộm.", rating = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "REV-10 [Normal] The author deletes their own review and can review again")]
    public async Task DeleteReview_AsAuthor_AllowsReviewingAgain()
    {
        var productId = await PurchasedProductIdAsync();
        var created = await Customer().PostAsJsonAsync("/api/Review",
            new { productId, content = "Sẽ xóa.", rating = 3 });
        var reviewId = (await ApiEnvelope.DataAsync(created)).GetProperty("id").GetGuid();

        var delete = await Customer().DeleteAsync($"/api/Review/{reviewId}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var again = await Customer().PostAsJsonAsync("/api/Review",
            new { productId, content = "Đánh giá lại.", rating = 5 });
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact(DisplayName = "REV-11 [Normal] A review appears in the author's own review list")]
    public async Task ListMyReviews_ContainsOwnReview()
    {
        var reviewId = await ReviewIdAsync();

        var response = await Customer().GetAsync("/api/Review/my");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains((await ApiEnvelope.DataAsync(response)).EnumerateArray(),
            r => r.GetProperty("id").GetGuid() == reviewId);
    }

    [Fact(DisplayName = "REV-12 [Normal] Reviews are readable without signing in")]
    public async Task ListReviews_Anonymously_Succeeds()
    {
        await ReviewIdAsync();

        var response = await _fixture.ClientFor(TestRole.Anonymous).GetAsync("/api/Review");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ===================== Helper =====================

    private HttpClient Customer() => _fixture.ClientFor(TestRole.Customer);

    /// <summary>Hồ sơ không gian của khách mẫu — mọi ca gợi ý đều cần một cái.</summary>
    private async Task<Guid> ProfileAsync()
    {
        var response = await Customer().PostAsJsonAsync("/api/workspace", new
        {
            name = $"Không gian gợi ý {Guid.NewGuid():N}"[..28],
            locationType = "Office",
            styleCode = "Modern",
            workPurpose = "Office",
            lighting = "Natural",
        });

        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "tạo hồ sơ không gian"));
        return (await ApiEnvelope.DataAsync(response)).GetProperty("id").GetGuid();
    }

    /// <summary>Sản phẩm khách mẫu đã mua — điều kiện tiên quyết để đánh giá.</summary>
    private async Task<Guid> PurchasedProductIdAsync()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);

        var response = await Customer().GetAsync($"/api/orders/{order.OrderId}");
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "đọc đơn hàng"));

        return (await ApiEnvelope.DataAsync(response)).GetProperty("items")[0].GetProperty("productId").GetGuid();
    }

    /// <summary>Sản phẩm vừa dựng nhưng chưa ai mua — dùng cho ca "chưa mua thì không được đánh giá".</summary>
    private async Task<Guid> UnpurchasedProductIdAsync()
    {
        var scenario = await SalesScenario.SeedAsync(_fixture, _fixture.UserId(TestRole.Customer), storeCount: 1);
        return scenario.Stores[0].ProductId;
    }

    private async Task<Guid> ReviewIdAsync()
    {
        var productId = await PurchasedProductIdAsync();
        var response = await Customer().PostAsJsonAsync("/api/Review",
            new { productId, content = "Đánh giá dựng cho ca test.", rating = 5 });

        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "tạo đánh giá"));
        return (await ApiEnvelope.DataAsync(response)).GetProperty("id").GetGuid();
    }
}
