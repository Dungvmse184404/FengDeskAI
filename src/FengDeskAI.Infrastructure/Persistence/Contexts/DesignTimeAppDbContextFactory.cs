using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FengDeskAI.Infrastructure.Persistence.Contexts;

/// <summary>
/// Cho phép EF CLI tạo migration mà không khởi động WebAPI, worker hoặc kết nối database.
/// Chuỗi kết nối chỉ dùng để chọn provider; lệnh migrations add không mở kết nối.
/// </summary>
public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=fengdesk_design;Username=design;Password=design")
            .Options;
        return new AppDbContext(options);
    }
}
