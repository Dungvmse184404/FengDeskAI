namespace FengDeskAI.ApiTests.Infrastructure;

/// <summary>
/// Chốt chặn KHÔNG ĐƯỢC GỠ: test tạo/sửa/xóa dữ liệu thật, chạy nhầm vào DB production là mất dữ liệu.
///
/// Bối cảnh (xem docs/adr/api-integration-testing.md): <c>appsettings.json</c> base trỏ vào Supabase
/// production, <c>appsettings.Development.json</c> mới đè sang localhost — mà CẢ HAI đều gitignore.
/// Trên CI không có file nào → config rơi về base → trỏ thẳng production. Chạy <c>dotnet test</c> ở
/// local mà quên set biến môi trường cũng rơi vào đúng trường hợp đó.
/// </summary>
public static class TestDatabaseGuard
{
    /// <summary>Host được phép chạy test. Mọi host khác bị chặn, kể cả khi trông có vẻ vô hại.</summary>
    private static readonly string[] AllowedHosts = ["localhost", "127.0.0.1", "::1", "postgres", "db"];

    /// <summary>Chuỗi xuất hiện trong connection string là chặn ngay, không cần xét gì thêm.</summary>
    private static readonly string[] ForbiddenFragments = ["supabase.com", "supabase.co", "railway.app", "rds.amazonaws.com"];

    /// <summary>
    /// Ném <see cref="InvalidOperationException"/> nếu connection string không trỏ vào DB test cục bộ.
    /// Gọi TRƯỚC mọi thao tác DB — kể cả migrate.
    /// </summary>
    public static void EnsureSafe(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"""
                 Thiếu connection string cho test. Chọn MỘT trong hai cách:

                 1) File cục bộ (khuyến nghị — cấu hình một lần, chạy được từ IDE lẫn dòng lệnh):
                    Copy tests/FengDeskAI.ApiTests/{TestConnectionString.LocalFileName}.example
                    thành tests/FengDeskAI.ApiTests/{TestConnectionString.LocalFileName}
                    rồi sửa mật khẩu Postgres của bạn. File này đã được .gitignore.
                    Đường dẫn đang tra: {TestConnectionString.LocalFilePath}

                 2) Biến môi trường (dùng cho CI, luôn được ưu tiên):
                    {TestConnectionString.EnvironmentVariable}=Host=localhost;Port=5432;Database=fengdeskai_test;Username=postgres;Password=...

                 DB phải là Postgres CỤC BỘ và là database riêng cho test — test sẽ ghi/xóa dữ liệu thật.
                 """);

        var lowered = connectionString.ToLowerInvariant();

        foreach (var fragment in ForbiddenFragments)
        {
            if (lowered.Contains(fragment))
                throw new InvalidOperationException(
                    $"CHẶN: connection string của test trỏ vào hạ tầng từ xa ('{fragment}'). "
                    + "Test ghi/xóa dữ liệu thật — chỉ được chạy trên Postgres cục bộ. "
                    + "Đặt ConnectionStrings__DefaultConnection trỏ vào localhost.");
        }

        var host = ExtractHost(lowered);
        if (host is null)
            throw new InvalidOperationException(
                "Không đọc được Host trong connection string của test — không xác minh được là DB cục bộ nên từ chối chạy.");

        if (!AllowedHosts.Contains(host))
            throw new InvalidOperationException(
                $"CHẶN: connection string của test trỏ vào host '{host}', không nằm trong danh sách cho phép "
                + $"({string.Join(", ", AllowedHosts)}). Test chỉ được chạy trên Postgres cục bộ.");
    }

    private static string? ExtractHost(string loweredConnectionString)
    {
        foreach (var part in loweredConnectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator < 0) continue;

            var key = part[..separator].Trim();
            if (key is "host" or "server" or "data source")
                return part[(separator + 1)..].Trim();
        }

        return null;
    }
}
