using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Hồ sơ không gian làm việc — đầu vào của engine chấm điểm phong thủy: tạo, sửa, đặt mặc định,
/// xóa, phân tích ngũ hành, và đặt sản phẩm đã mua vào không gian.
///
/// Hai quy ước dễ nhầm, được khẳng định ở đây:
/// <list type="bullet">
/// <item>Hồ sơ của người khác trả <b>404 chứ không phải 403</b> — repository lọc luôn theo
///   <c>UserId</c> nên "không phải của bạn" và "không tồn tại" là một.</item>
/// <item>Hồ sơ ĐẦU TIÊN luôn thành mặc định, kể cả khi gửi <c>isDefault: false</c>.</item>
/// </list>
///
/// Không phủ ở đây: <c>POST /api/workspace/parse-description</c> (nhận việc rồi giao cho
/// <c>WorkspaceIntakeWorker</c>, mà worker đã bị gỡ trong test nên trạng thái đứng ở "pending" mãi)
/// và <c>POST /api/workspace/transcriptions</c> (<c>Speech__Enabled=false</c> nên luôn 503).
/// Hai phần đó cần unit test ở tầng service, không phủ được qua HTTP.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class WorkspaceFlowTests
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public WorkspaceFlowTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    // ===================== Vòng đời =====================

    [Fact(DisplayName = "WS-01 [Normal] A customer creates a workspace profile")]
    public async Task CreateWorkspace_AsCustomer_Succeeds()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/workspace", NewProfile());
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"{(int)response.StatusCode} {body}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal(user.Id, data.GetProperty("userId").GetGuid());
        Assert.Equal("Office", data.GetProperty("workPurpose").GetString());
    }

    [Fact(DisplayName = "WS-02 [Normal] The very first profile becomes the default even when not asked for")]
    public async Task CreateWorkspace_FirstOne_BecomesDefault()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user)
            .PostAsJsonAsync("/api/workspace", NewProfile(isDefault: false));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True((await ApiEnvelope.DataAsync(response)).GetProperty("isDefault").GetBoolean(),
            "Hồ sơ đầu tiên phải tự thành mặc định, nếu không khách không có không gian nào để gợi ý.");
    }

    [Fact(DisplayName = "WS-03 [Abnormal] A profile with a blank name is rejected")]
    public async Task CreateWorkspace_BlankName_IsRejected()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user)
            .PostAsJsonAsync("/api/workspace", NewProfile(name: "   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("tên", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "WS-04 [Boundary] A desk area of zero is rejected")]
    public async Task CreateWorkspace_ZeroDeskArea_IsRejected()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/workspace", new
        {
            name = "Bàn không diện tích",
            locationType = "Office",
            styleCode = "Modern",
            workPurpose = "Office",
            deskArea = 0,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("diện tích", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "WS-05 [Abnormal] An unknown style code is rejected")]
    public async Task CreateWorkspace_UnknownStyle_IsRejected()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user)
            .PostAsJsonAsync("/api/workspace", NewProfile(styleCode: "khong-co-phong-cach-nay"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("phong cách", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "WS-06 [Abnormal] An unknown workspace type is rejected")]
    public async Task CreateWorkspace_UnknownWorkspaceType_IsRejected()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/workspace", new
        {
            name = "Loại phòng lạ",
            locationType = "Office",
            styleCode = "Modern",
            workPurpose = "Office",
            workspaceTypeId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "WS-07 [Normal] Updating a profile persists the change")]
    public async Task UpdateWorkspace_AsOwner_PersistsChange()
    {
        var (user, profileId) = await UserWithProfileAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, user).PutAsJsonAsync($"/api/workspace/{profileId}", new
        {
            name = "Góc làm việc đã đổi",
            locationType = "Home",
            styleCode = "Minimal",
            workPurpose = "Study",
            lighting = "Natural",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal("Góc làm việc đã đổi", data.GetProperty("name").GetString());
        Assert.Equal("Study", data.GetProperty("workPurpose").GetString());
        Assert.Equal("Natural", data.GetProperty("lighting").GetString());
    }

    [Fact(DisplayName = "WS-08 [Abnormal] Another user's profile is invisible — 404, not 403")]
    public async Task GetWorkspace_OfAnotherUser_ReturnsNotFound()
    {
        var (_, profileId) = await UserWithProfileAsync();
        var stranger = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, stranger).GetAsync($"/api/workspace/{profileId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "WS-09 [Abnormal] Another user cannot update a profile they do not own")]
    public async Task UpdateWorkspace_OfAnotherUser_ReturnsNotFound()
    {
        var (_, profileId) = await UserWithProfileAsync();
        var stranger = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, stranger)
            .PutAsJsonAsync($"/api/workspace/{profileId}", new
            {
                name = "Chiếm không gian",
                locationType = "Office",
                styleCode = "Modern",
                workPurpose = "Office",
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "WS-10 [Normal] Setting a second profile as default unsets the first")]
    public async Task SetDefault_SecondProfile_UnsetsTheFirst()
    {
        var (user, firstId) = await UserWithProfileAsync();
        var client = ScenarioUsers.ClientFor(_fixture, user);

        var second = await client.PostAsJsonAsync("/api/workspace", NewProfile(name: "Không gian thứ hai"));
        var secondId = (await ApiEnvelope.DataAsync(second)).GetProperty("id").GetGuid();

        var setDefault = await client.PatchAsync($"/api/workspace/{secondId}/set-default", null);
        Assert.Equal(HttpStatusCode.OK, setDefault.StatusCode);

        var list = await client.GetAsync("/api/workspace");
        var profiles = (await ApiEnvelope.DataAsync(list)).EnumerateArray().ToList();

        Assert.True(profiles.Single(p => p.GetProperty("id").GetGuid() == secondId).GetProperty("isDefault").GetBoolean());
        Assert.False(profiles.Single(p => p.GetProperty("id").GetGuid() == firstId).GetProperty("isDefault").GetBoolean());
    }

    [Fact(DisplayName = "WS-11 [Normal] The default endpoint returns the profile marked default")]
    public async Task GetDefault_AfterCreate_ReturnsThatProfile()
    {
        var (user, profileId) = await UserWithProfileAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, user).GetAsync("/api/workspace/default");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(profileId, (await ApiEnvelope.DataAsync(response)).GetProperty("id").GetGuid());
    }

    [Fact(DisplayName = "WS-12 [Abnormal] A user with no profile has no default")]
    public async Task GetDefault_WithoutAnyProfile_ReturnsNotFound()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user).GetAsync("/api/workspace/default");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "WS-13 [Normal] A deleted profile disappears from the list")]
    public async Task DeleteWorkspace_AsOwner_RemovesFromList()
    {
        var (user, profileId) = await UserWithProfileAsync();
        var client = ScenarioUsers.ClientFor(_fixture, user);

        var delete = await client.DeleteAsync($"/api/workspace/{profileId}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var list = await client.GetAsync("/api/workspace");
        Assert.DoesNotContain((await ApiEnvelope.DataAsync(list)).EnumerateArray(),
            p => p.GetProperty("id").GetGuid() == profileId);
    }

    // ===================== Độ đầy đủ & input ngũ hành =====================

    [Fact(DisplayName = "WS-14 [Normal] Filling more fields raises the completeness percentage")]
    public async Task Completeness_MoreFieldsFilled_IsHigher()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var client = ScenarioUsers.ClientFor(_fixture, user);

        var sparse = await client.PostAsJsonAsync("/api/workspace", NewProfile(name: "Sơ sài"));
        var sparsePercent = (await ApiEnvelope.DataAsync(sparse)).GetProperty("completenessPercent").GetInt32();

        var rich = await client.PostAsJsonAsync("/api/workspace", new
        {
            name = "Đầy đủ",
            locationType = "Office",
            styleCode = "Modern",
            workPurpose = "Office",
            lighting = "Natural",
            deskType = "Sitting",
            deskOrientation = "East",
            roomFacingDirection = "South",
            deskArea = 120,
            inputs = new[] { new { inputKind = "Color", inputCode = "Green" } },
        });
        var richPercent = (await ApiEnvelope.DataAsync(rich)).GetProperty("completenessPercent").GetInt32();

        Assert.True(richPercent > sparsePercent,
            $"Hồ sơ đầy đủ ({richPercent}%) phải cao hơn hồ sơ sơ sài ({sparsePercent}%).");
    }

    [Fact(DisplayName = "WS-15 [Normal] Valid element inputs are stored on the profile")]
    public async Task CreateWorkspace_WithElementInputs_StoresThem()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/workspace", new
        {
            name = "Có input ngũ hành",
            locationType = "Office",
            styleCode = "Modern",
            workPurpose = "Office",
            inputs = new[]
            {
                new { inputKind = "Color", inputCode = "Green" },
                new { inputKind = "Material", inputCode = "Wood" },
            },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var inputs = (await ApiEnvelope.DataAsync(response)).GetProperty("inputs").EnumerateArray().ToList();
        Assert.Equal(2, inputs.Count);
    }

    [Fact(DisplayName = "WS-16 [Abnormal] Unknown element input codes are dropped silently, not rejected")]
    public async Task CreateWorkspace_UnknownElementInput_IsDroppedNotRejected()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/workspace", new
        {
            name = "Input lạ",
            locationType = "Office",
            styleCode = "Modern",
            workPurpose = "Office",
            inputs = new[] { new { inputKind = "Color", inputCode = "MauKhongTonTai" } },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty((await ApiEnvelope.DataAsync(response)).GetProperty("inputs").EnumerateArray());
    }

    [Fact(DisplayName = "WS-17 [Normal] The element-input vocabulary lists the seeded colours and materials")]
    public async Task GetElementInputVocabulary_AfterSeeding_IsNotEmpty()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user).GetAsync("/api/workspace/element-inputs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.NotEmpty(data.GetProperty("colors").EnumerateArray());
        Assert.NotEmpty(data.GetProperty("materials").EnumerateArray());
    }

    // ===================== Phân tích ngũ hành =====================

    [Fact(DisplayName = "WS-18 [Normal] The element analysis reports one row per element, sorted by gap")]
    public async Task ElementAnalysis_ForOwnedProfile_ReturnsSortedRows()
    {
        var (user, profileId) = await UserWithProfileAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, user)
            .GetAsync($"/api/workspace/{profileId}/element-analysis");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);

        var gaps = data.GetProperty("elements").EnumerateArray()
            .Select(e => e.GetProperty("gap").GetDecimal()).ToList();
        Assert.Equal(5, gaps.Count);
        Assert.Equal(gaps.OrderByDescending(g => g), gaps);
    }

    [Fact(DisplayName = "WS-19 [Normal] The element analysis carries a compatibility percentage and insights")]
    public async Task ElementAnalysis_ForOwnedProfile_CarriesInsights()
    {
        var (user, profileId) = await UserWithProfileAsync();

        var response = await ScenarioUsers.ClientFor(_fixture, user)
            .GetAsync($"/api/workspace/{profileId}/element-analysis");
        var data = await ApiEnvelope.DataAsync(response);

        Assert.InRange(data.GetProperty("compatibilityPercent").GetInt32(), 0, 100);
        Assert.NotEmpty(data.GetProperty("insights").GetProperty("lines").EnumerateArray());
    }

    [Fact(DisplayName = "WS-20 [Abnormal] The element analysis of another user's profile returns 404")]
    public async Task ElementAnalysis_OfAnotherUser_ReturnsNotFound()
    {
        var (_, profileId) = await UserWithProfileAsync();
        var stranger = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, stranger)
            .GetAsync($"/api/workspace/{profileId}/element-analysis");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ===================== Đặt sản phẩm đã mua vào không gian =====================

    [Fact(DisplayName = "WS-21 [Normal] A delivered purchase with feng-shui data is counted in the workspace analysis")]
    public async Task PlaceProduct_DeliveredPurchaseWithFengShui_IsCountedInAnalysis()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);
        await SetFengShuiAsync(await ProductIdOfAsync(order.OrderId));

        var profileId = await ProfileForFixtureCustomerAsync();
        var customer = _fixture.ClientFor(TestRole.Customer);

        var response = await customer.PutAsJsonAsync($"/api/workspace/{profileId}/placements",
            new { orderItemId = order.OrderItemId });
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "đặt sản phẩm vào không gian"));

        var analysis = await customer.GetAsync($"/api/workspace/{profileId}/element-analysis");
        var placed = (await ApiEnvelope.DataAsync(analysis)).GetProperty("placedProducts");
        Assert.Contains(placed.EnumerateArray(), p => p.GetProperty("orderItemId").GetGuid() == order.OrderItemId);
    }

    [Fact(DisplayName = "WS-21b [Abnormal] A placed product with no feng-shui data does not skew the analysis")]
    public async Task PlaceProduct_WithoutFengShui_IsNotCountedInAnalysis()
    {
        // Đặt được, nhưng KHÔNG được tính vào radar: sản phẩm chưa gắn thuộc tính phong thủy có
        // vector rỗng, tính vào thì kéo lệch kết quả bằng dữ liệu không có thật.
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);
        var profileId = await ProfileForFixtureCustomerAsync();
        var customer = _fixture.ClientFor(TestRole.Customer);

        var response = await customer.PutAsJsonAsync($"/api/workspace/{profileId}/placements",
            new { orderItemId = order.OrderItemId });
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "đặt sản phẩm vào không gian"));

        var analysis = await customer.GetAsync($"/api/workspace/{profileId}/element-analysis");
        var placed = (await ApiEnvelope.DataAsync(analysis)).GetProperty("placedProducts");
        Assert.DoesNotContain(placed.EnumerateArray(), p => p.GetProperty("orderItemId").GetGuid() == order.OrderItemId);
    }

    [Fact(DisplayName = "WS-22 [Abnormal] An item the user never bought cannot be placed")]
    public async Task PlaceProduct_NotPurchased_IsRejected()
    {
        var profileId = await ProfileForFixtureCustomerAsync();

        var response = await _fixture.ClientFor(TestRole.Customer)
            .PutAsJsonAsync($"/api/workspace/{profileId}/placements", new { orderItemId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("lịch sử mua", await ApiEnvelope.MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "WS-23 [Normal] A placed product can be removed from the workspace")]
    public async Task RemovePlacement_AfterPlacing_Succeeds()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);
        var profileId = await ProfileForFixtureCustomerAsync();
        var customer = _fixture.ClientFor(TestRole.Customer);
        await customer.PutAsJsonAsync($"/api/workspace/{profileId}/placements", new { orderItemId = order.OrderItemId });

        var response = await customer.DeleteAsync($"/api/workspace/{profileId}/placements/{order.OrderItemId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "WS-24 [Abnormal] Removing a product that was never placed returns 404")]
    public async Task RemovePlacement_NeverPlaced_ReturnsNotFound()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);
        var profileId = await ProfileForFixtureCustomerAsync();

        var response = await _fixture.ClientFor(TestRole.Customer)
            .DeleteAsync($"/api/workspace/{profileId}/placements/{order.OrderItemId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "WS-25 [Normal] A delivered purchase shows up in the purchasable placement list")]
    public async Task PurchasableItems_AfterDelivery_ContainsItem()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);

        var response = await _fixture.ClientFor(TestRole.Customer).GetAsync("/api/workspace/placements/purchasable");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains((await ApiEnvelope.DataAsync(response)).EnumerateArray(),
            p => p.GetProperty("orderItemId").GetGuid() == order.OrderItemId);
    }

    // ===================== Loại không gian =====================

    [Fact(DisplayName = "WS-26 [Normal] The seeded workspace types are listed")]
    public async Task ListWorkspaceTypes_AfterSeeding_IsNotEmpty()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user).GetAsync("/api/workspace-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty((await ApiEnvelope.DataAsync(response)).EnumerateArray());
    }

    [Fact(DisplayName = "WS-27 [Normal] A customer creates their own workspace type")]
    public async Task CreateWorkspaceType_AsCustomer_Succeeds()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/workspace-types",
            new { name = $"Phòng {Guid.NewGuid():N}"[..18], isPublic = false });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False((await ApiEnvelope.DataAsync(response)).GetProperty("isSystemSeeded").GetBoolean());
    }

    [Fact(DisplayName = "WS-28 [Boundary] A personal weight above one is clamped, not rejected")]
    public async Task CreateWorkspaceType_WeightAboveOne_IsClamped()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/workspace-types",
            new { name = $"Phòng {Guid.NewGuid():N}"[..18], personalWeight = 5.0m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1.0m, (await ApiEnvelope.DataAsync(response)).GetProperty("personalWeight").GetDecimal());
    }

    [Fact(DisplayName = "WS-29 [Abnormal] A workspace type with a blank name is rejected")]
    public async Task CreateWorkspaceType_BlankName_IsRejected()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);

        var response = await ScenarioUsers.ClientFor(_fixture, user)
            .PostAsJsonAsync("/api/workspace-types", new { name = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ===================== Helper =====================

    private static object NewProfile(string? name = null, string styleCode = "Modern", bool isDefault = false) => new
    {
        name = name ?? $"Góc làm việc {Guid.NewGuid():N}"[..26],
        locationType = "Office",
        styleCode,
        workPurpose = "Office",
        isDefault,
    };

    private async Task<(ThrowawayUser User, Guid ProfileId)> UserWithProfileAsync()
    {
        var user = await ScenarioUsers.CreateAsync(_fixture);
        var response = await ScenarioUsers.ClientFor(_fixture, user).PostAsJsonAsync("/api/workspace", NewProfile());
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "tạo hồ sơ không gian"));

        return (user, (await ApiEnvelope.DataAsync(response)).GetProperty("id").GetGuid());
    }

    /// <summary>
    /// Hồ sơ thuộc về user mẫu Customer — bắt buộc với các ca "đặt sản phẩm đã mua", vì lịch sử mua
    /// gắn với chính user đó chứ không phải user dùng một lần.
    /// </summary>
    private async Task<Guid> ProfileForFixtureCustomerAsync()
    {
        var response = await _fixture.ClientFor(TestRole.Customer).PostAsJsonAsync("/api/workspace", NewProfile());
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "tạo hồ sơ cho khách mẫu"));

        return (await ApiEnvelope.DataAsync(response)).GetProperty("id").GetGuid();
    }
    /// <summary>Id sản phẩm gốc của dòng đầu tiên trong đơn.</summary>
    private async Task<Guid> ProductIdOfAsync(Guid orderId)
    {
        var response = await _fixture.ClientFor(TestRole.Customer).GetAsync($"/api/orders/{orderId}");
        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "đọc đơn hàng"));

        return (await ApiEnvelope.DataAsync(response)).GetProperty("items")[0].GetProperty("productId").GetGuid();
    }

    /// <summary>
    /// Gắn thuộc tính phong thủy cho sản phẩm. Bắt buộc với ca "được tính vào radar": phân tích ngũ
    /// hành bỏ qua mọi sản phẩm có vector rỗng, nên sản phẩm do SalesScenario dựng (trần trụi, không
    /// có hành nào) sẽ không xuất hiện trong placedProducts dù đặt thành công.
    /// </summary>
    private async Task SetFengShuiAsync(Guid productId)
    {
        var response = await _fixture.ClientFor(TestRole.GardenOwner)
            .PutAsJsonAsync($"/api/products/{productId}/feng-shui", new { primaryElement = "Moc" });

        Assert.True(response.IsSuccessStatusCode, await ApiEnvelope.DescribeAsync(response, "gắn thuộc tính phong thủy"));
    }
}
