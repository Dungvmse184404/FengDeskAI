using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed <c>work_purpose_element_modifiers</c>: bẻ vector lý tưởng theo mục đích làm việc.
/// Data đọc từ <c>seed-data/work-purpose-modifiers.json</c>. Idempotent theo (work_purpose, element).
/// Delta (có thể âm) nhân với hệ số scale.
/// </summary>
public class WorkPurposeModifierSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly SeedDataLoader _loader;
    private readonly ILogger<WorkPurposeModifierSeeder> _logger;

    public WorkPurposeModifierSeeder(AppDbContext context, SeedDataLoader loader, ILogger<WorkPurposeModifierSeeder> logger)
    {
        _context = context;
        _loader = loader;
        _logger = logger;
    }

    public int Order => 4;
    public string Name => "Work purpose element modifiers (intent)";

    public sealed class Row
    {
        public WorkPurpose Purpose { get; set; }
        public FengShuiElement Element { get; set; }
        public decimal Delta { get; set; }
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var file = _loader.Load<WeightedSeedFile<Row>>("work-purpose-modifiers.json");
        var scale = _loader.EffectiveScale(file.WeightScale);

        var set = _context.Set<WorkPurposeElementModifier>();
        var existing = (await set.Select(x => new { x.WorkPurpose, x.Element }).ToListAsync(ct))
            .Select(x => (x.WorkPurpose, x.Element)).ToHashSet();

        int added = 0;
        foreach (var row in file.Rows)
        {
            if (existing.Contains((row.Purpose, row.Element))) continue;
            await set.AddAsync(new WorkPurposeElementModifier
            {
                WorkPurpose = row.Purpose,
                Element = row.Element,
                Delta = row.Delta * scale,
            }, ct);
            added++;
        }
        if (added > 0) await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seed work_purpose_element_modifiers: thêm {Added} row (scale {Scale}).", added, scale);
    }
}
