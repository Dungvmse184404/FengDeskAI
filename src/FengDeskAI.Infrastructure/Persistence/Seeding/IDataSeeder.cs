namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Một seeder dữ liệu khởi tạo. Mỗi seeder tự idempotent (chỉ ghi khi cần).
/// Chạy theo <see cref="Order"/> tăng dần.
/// </summary>
public interface IDataSeeder
{
    int Order { get; }
    string Name { get; }
    Task SeedAsync(CancellationToken ct = default);
}
