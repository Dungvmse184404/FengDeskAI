using System.Text;
using System.Text.Json;
using FengDeskAI.ApiTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Chạy các ca test nghiệp vụ mô tả trong <c>TestData/cases/*.json</c>. Mỗi dòng trong file là một
/// ca xunit riêng — thêm ca mới chỉ cần sửa JSON, không đụng code.
///
/// Phạm vi: ca một-request (gửi đi, kiểm status + message). Ca nhiều bước có trạng thái nối nhau
/// (đăng ký 3 bước, quên mật khẩu trọn vẹn, đổi email 4 bước) nằm ở <see cref="AuthFlowTests"/>
/// vì chúng cần bắt OTP và chuyền token giữa các bước.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class FunctionalCaseTests
{
    private readonly ApiTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public FunctionalCaseTests(ApiTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public static TheoryData<string> CaseIds()
    {
        var data = new TheoryData<string>();
        foreach (var c in FunctionalCaseLoader.LoadAll())
            data.Add(c.Id);
        return data;
    }

    [Theory]
    [MemberData(nameof(CaseIds))]
    public async Task FunctionalCase(string caseId)
    {
        var testCase = FunctionalCaseLoader.LoadAll().Single(c => c.Id == caseId);
        var tokens = new CaseTokens(_fixture);

        _output.WriteLine($"{testCase.Id} [{testCase.Kind}] {testCase.Description}");
        _output.WriteLine($"  Điều kiện trước: {testCase.PreCondition}");
        _output.WriteLine($"  Các bước       : {testCase.Procedure}");
        _output.WriteLine($"  Mong đợi       : {testCase.ExpectedResult}");

        var role = Enum.Parse<TestRole>(testCase.Role, ignoreCase: true);
        var client = _fixture.ClientFor(role);

        var request = new HttpRequestMessage(new HttpMethod(testCase.Method), testCase.Path);
        if (testCase.Body is { } body)
        {
            var json = tokens.Resolve(body.GetRawText());
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            _output.WriteLine($"  Body           : {json}");
        }

        var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"  Nhận được      : {(int)response.StatusCode} {responseBody}");

        Assert.True((int)response.StatusCode == testCase.ExpectedStatus,
            $"{testCase.Id}: mong đợi {testCase.ExpectedStatus}, nhận {(int)response.StatusCode}. Body: {responseBody}");

        if (!string.IsNullOrWhiteSpace(testCase.ExpectedMessageContains))
        {
            var message = ReadMessage(responseBody);
            Assert.True(
                message?.Contains(testCase.ExpectedMessageContains, StringComparison.OrdinalIgnoreCase) == true,
                $"{testCase.Id}: message phải chứa \"{testCase.ExpectedMessageContains}\" nhưng nhận \"{message}\".");
        }
    }

    /// <summary>Mọi response đi qua ApiControllerBase nên luôn có field "message" ở cấp gốc.</summary>
    private static string? ReadMessage(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
