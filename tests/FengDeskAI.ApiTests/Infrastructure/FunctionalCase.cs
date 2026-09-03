using System.Text.Json;

namespace FengDeskAI.ApiTests.Infrastructure;

/// <summary>
/// Một ca test nghiệp vụ đọc từ <c>TestData/cases/*.json</c>. Mỗi dòng trong file thành một ca
/// xunit riêng, nên số test chạy trong CI khớp đúng số dòng khi lập tài liệu test.
/// </summary>
/// <remarks>
/// Các trường <see cref="Description"/>, <see cref="Procedure"/>, <see cref="PreCondition"/>,
/// <see cref="ExpectedResult"/> đặt đúng theo cột của <c>Report5_Test Report.xlsx</c> (Test Case
/// Description / Procedure / Expected Results / Pre-conditions) để xuất thẳng ra Excel, khỏi gõ lại.
/// <see cref="Kind"/> khớp cột phân loại N/B/A của tài liệu unit test.
/// </remarks>
public sealed record FunctionalCase(
    string Id,
    string Feature,
    string Kind,
    string Description,
    string Procedure,
    string PreCondition,
    string ExpectedResult,
    string Method,
    string Path,
    string Role,
    JsonElement? Body,
    int ExpectedStatus,
    string? ExpectedMessageContains)
{
    /// <summary>Hiển thị trong danh sách test — phải ngắn và đủ nhận ra ca nào.</summary>
    public override string ToString() => $"{Id} [{Kind}] {Description}";
}

public static class FunctionalCaseLoader
{
    /// <summary>Nạp toàn bộ ca test nghiệp vụ, gộp từ mọi file trong TestData/cases/.</summary>
    public static IReadOnlyList<FunctionalCase> LoadAll()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "TestData", "cases");
        if (!Directory.Exists(dir)) return [];

        return Directory.EnumerateFiles(dir, "*.json")
            .OrderBy(f => f)
            .SelectMany(ParseFile)
            .ToList();
    }

    private static IEnumerable<FunctionalCase> ParseFile(string path)
    {
        using var doc = JsonDocument.Parse(
            File.ReadAllText(path),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        var root = doc.RootElement;
        var feature = root.GetProperty("feature").GetString()!;

        foreach (var c in root.GetProperty("cases").EnumerateArray())
        {
            yield return new FunctionalCase(
                Id: c.GetProperty("id").GetString()!,
                Feature: feature,
                Kind: Str(c, "kind") ?? "Normal",
                Description: Str(c, "description") ?? "",
                Procedure: Str(c, "procedure") ?? "",
                PreCondition: Str(c, "preCondition") ?? "",
                ExpectedResult: Str(c, "expectedResult") ?? "",
                Method: c.GetProperty("method").GetString()!,
                Path: c.GetProperty("path").GetString()!,
                Role: Str(c, "role") ?? "Anonymous",
                // Clone(): JsonDocument bị dispose khi ra khỏi using.
                Body: c.TryGetProperty("body", out var b) ? b.Clone() : null,
                ExpectedStatus: c.GetProperty("expectedStatus").GetInt32(),
                ExpectedMessageContains: Str(c, "expectedMessageContains"));
        }
    }

    private static string? Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}

/// <summary>
/// Thay chỗ giữ chỗ <c>{{...}}</c> trong file dữ liệu bằng giá trị của phiên chạy.
///
/// Cần thiết vì mật khẩu và email user mẫu được sinh ngẫu nhiên mỗi lần chạy (không có secret nào
/// nằm trong mã nguồn) — file JSON không thể ghi cứng giá trị.
/// </summary>
public sealed class CaseTokens
{
    private readonly Dictionary<string, string> _values;

    public CaseTokens(ApiTestFixture fixture)
    {
        _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["password"] = fixture.Password,
        };

        foreach (var role in Enum.GetValues<TestRole>())
        {
            if (role == TestRole.Anonymous) continue;
            _values[$"email.{role}"] = ApiTestFixture.EmailFor(role);
        }
    }

    /// <summary>
    /// <c>{{email.Customer}}</c>, <c>{{password}}</c> → giá trị thật.
    /// <c>{{newEmail}}</c> sinh mới mỗi lần gọi — dùng cho ca cần email chưa từng đăng ký.
    /// </summary>
    public string Resolve(string input)
    {
        var result = input.Replace("{{newEmail}}", ApiTestFixture.NewEmail(), StringComparison.OrdinalIgnoreCase);

        foreach (var (key, value) in _values)
            result = result.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);

        return result;
    }
}
