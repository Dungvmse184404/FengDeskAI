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
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _context.Set<WorkspaceType>().AnyAsync(t => t.IsSystemSeeded, ct))
        {
            _logger.LogInformation("Workspace types hệ thống đã tồn tại — bỏ qua seeding.");
            return;
        }

        var entities = SystemTypes.Select(t => new WorkspaceType
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

        _logger.LogInformation("Seed {Count} workspace types hệ thống.", SystemTypes.Length);
    }
}
