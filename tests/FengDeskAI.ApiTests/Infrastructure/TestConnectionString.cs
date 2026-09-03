using System.Text.Json;

namespace FengDeskAI.ApiTests.Infrastructure;

/// <summary>
/// Tìm connection string cho bộ test theo thứ tự: biến môi trường → file cục bộ
/// <c>appsettings.Testing.json</c>. KHÔNG có giá trị mặc định trong mã nguồn.
///
/// Vì sao cần lớp này: lúc đầu chỉ đọc biến môi trường, nên chạy test từ Visual Studio / Rider
/// hoặc <c>dotnet test</c> trần là hỏng ngay — mỗi lần chạy phải nhớ set biến. File cục bộ cho
/// phép cấu hình một lần rồi thôi, mà vẫn không có secret nào nằm trong repo:
/// <c>appsettings.Testing.json</c> khớp mẫu <c>**/appsettings.*.json</c> trong .gitignore.
/// CI vẫn dùng biến môi trường và luôn được ưu tiên.
/// </summary>
public static class TestConnectionString
{
    public const string EnvironmentVariable = "ConnectionStrings__DefaultConnection";

    public const string LocalFileName = "appsettings.Testing.json";

    /// <summary>Null nếu không tìm thấy ở cả hai nguồn — <see cref="TestDatabaseGuard"/> sẽ báo lỗi hướng dẫn.</summary>
    public static string? Resolve()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment;

        return ReadFromLocalFile();
    }

    /// <summary>Đường dẫn file cục bộ đang được tra — đưa vào thông báo lỗi cho dễ sửa.</summary>
    public static string LocalFilePath => Path.Combine(AppContext.BaseDirectory, LocalFileName);

    private static string? ReadFromLocalFile()
    {
        if (!File.Exists(LocalFilePath)) return null;

        using var doc = JsonDocument.Parse(
            File.ReadAllText(LocalFilePath),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        return doc.RootElement.TryGetProperty("ConnectionStrings", out var section)
               && section.TryGetProperty("DefaultConnection", out var value)
            ? value.GetString()
            : null;
    }
}
