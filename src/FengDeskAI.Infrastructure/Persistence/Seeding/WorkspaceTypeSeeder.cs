using FengDeskAI.Domain.Entities.Workspace;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed các loại không gian hệ thống + trọng số cá nhân. Không gian riêng tư = 1.0,
/// công cộng (dùng chung nhiều người) = 0.5. Idempotent: bỏ qua nếu đã có loại seed sẵn.
/// </summary>
public class WorkspaceTypeSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<WorkspaceTypeSeeder> _logger;

    public WorkspaceTypeSeeder(AppDbContext context, ILogger<WorkspaceTypeSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public int Order => 5;
    public string Name => "Workspace types (system + personal weights)";

    // (Name, IsPublic, PersonalWeight, Scope, Desc)
    private static readonly (string Name, bool IsPublic, decimal Weight, WorkspaceScope Scope, string Desc)[] SystemTypes =
    {
        ("Personal Desk", false, 1.0m, WorkspaceScope.Private, "Bàn làm việc cá nhân."),
        ("Home Office", false, 1.0m, WorkspaceScope.Private, "Phòng làm việc tại nhà, một người dùng."),
        ("Private Office", false, 1.0m, WorkspaceScope.Private, "Phòng làm việc riêng trong công ty."),
        ("Meeting Room", true, 0.5m, WorkspaceScope.Shared, "Phòng họp dùng chung nhiều người."),
        ("Co-working Booth", true, 0.5m, WorkspaceScope.Shared, "Khoang làm việc chia sẻ."),
        ("Open Workspace", true, 0.5m, WorkspaceScope.Shared, "Khu làm việc mở dùng chung."),
        ("Reception / Lounge", true, 0.5m, WorkspaceScope.Public, "Khu lễ tân / tiếp khách chung."),

        // Không gian sinh hoạt tại nhà — mở rộng ngoài phạm vi "bàn làm việc" thuần túy.
        ("Kitchen", false, 1.0m, WorkspaceScope.Private, "Bếp — khu vực nấu nướng trong nhà."),
        ("Living Room", false, 1.0m, WorkspaceScope.Private, "Phòng khách — không gian sinh hoạt chung."),
        ("Bedroom", false, 1.0m, WorkspaceScope.Private, "Phòng ngủ."),
        ("Dining Room", false, 1.0m, WorkspaceScope.Private, "Phòng ăn."),
        ("Kids Room", false, 1.0m, WorkspaceScope.Private, "Phòng trẻ em."),
        ("Balcony", false, 1.0m, WorkspaceScope.Private, "Ban công / sân nhỏ."),
        ("Home Gym", false, 1.0m, WorkspaceScope.Private, "Góc tập luyện tại nhà."),

        // Mở rộng thêm — bao quát các không gian đặc trưng của nhà ở Việt Nam + các phòng chức năng còn thiếu.
        ("Altar Room", false, 1.0m, WorkspaceScope.Private, "Gian thờ — không gian thờ cúng tổ tiên."),
        ("Bathroom", false, 1.0m, WorkspaceScope.Private, "Phòng tắm / nhà vệ sinh."),
        ("Study Room", false, 1.0m, WorkspaceScope.Private, "Phòng học — dành cho học sinh, sinh viên."),
        ("Home Theater", false, 1.0m, WorkspaceScope.Private, "Phòng giải trí / rạp phim mini tại nhà."),
        ("Walk-in Closet", false, 1.0m, WorkspaceScope.Private, "Phòng thay đồ."),
        ("Garage", false, 1.0m, WorkspaceScope.Private, "Nhà để xe."),
        ("Rooftop Garden", false, 1.0m, WorkspaceScope.Private, "Sân thượng / vườn trên mái."),
        ("Guest Room", false, 1.0m, WorkspaceScope.Private, "Phòng dành cho khách."),
        ("Meditation Room", false, 1.0m, WorkspaceScope.Private, "Phòng thiền / yoga."),
        ("Laundry Room", false, 1.0m, WorkspaceScope.Private, "Phòng giặt đồ."),
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Idempotent THEO TỪNG TÊN (không "skip toàn bộ nếu đã có ít nhất 1 loại") — cho phép
        // thêm loại mới vào SystemTypes ở các lần deploy sau mà không cần xoá DB.
        var existingNames = (await _context.Set<WorkspaceType>()
            .Where(t => t.IsSystemSeeded)
            .Select(t => t.Name)
            .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = SystemTypes.Where(t => !existingNames.Contains(t.Name)).ToList();
        if (toAdd.Count == 0)
        {
            _logger.LogInformation("Workspace types hệ thống đã đầy đủ — bỏ qua seeding.");
            return;
        }

        var entities = toAdd.Select(t => new WorkspaceType
        {
            Name = t.Name,
            Description = t.Desc,
            IsPublic = t.IsPublic,
            PersonalWeight = t.Weight,
            Scope = t.Scope,
            IsSystemSeeded = true,
        });

        await _context.Set<WorkspaceType>().AddRangeAsync(entities, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Seed {Count} workspace types hệ thống mới.", toAdd.Count);
    }
}
