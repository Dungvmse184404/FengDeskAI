using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

public static class DatabaseSeederExtensions
{
    /// <summary>
    /// Chạy toàn bộ <see cref="IDataSeeder"/> theo thứ tự. Tùy chọn apply migrations trước.
    /// Dùng cho chế độ "chỉ seeding": <c>dotnet run -- seed</c>.
    /// </summary>
    public static async Task RunSeedersAsync(this IServiceProvider services, bool migrate = true, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        if (migrate)
        {
            logger.LogInformation("Áp dụng migrations (nếu có pending)...");
            await sp.GetRequiredService<AppDbContext>().Database.MigrateAsync(ct);
        }

        var seeders = sp.GetServices<IDataSeeder>().OrderBy(s => s.Order).ToList();
        foreach (var seeder in seeders)
        {
            logger.LogInformation("→ Seeding: {Name}", seeder.Name);
            await seeder.SeedAsync(ct);
        }

        logger.LogInformation("Seeding hoàn tất ({Count} seeder).", seeders.Count);
    }
}
