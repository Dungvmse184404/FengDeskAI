using System.Net;
using FengDeskAI.ApiTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Tầng 1 — smoke: gọi thật các endpoint GET không tham số bằng token Admin và khẳng định
/// KHÔNG có 5xx. Đây là phần duy nhất ở tầng 1 chạm tới DB và service, nên nó bắt được lỗi
/// thiếu đăng ký DI, AutoMapper thiếu profile, LINQ dịch không nổi sang SQL — những thứ chỉ nổ
/// lúc runtime, sau khi đã deploy.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class SmokeTests
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SmokeTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task ParameterlessGetEndpoints_DoNotReturn5xx()
    {
        var endpoints = EndpointCatalog.Discover(_fixture.Factory.Services)
            .Where(e => e.HttpMethod == "GET" && !e.HasRouteParameters)
            .ToList();

        Assert.NotEmpty(endpoints);

        var client = _fixture.ClientFor(TestRole.Admin);
        var failures = new List<string>();

        foreach (var endpoint in endpoints)
        {
            HttpStatusCode status;
            string? body = null;

            try
            {
                var response = await client.GetAsync(EndpointCatalog.BuildUrl(endpoint, TestDataSet.Default));
                status = response.StatusCode;
                if ((int)status >= 500)
                    body = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                failures.Add($"{endpoint} → ném {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            if ((int)status >= 500)
                failures.Add($"{endpoint} → {(int)status}. Body: {Truncate(body)}");
        }

        _output.WriteLine($"Đã gọi {endpoints.Count} endpoint GET, lỗi 5xx: {failures.Count}.");

        if (failures.Count > 0)
            Assert.Fail($"{failures.Count}/{endpoints.Count} endpoint GET trả 5xx:"
                        + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task SwaggerDocument_IsGenerated()
    {
        // Swagger hỏng = có action trùng route hoặc kiểu trả về không mô tả nổi. Nó không làm app
        // chết lúc khởi động nhưng làm FE mất tài liệu, và thường là dấu hiệu route bị trùng.
        var response = await _fixture.ClientFor(TestRole.Anonymous).GetAsync("/swagger/v1/swagger.json");

        Assert.True(response.IsSuccessStatusCode,
            $"/swagger/v1/swagger.json trả {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static string Truncate(string? value)
        => value is null ? "(rỗng)" : value.Length <= 400 ? value : value[..400] + "…";
}
