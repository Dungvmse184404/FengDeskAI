using System.Net;
using FengDeskAI.ApiTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Kiểm chính hạ tầng test: token do <see cref="ApiTestFixture"/> cấp phải dùng được thật.
/// Nếu bộ này đỏ thì mọi kết luận của ma trận phân quyền đều vô nghĩa — sửa ở đây trước.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class AuthHelperTests
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public AuthHelperTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Theory]
    [InlineData(TestRole.Customer)]
    [InlineData(TestRole.Staff)]
    [InlineData(TestRole.Manager)]
    [InlineData(TestRole.Admin)]
    [InlineData(TestRole.GardenOwner)]
    public async Task TokenForEveryRole_CanCallAuthMe(TestRole role)
    {
        var response = await _fixture.ClientFor(role).GetAsync("/api/Auth/me");
        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"{role}: {(int)response.StatusCode} {body}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(ApiTestFixture.EmailFor(role), body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthMe_WithoutToken_Returns401()
    {
        var response = await _fixture.ClientFor(TestRole.Anonymous).GetAsync("/api/Auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
