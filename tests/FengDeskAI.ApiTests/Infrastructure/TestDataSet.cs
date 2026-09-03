using System.Text;
using System.Text.Json;

namespace FengDeskAI.ApiTests.Infrastructure;

/// <summary>
/// Một bộ dữ liệu test nạp từ <c>TestData/datasets/*.json</c>.
///
/// Trước đây giá trị thay tham số route và body request nằm cứng trong code, nên cả bộ test chỉ
/// chạy được đúng MỘT tổ hợp dữ liệu. Tách ra file cho phép thêm bộ mới mà không đụng vào code
/// test, và phân loại theo Normal / Boundary / Abnormal đúng cách tài liệu unit test của trường
/// yêu cầu (xem Report5_Unit Test.xls).
/// </summary>
public sealed class TestDataSet
{
    /// <summary>Khóa dùng chung khi không khai riêng cho tham số / endpoint cụ thể.</summary>
    private const string Fallback = "*";

    private readonly Dictionary<string, string> _routeValues;
    private readonly Dictionary<string, JsonElement> _bodies;
    private readonly Dictionary<string, string> _rawBodies;

    private TestDataSet(
        string name, string kind, string description, IReadOnlyList<string> appliesTo,
        Dictionary<string, string> routeValues,
        Dictionary<string, JsonElement> bodies,
        Dictionary<string, string> rawBodies)
    {
        Name = name;
        Kind = kind;
        Description = description;
        AppliesTo = appliesTo;
        _routeValues = routeValues;
        _bodies = bodies;
        _rawBodies = rawBodies;
    }

    public string Name { get; }

    /// <summary>Normal | Boundary | Abnormal — phân loại của tài liệu unit test.</summary>
    public string Kind { get; }

    public string Description { get; }

    /// <summary>Bộ test nào được dùng bộ dữ liệu này: authorization / smoke / robustness.</summary>
    public IReadOnlyList<string> AppliesTo { get; }

    public bool Supports(string suite) => AppliesTo.Contains(suite, StringComparer.OrdinalIgnoreCase);

    public override string ToString() => $"{Name} ({Kind})";

    /// <summary>
    /// Giá trị thay cho một tham số route. Thứ tự tra:
    /// <list type="number">
    ///   <item>đúng tên tham số — <c>"storeId"</c></item>
    ///   <item>ràng buộc kiểu trong template — <c>{storeId:guid}</c> tra khóa <c>"guid"</c></item>
    ///   <item>tên có chứa "id" nhưng không khai ràng buộc → tra khóa <c>"id"</c></item>
    ///   <item><c>"*"</c></item>
    /// </list>
    /// Bước 2 quan trọng: <c>{storeId:guid}</c> mà nhận chuỗi thường thì khâu CHỌN ACTION loại
    /// endpoint và trả 404 trước cả middleware phân quyền — ma trận 401/403 sẽ sai hàng loạt.
    /// </summary>
    public string RouteValue(string parameterName, string? constraint = null)
    {
        if (_routeValues.TryGetValue(parameterName, out var byName)) return byName;

        if (!string.IsNullOrEmpty(constraint) && _routeValues.TryGetValue(constraint, out var byConstraint))
            return byConstraint;

        if (parameterName.Contains("id", StringComparison.OrdinalIgnoreCase)
            && _routeValues.TryGetValue("id", out var byIdConvention))
            return byIdConvention;

        return _routeValues.TryGetValue(Fallback, out var any) ? any : "test";
    }

    /// <summary>
    /// Body cho một endpoint. Khóa tra cứu: "POST /api/Auth/login" rồi mới tới "*".
    /// <c>rawBodies</c> được ưu tiên để mô tả được cả JSON hỏng cú pháp.
    /// </summary>
    public HttpContent? BodyFor(EndpointInfo endpoint)
    {
        if (endpoint.HttpMethod is not ("POST" or "PUT" or "PATCH"))
            return null;

        // Endpoint multipart bị ràng buộc content-type: gửi JSON vào là 415 ngay ở khâu chọn
        // action, chưa tới phân quyền. Body dạng dữ liệu không áp dụng được cho nhóm này.
        if (endpoint.ConsumesMultipart)
            return new MultipartFormDataContent();

        var key = $"{endpoint.HttpMethod} /{endpoint.RouteTemplate}";

        if (_rawBodies.TryGetValue(key, out var raw) || _rawBodies.TryGetValue(Fallback, out raw))
            return new StringContent(raw, Encoding.UTF8, "application/json");

        if (_bodies.TryGetValue(key, out var body) || _bodies.TryGetValue(Fallback, out body))
            return new StringContent(body.GetRawText(), Encoding.UTF8, "application/json");

        return new StringContent("{}", Encoding.UTF8, "application/json");
    }

    // ===== Nạp từ file =====

    private static readonly Lazy<IReadOnlyList<TestDataSet>> All = new(LoadAll);

    public static IReadOnlyList<TestDataSet> ForSuite(string suite)
        => All.Value.Where(s => s.Supports(suite)).ToList();

    public static TestDataSet Default
        => All.Value.FirstOrDefault(s => s.Name == "normal") ?? All.Value[0];

    private static IReadOnlyList<TestDataSet> LoadAll()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "TestData", "datasets");
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException(
                $"Không tìm thấy thư mục bộ dữ liệu test: {dir}. "
                + "Kiểm tra mục Content copy TestData trong FengDeskAI.ApiTests.csproj.");

        var sets = Directory.EnumerateFiles(dir, "*.json")
            .OrderBy(f => f)
            .Select(Parse)
            .ToList();

        if (sets.Count == 0)
            throw new InvalidOperationException($"Thư mục {dir} không có file bộ dữ liệu nào.");

        return sets;
    }

    private static TestDataSet Parse(string path)
    {
        using var doc = JsonDocument.Parse(
            File.ReadAllText(path),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        var root = doc.RootElement;

        return new TestDataSet(
            name: GetString(root, "name") ?? Path.GetFileNameWithoutExtension(path),
            kind: GetString(root, "kind") ?? "Normal",
            description: GetString(root, "description") ?? string.Empty,
            appliesTo: root.TryGetProperty("appliesTo", out var applies) && applies.ValueKind == JsonValueKind.Array
                ? applies.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToList()
                : ["authorization", "smoke", "robustness"],
            routeValues: ReadStringMap(root, "routeValues"),
            // Clone(): JsonDocument bị dispose khi ra khỏi using, element không clone sẽ hỏng.
            bodies: ReadMap(root, "bodies", e => e.Clone()),
            rawBodies: ReadStringMap(root, "rawBodies"));
    }

    private static string? GetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static Dictionary<string, string> ReadStringMap(JsonElement root, string name)
        => ReadMap(root, name, e => e.GetString() ?? string.Empty);

    private static Dictionary<string, T> ReadMap<T>(JsonElement root, string name, Func<JsonElement, T> convert)
    {
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var prop in node.EnumerateObject())
            result[prop.Name] = convert(prop.Value);

        return result;
    }
}
