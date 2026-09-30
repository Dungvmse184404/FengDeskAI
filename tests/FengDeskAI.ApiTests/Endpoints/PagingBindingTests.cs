using System.Net;
using FengDeskAI.ApiTests.Infrastructure;
using Xunit;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Hồi quy lỗi binding: action nhận <c>[FromQuery] PageRequest page</c> mà query string cũng có key <c>page</c> →
/// model binder lấy tên tham số làm tiền tố, đi tìm <c>page.PageSize</c>, không thấy nên <c>pageSize</c> bị bỏ qua và
/// luôn về mặc định 20. Tên tham số action KHÔNG được trùng key query nào của chính kiểu đó.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class PagingBindingTests
{
    private readonly ApiTestFixture _fixture;

    public PagingBindingTests(ApiTestFixture fixture) => _fixture = fixture;

    [Theory(DisplayName = "PAGE-01 [Boundary] pageSize from the query string is honoured when page is also sent")]
    [InlineData(TestRole.Customer, "/api/orders")]
    [InlineData(TestRole.Customer, "/api/notifications")]
    [InlineData(TestRole.Customer, "/api/returns/mine")]
    [InlineData(TestRole.Admin, "/api/orders/all")]
    public async Task PagedEndpoint_PageAndPageSize_BindsPageSize(TestRole role, string path)
    {
        var response = await _fixture.ClientFor(role).GetAsync($"{path}?page=1&pageSize=7");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(7, (await ApiEnvelope.DataAsync(response)).GetProperty("pageSize").GetInt32());
    }

    [Fact(DisplayName = "PAGE-02 [Boundary] A store's delivery list honours pageSize")]
    public async Task StoreDeliveries_PageAndPageSize_ReturnsRequestedPageSize()
    {
        var order = await DeliveredOrderScenario.CreateAsync(_fixture);
        await DeliveredOrderScenario.CreateAsync(_fixture);

        var response = await _fixture.ClientFor(TestRole.GardenOwner)
            .GetAsync($"/api/orders/stores/{order.StoreId}/deliveries?page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await ApiEnvelope.DataAsync(response);
        Assert.Equal(1, data.GetProperty("pageSize").GetInt32());
        Assert.Single(data.GetProperty("items").EnumerateArray());
    }

    [Fact(DisplayName = "PAGE-03 [Boundary] The admin user search keeps its paging when a search term is sent")]
    public async Task AdminUsers_QueryAndPageSize_BindsBoth()
    {
        var response = await _fixture.ClientFor(TestRole.Admin)
            .GetAsync("/api/admin/users?query=a&page=1&pageSize=7");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(7, (await ApiEnvelope.DataAsync(response)).GetProperty("pageSize").GetInt32());
    }
}
