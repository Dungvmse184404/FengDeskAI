using FengDeskAI.Domain.Entities.Workspace;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed các loại không gian hệ thống + trọng số cá nhân (riêng tư = 1.0, công cộng = 0.5).
/// Data đọc từ <c>seed-data/workspace-types.json</c>. Idempotent theo tên.
/// PersonalWeight nhân với hệ số scale.
/// </summary>
public class WorkspaceTypeSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly SeedDataLoader _loader;
    private readonly ILogger<WorkspaceTypeSeeder> _logger;

    public WorkspaceTypeSeeder(AppDbContext context, SeedDataLoader loader, ILogger<WorkspaceTypeSeeder> logger)
    {
        _context = context;
        _loader = loader;
        _logger = logger;
    }

    public int Order => 5;
    public string Name => "Workspace types (system + personal weights)";

    public sealed class Row
    {
        public string Name { get; set; } = "";
        public bool IsPublic { get; set; }
        public decimal PersonalWeight { get; set; }
        public WorkspaceScope Scope { get; set; }
        public string Description { get; set; } = "";
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var file = _loader.Load<WeightedSeedFile<Row>>("workspace-types.json");
        var scale = _loader.EffectiveScale(file.WeightScale);

        // Idempotent THEO TỪNG TÊN — cho phép thêm loại mới vào file JSON ở các lần deploy sau.
        var existingNames = (await _context.Set<WorkspaceType>()
            .Where(t => t.IsSystemSeeded)
            .Select(t => t.Name)
            .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = file.Rows.Where(t => !existingNames.Contains(t.Name)).ToList();
        if (toAdd.Count == 0)
        {
            _logger.LogInformation("Workspace types hệ thống đã đầy đủ — bỏ qua seeding.");
            return;
        }

        var entities = toAdd.Select(t => new WorkspaceType
        {
            Name = t.Name,
            Description = t.Description,
            IsPublic = t.IsPublic,
            PersonalWeight = t.PersonalWeight * scale,
            Scope = t.Scope,
            IsSystemSeeded = true,
        });

        await _context.Set<WorkspaceType>().AddRangeAsync(entities, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Seed {Count} workspace types hệ thống mới (scale {Scale}).", toAdd.Count, scale);
    }
}
