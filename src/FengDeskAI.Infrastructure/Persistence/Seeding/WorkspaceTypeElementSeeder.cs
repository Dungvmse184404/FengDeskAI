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
/// Chạy sau <see cref="WorkspaceTypeSeeder"/>. Idempotent theo (type, source, element).
/// </summary>
public class WorkspaceTypeElementSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<WorkspaceTypeElementSeeder> _logger;

    public WorkspaceTypeElementSeeder(AppDbContext context, ILogger<WorkspaceTypeElementSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public int Order => 6; // sau WorkspaceTypeSeeder (Order = 5)
    public string Name => "Workspace type elements (ideal + interior vectors)";

    // Vector = (Tho, Kim, Thuy, Moc, Hoa), Σ = 1.0.
    private static readonly Dictionary<string, ((decimal Tho, decimal Kim, decimal Thuy, decimal Moc, decimal Hoa) Ideal,
                                               (decimal Tho, decimal Kim, decimal Thuy, decimal Moc, decimal Hoa) Interior)> ByName = new()
    {
        ["Personal Desk"]      = ((0.25m, 0.20m, 0.15m, 0.30m, 0.10m), (0.35m, 0.20m, 0.10m, 0.25m, 0.10m)),
        ["Home Office"]        = ((0.20m, 0.20m, 0.20m, 0.30m, 0.10m), (0.30m, 0.20m, 0.10m, 0.30m, 0.10m)),
        ["Private Office"]     = ((0.25m, 0.30m, 0.15m, 0.20m, 0.10m), (0.35m, 0.25m, 0.10m, 0.20m, 0.10m)),
        ["Meeting Room"]       = ((0.30m, 0.30m, 0.10m, 0.20m, 0.10m), (0.40m, 0.30m, 0.05m, 0.15m, 0.10m)),
        ["Co-working Booth"]   = ((0.20m, 0.25m, 0.15m, 0.30m, 0.10m), (0.30m, 0.25m, 0.10m, 0.25m, 0.10m)),
        ["Open Workspace"]     = ((0.20m, 0.20m, 0.20m, 0.30m, 0.10m), (0.30m, 0.25m, 0.10m, 0.25m, 0.10m)),
        ["Reception / Lounge"] = ((0.30m, 0.25m, 0.10m, 0.15m, 0.20m), (0.35m, 0.25m, 0.10m, 0.10m, 0.20m)),

        // Không gian sinh hoạt tại nhà — Ideal theo công năng, Interior theo vật liệu/décor điển hình.
        ["Kitchen"]      = ((0.25m, 0.15m, 0.10m, 0.15m, 0.35m), (0.20m, 0.30m, 0.10m, 0.10m, 0.30m)),
        ["Living Room"]  = ((0.25m, 0.15m, 0.15m, 0.25m, 0.20m), (0.25m, 0.15m, 0.15m, 0.30m, 0.15m)),
        ["Bedroom"]      = ((0.25m, 0.10m, 0.30m, 0.25m, 0.10m), (0.25m, 0.10m, 0.20m, 0.35m, 0.10m)),
        ["Dining Room"]  = ((0.35m, 0.15m, 0.10m, 0.15m, 0.25m), (0.30m, 0.15m, 0.10m, 0.30m, 0.15m)),
        ["Kids Room"]    = ((0.25m, 0.05m, 0.15m, 0.35m, 0.20m), (0.25m, 0.10m, 0.15m, 0.30m, 0.20m)),
        ["Balcony"]      = ((0.20m, 0.10m, 0.25m, 0.35m, 0.10m), (0.20m, 0.10m, 0.20m, 0.40m, 0.10m)),
        ["Home Gym"]     = ((0.20m, 0.25m, 0.10m, 0.15m, 0.30m), (0.15m, 0.35m, 0.10m, 0.15m, 0.25m)),

        // Mở rộng thêm — không gian đặc trưng nhà ở Việt Nam + phòng chức năng còn thiếu.
        ["Altar Room"]      = ((0.40m, 0.15m, 0.05m, 0.10m, 0.30m), (0.25m, 0.10m, 0.05m, 0.30m, 0.30m)),
        ["Bathroom"]        = ((0.15m, 0.20m, 0.45m, 0.10m, 0.10m), (0.20m, 0.30m, 0.35m, 0.10m, 0.05m)),
        ["Study Room"]      = ((0.20m, 0.15m, 0.30m, 0.30m, 0.05m), (0.25m, 0.15m, 0.20m, 0.35m, 0.05m)),
        ["Home Theater"]    = ((0.15m, 0.30m, 0.10m, 0.15m, 0.30m), (0.20m, 0.35m, 0.10m, 0.10m, 0.25m)),
        ["Walk-in Closet"]  = ((0.25m, 0.35m, 0.10m, 0.15m, 0.15m), (0.20m, 0.30m, 0.10m, 0.25m, 0.15m)),
        ["Garage"]          = ((0.25m, 0.45m, 0.10m, 0.05m, 0.15m), (0.30m, 0.40m, 0.10m, 0.05m, 0.15m)),
        ["Rooftop Garden"]  = ((0.15m, 0.05m, 0.20m, 0.45m, 0.15m), (0.20m, 0.05m, 0.15m, 0.50m, 0.10m)),
        ["Guest Room"]      = ((0.30m, 0.20m, 0.20m, 0.20m, 0.10m), (0.30m, 0.20m, 0.15m, 0.25m, 0.10m)),
        ["Meditation Room"] = ((0.25m, 0.05m, 0.35m, 0.30m, 0.05m), (0.30m, 0.05m, 0.25m, 0.35m, 0.05m)),
        ["Laundry Room"]    = ((0.15m, 0.30m, 0.40m, 0.10m, 0.05m), (0.15m, 0.35m, 0.35m, 0.10m, 0.05m)),
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
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
            if (!ByName.TryGetValue(type.Name, out var cfg)) continue;

            added += await AddSourceAsync(set, existing, type.Id, WorkspaceElementSources.Ideal, cfg.Ideal, ct);
            added += await AddSourceAsync(set, existing, type.Id, WorkspaceElementSources.Interior, cfg.Interior, ct);
        }

        if (added > 0) await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seed workspace_type_elements: thêm {Added} row.", added);
    }

    private static async Task<int> AddSourceAsync(
        DbSet<WorkspaceTypeElement> set,
        HashSet<(Guid, string, FengShuiElement)> existing,
        Guid typeId, string source,
        (decimal Tho, decimal Kim, decimal Thuy, decimal Moc, decimal Hoa) v,
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
                Weight = weight,
            }, ct);
            added++;
        }
        return added;
    }
}
