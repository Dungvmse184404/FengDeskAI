using System.Net;
using System.Text;
using FengDeskAI.ApiTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Tầng 1 — ma trận phân quyền tự sinh từ routing table, phủ TOÀN BỘ endpoint.
///
/// Bắt đúng nhóm lỗi làm sập app sau khi deploy: quên [Authorize], gắn nhầm policy, route trùng.
/// KHÔNG khẳng định nghiệp vụ đúng — đó là việc của tầng 2.
///
/// Chạy được vì authentication/authorization middleware đứng TRƯỚC model binding và controller,
/// nên request thiếu body / route id không có thật vẫn dừng đúng ở 401/403 mà không chạm DB.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class AuthorizationMatrixTests
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public AuthorizationMatrixTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Bộ dữ liệu nạp từ TestData/datasets/*.json. Thêm file mới là tự có thêm ca test —
    /// không phải sửa code test.
    /// </summary>
    public static TheoryData<string> Datasets()
    {
        var data = new TheoryData<string>();
        foreach (var set in TestDataSet.ForSuite("authorization"))
            data.Add(set.Name);
        return data;
    }

    private static TestDataSet Data(string name)
        => TestDataSet.ForSuite("authorization").Single(s => s.Name == name);

    [Fact]
    public void RoutingTable_IsNotEmpty()
    {
        var endpoints = EndpointCatalog.Discover(_fixture.Factory.Services);

        _output.WriteLine($"Tìm thấy {endpoints.Count} endpoint.");
        Assert.NotEmpty(endpoints);
    }

    [Theory]
    [MemberData(nameof(Datasets))]
    public async Task ProtectedEndpoints_WithoutToken_Return401(string dataset)
    {
        var data = Data(dataset);
        _output.WriteLine($"Bộ dữ liệu: {data} — {data.Description}");

        var endpoints = EndpointCatalog.Discover(_fixture.Factory.Services)
            .Where(e => e.RequiresAuth)
            .ToList();

        Assert.NotEmpty(endpoints);

        var client = _fixture.ClientFor(TestRole.Anonymous);
        var failures = new List<string>();

        foreach (var endpoint in endpoints)
        {
            var status = await SendAsync(client, endpoint, data);
            if (status != HttpStatusCode.Unauthorized)
                failures.Add($"{endpoint} → mong đợi 401, nhận {(int)status}");
        }

        AssertNoFailures(failures, endpoints.Count, "endpoint yêu cầu đăng nhập");
    }

    [Theory]
    [MemberData(nameof(Datasets))]
    public async Task PolicyEndpoints_WithWrongRole_Return403(string dataset)
    {
        var data = Data(dataset);
        _output.WriteLine($"Bộ dữ liệu: {data} — {data.Description}");

        var cases = EndpointCatalog.Discover(_fixture.Factory.Services)
            .Select(e => (Endpoint: e, Role: EndpointCatalog.PickDisallowedRole(e)))
            .Where(c => c.Role is not null)
            .ToList();

        Assert.NotEmpty(cases);

        var failures = new List<string>();

        foreach (var (endpoint, role) in cases)
        {
            var client = _fixture.ClientFor(role!.Value);
            var status = await SendAsync(client, endpoint, data);

            if (status != HttpStatusCode.Forbidden)
                failures.Add($"{endpoint} với role {role} → mong đợi 403, nhận {(int)status}");
        }

        AssertNoFailures(failures, cases.Count, "endpoint có policy role");
    }

    [Theory]
    [MemberData(nameof(Datasets))]
    public async Task PublicEndpoints_NeverReturn401Or403(string dataset)
    {
        var data = Data(dataset);
        _output.WriteLine($"Bộ dữ liệu: {data} — {data.Description}");

        var endpoints = EndpointCatalog.Discover(_fixture.Factory.Services)
            .Where(e => e.AllowsAnonymous && !UsesOwnAuthScheme(e))
            .ToList();

        Assert.NotEmpty(endpoints);

        var client = _fixture.ClientFor(TestRole.Anonymous);
        var failures = new List<string>();

        foreach (var endpoint in endpoints)
        {
            var status = await SendAsync(client, endpoint, data);

            // 401/403 trên endpoint [AllowAnonymous] nghĩa là phân quyền gắn sai chỗ.
            if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                failures.Add($"{endpoint} là public nhưng trả {(int)status}");
        }

        AssertNoFailures(failures, endpoints.Count, "endpoint public");
    }

    /// <summary>
    /// Endpoint webhook là <c>[AllowAnonymous]</c> với JWT nhưng tự xác thực bằng secret dùng chung
    /// trong header, và trả 401 khi thiếu (xem <c>ShippingController.IsWebhookAuthorized</c>).
    /// 401 ở đây là hành vi ĐÚNG, không phải phân quyền gắn sai — nên loại khỏi phép khẳng định này.
    /// Việc kiểm secret webhook đúng/sai thuộc tầng 2.
    /// </summary>
    private static bool UsesOwnAuthScheme(EndpointInfo endpoint)
        => endpoint.RouteTemplate.Contains("webhook", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// URL và body đều lấy từ bộ dữ liệu, không còn giá trị cứng trong code.
    /// Sai Content-Type là 415 (do khâu chọn action) thay vì 401/403 — TestDataSet.BodyFor xử lý
    /// riêng nhóm multipart để phép khẳng định phân quyền không bị vô hiệu.
    /// </summary>
    internal static async Task<HttpStatusCode> SendAsync(HttpClient client, EndpointInfo endpoint, TestDataSet data)
    {
        var request = new HttpRequestMessage(
            new HttpMethod(endpoint.HttpMethod), EndpointCatalog.BuildUrl(endpoint, data))
        {
            Content = data.BodyFor(endpoint),
        };

        var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private void AssertNoFailures(List<string> failures, int total, string label)
    {
        _output.WriteLine($"Đã kiểm tra {total} {label}, sai {failures.Count}.");

        if (failures.Count == 0) return;

        var report = string.Join(Environment.NewLine, failures);
        Assert.Fail($"{failures.Count}/{total} {label} sai kỳ vọng:{Environment.NewLine}{report}");
    }
}
