using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Entities.Workspace;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed vector Ideal + Interior cho từng loại không gian hệ thống (khớp theo Name).
/// Data đọc từ <c>seed-data/workspace-type-elements.json</c>. Chạy sau <see cref="WorkspaceTypeSeeder"/>.
/// Idempotent theo (type, source, element). Weight nhân với hệ số scale.
/// </summary>
public class WorkspaceTypeElementSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly SeedDataLoader _loader;
    private readonly ILogger<WorkspaceTypeElementSeeder> _logger;

    public WorkspaceTypeElementSeeder(AppDbContext context, SeedDataLoader loader, ILogger<WorkspaceTypeElementSeeder> logger)
    {
        _context = context;
        _loader = loader;
        _logger = logger;
    }

    public int Order => 6; // sau WorkspaceTypeSeeder (Order = 5)
    public string Name => "Workspace type elements (ideal + interior vectors)";

    /// <summary>Vector 5 hành, tổng nên = 1.0 (trước khi scale).</summary>
    public sealed class Vector
    {
        public decimal Tho { get; set; }
        public decimal Kim { get; set; }
        public decimal Thuy { get; set; }
        public decimal Moc { get; set; }
        public decimal Hoa { get; set; }
    }

    public sealed class Row
    {
        public string Name { get; set; } = "";
        public Vector Ideal { get; set; } = new();
        public Vector Interior { get; set; } = new();
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var file = _loader.Load<WeightedSeedFile<Row>>("workspace-type-elements.json");
        var scale = _loader.EffectiveScale(file.WeightScale);
        var byName = file.Rows.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

        var types = await _context.Set<WorkspaceType>()
            .Where(t => t.IsSystemSeeded)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(ct);

        var set = _context.Set<WorkspaceTypeElement>();
        var existing = (await set.Select(x => new { x.WorkspaceTypeId, x.Source, x.Element }).ToListAsync(ct))
            .Select(x => (x.WorkspaceTypeId, x.Source, x.Element)).ToHashSet();

        int added = 0;
        foreach (var type in types)
        {
            if (!byName.TryGetValue(type.Name, out var cfg)) continue;

            added += await AddSourceAsync(set, existing, type.Id, WorkspaceElementSources.Ideal, cfg.Ideal, scale, ct);
            added += await AddSourceAsync(set, existing, type.Id, WorkspaceElementSources.Interior, cfg.Interior, scale, ct);
        }

        if (added > 0) await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seed workspace_type_elements: thêm {Added} row (scale {Scale}).", added, scale);
    }

    private static async Task<int> AddSourceAsync(
        DbSet<WorkspaceTypeElement> set,
        HashSet<(Guid, string, FengShuiElement)> existing,
        Guid typeId, string source, Vector v, decimal scale,
        CancellationToken ct)
    {
        var rows = new (FengShuiElement Element, decimal Weight)[]
        {
            (FengShuiElement.Tho, v.Tho),
            (FengShuiElement.Kim, v.Kim),
            (FengShuiElement.Thuy, v.Thuy),
            (FengShuiElement.Moc, v.Moc),
            (FengShuiElement.Hoa, v.Hoa),
        };

        int added = 0;
        foreach (var (element, weight) in rows)
        {
            if (weight <= 0m) continue;
            if (existing.Contains((typeId, source, element))) continue;
            await set.AddAsync(new WorkspaceTypeElement
            {
                WorkspaceTypeId = typeId,
                Source = source,
                Element = element,
                Weight = weight * scale,
            }, ct);
            added++;
        }
        return added;
    }
}
