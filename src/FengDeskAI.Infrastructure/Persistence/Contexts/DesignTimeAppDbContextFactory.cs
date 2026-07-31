using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FengDeskAI.Infrastructure.Persistence.Contexts;

/// <summary>
/// Cho phép EF CLI/Package Manager Console tạo &amp; áp migration mà không cần khởi động full app host
/// (né side-effect lúc host build: seeding, hosted workers, gọi API ngoài...). EF ưu tiên factory này
/// hơn build app host — nên nó phải tự đọc CÙNG appsettings.json/appsettings.{Environment}.json của
/// FengDeskAI.WebAPI, không hardcode connection string riêng (bài học từ bug thật: bản cũ hardcode
/// user "design" giả → Get-Migration/Update-Database auth failed vì lệch với DB thật đang dùng).
/// </summary>
public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // dotnet-ef (PMC dùng dưới nền) set current directory = thư mục startup project (WebAPI)
        // khi có factory này — nhưng phòng khi khác (IDE khác, chạy tay từ Infrastructure), thử thêm
        // đường dẫn tương đối sang WebAPI.
        var candidates = new[]
        {
            Directory.GetCurrentDirectory(),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "FengDeskAI.WebAPI"),
        };
        var basePath = candidates.FirstOrDefault(p => File.Exists(Path.Combine(p, "appsettings.json")))
            ?? throw new InvalidOperationException(
                "Không tìm thấy appsettings.json của FengDeskAI.WebAPI. Trong Package Manager Console, " +
                "đảm bảo \"Default project\" = FengDeskAI.Infrastructure và Startup Project (Solution " +
                "Explorer) = FengDeskAI.WebAPI.");

        // PMC/dotnet-ef tự set ASPNETCORE_ENVIRONMENT theo launchSettings.json của startup project
        // (mặc định "Development" ở đây) — cùng cơ chế app thật dùng lúc chạy.
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{env}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"ConnectionStrings:DefaultConnection rỗng (đã đọc từ {basePath}, môi trường \"{env}\"). " +
                "Kiểm tra appsettings.json/appsettings.Development.json trong FengDeskAI.WebAPI.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        return new AppDbContext(options);
    }
}
