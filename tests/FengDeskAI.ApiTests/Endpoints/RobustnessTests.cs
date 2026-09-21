using FengDeskAI.ApiTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Bắn dữ liệu xấu vào toàn bộ endpoint và khẳng định hệ thống **từ chối tử tế** — trả 4xx chứ
/// không nổ 5xx. Đây là nơi dùng bộ dữ liệu Abnormal (GUID sai định dạng, JSON hỏng cú pháp, số âm,
/// chuỗi chứa cú pháp SQL).
///
/// Vì sao tách khỏi <see cref="AuthorizationMatrixTests"/>: dữ liệu rác làm route constraint
/// <c>{id:guid}</c> loại endpoint ngay ở khâu chọn action → 404 trước cả middleware phân quyền,
/// nên không thể khẳng định 401/403 với bộ dữ liệu này.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class RobustnessTests
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public RobustnessTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public static TheoryData<string> Datasets()
    {
        var data = new TheoryData<string>();
        foreach (var set in TestDataSet.ForSuite("robustness"))
            data.Add(set.Name);
        return data;
    }

    [Theory]
    [MemberData(nameof(Datasets))]
    public async Task MalformedData_DoesNotCause5xx(string dataset)
    {
        var data = TestDataSet.ForSuite("robustness").Single(s => s.Name == dataset);
        _output.WriteLine($"Bộ dữ liệu: {data} — {data.Description}");

        var endpoints = EndpointCatalog.Discover(_fixture.Factory.Services);
        Assert.NotEmpty(endpoints);

        // Dùng token Admin: qua được phân quyền thì request mới đi sâu vào model binding và
        // service, tức là mới thật sự thử được sức chịu dữ liệu xấu.
        var client = _fixture.ClientFor(TestRole.Admin);
        var failures = new List<string>();

        foreach (var endpoint in endpoints)
        {
            try
            {
                var status = await AuthorizationMatrixTests.SendAsync(client, endpoint, data);
                if ((int)status >= 500)
                    failures.Add($"{endpoint} → {(int)status}");
            }
            catch (Exception ex)
            {
                failures.Add($"{endpoint} → ném {ex.GetType().Name}: {ex.Message}");
            }
        }

        _output.WriteLine($"Đã bắn {endpoints.Count} endpoint, lỗi 5xx: {failures.Count}.");

        if (failures.Count > 0)
            Assert.Fail($"{failures.Count}/{endpoints.Count} endpoint trả 5xx với dữ liệu '{data.Name}':"
                        + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }
}
