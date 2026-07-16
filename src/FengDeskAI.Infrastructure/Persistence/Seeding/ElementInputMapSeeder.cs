using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed bảng <c>element_input_map</c>: màu / vật liệu / hình khối → hành. Dùng chung phòng + sản phẩm.
/// Data đọc từ <c>seed-data/element-input-map.json</c>. Idempotent theo (kind, code, element).
/// Weight nhân với hệ số scale (file-level <c>weightScale</c> hoặc config <c>Seeding:WeightScale</c>).
/// </summary>
public class ElementInputMapSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly SeedDataLoader _loader;
    private readonly ILogger<ElementInputMapSeeder> _logger;

    public ElementInputMapSeeder(AppDbContext context, SeedDataLoader loader, ILogger<ElementInputMapSeeder> logger)
    {
        _context = context;
        _loader = loader;
        _logger = logger;
    }

    public int Order => 3;
    public string Name => "Element input map (color/material/shape → element)";

    public sealed class Row
    {
        public ElementInputKind Kind { get; set; }
        public string Code { get; set; } = "";
        public FengShuiElement Element { get; set; }
        public decimal Weight { get; set; }
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var file = _loader.Load<WeightedSeedFile<Row>>("element-input-map.json");
        var scale = _loader.EffectiveScale(file.WeightScale);

        var set = _context.Set<ElementInputMap>();
        var existing = (await set.Select(x => new { x.InputKind, x.InputCode, x.Element }).ToListAsync(ct))
            .Select(x => (x.InputKind, x.InputCode, x.Element)).ToHashSet();

        int added = 0;
        foreach (var row in file.Rows)
        {
            if (existing.Contains((row.Kind, row.Code, row.Element))) continue;
            await set.AddAsync(new ElementInputMap
            {
                InputKind = row.Kind,
                InputCode = row.Code,
                Element = row.Element,
                Weight = row.Weight * scale,
            }, ct);
            added++;
        }
        if (added > 0) await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seed element_input_map: thêm {Added} row (scale {Scale}).", added, scale);
    }
}
