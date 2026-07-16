using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed bảng tra cứu <c>styles</c> + <c>vibes</c> + <c>elements</c>. Data đọc từ
/// <c>seed-data/styles-vibes.json</c>. Idempotent: chỉ thêm code còn thiếu.
/// Chạy SỚM (Order=1) vì product_styles/product_vibes/workspace_profiles có FK tới các code này.
/// </summary>
public class StyleVibeSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly SeedDataLoader _loader;
    private readonly ILogger<StyleVibeSeeder> _logger;

    public StyleVibeSeeder(AppDbContext context, SeedDataLoader loader, ILogger<StyleVibeSeeder> logger)
    {
        _context = context;
        _loader = loader;
        _logger = logger;
    }

    public int Order => 1;
    public string Name => "Styles & Vibes lookup";

    public sealed class LookupRow
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public int Sort { get; set; }
    }

    public sealed class FileModel
    {
        public List<LookupRow> Styles { get; set; } = new();
        public List<LookupRow> Vibes { get; set; } = new();
        public List<LookupRow> Elements { get; set; } = new();
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var file = _loader.Load<FileModel>("styles-vibes.json");

        var styleSet = _context.Set<Style>();
        var existingStyles = await styleSet.Select(s => s.Code).ToListAsync(ct);
        foreach (var row in file.Styles)
            if (!existingStyles.Contains(row.Code))
                await styleSet.AddAsync(new Style { Code = row.Code, Name = row.Name, SortOrder = row.Sort }, ct);

        var vibeSet = _context.Set<Vibe>();
        var existingVibes = await vibeSet.Select(v => v.Code).ToListAsync(ct);
        foreach (var row in file.Vibes)
            if (!existingVibes.Contains(row.Code))
                await vibeSet.AddAsync(new Vibe { Code = row.Code, Name = row.Name, SortOrder = row.Sort }, ct);

        var elementSet = _context.Set<Element>();
        var existingElements = await elementSet.Select(e => e.Code).ToListAsync(ct);
        foreach (var row in file.Elements)
            if (!existingElements.Contains(row.Code))
                await elementSet.AddAsync(new Element { Code = row.Code, Name = row.Name, SortOrder = row.Sort }, ct);

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seed styles/vibes/elements lookup xong.");
    }
}
