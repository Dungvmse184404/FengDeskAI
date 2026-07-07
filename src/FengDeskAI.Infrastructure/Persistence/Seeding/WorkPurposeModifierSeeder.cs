using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed <c>work_purpose_element_modifiers</c>: bẻ vector lý tưởng theo mục đích làm việc.
/// Idempotent theo (work_purpose, element).
/// </summary>
public class WorkPurposeModifierSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<WorkPurposeModifierSeeder> _logger;

    public WorkPurposeModifierSeeder(AppDbContext context, ILogger<WorkPurposeModifierSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public int Order => 4;
    public string Name => "Work purpose element modifiers (intent)";

    // (purpose, element, delta) — delta có thể âm.
    private static readonly (WorkPurpose Purpose, FengShuiElement Element, decimal Delta)[] Rows =
    {
        // Office: cần cấu trúc + tập trung.
        (WorkPurpose.Office, FengShuiElement.Kim, 0.05m),
        (WorkPurpose.Office, FengShuiElement.Tho, 0.05m),
        // Study: trí tuệ (Thủy) + minh mẫn (Kim).
        (WorkPurpose.Study, FengShuiElement.Thuy, 0.10m),
        (WorkPurpose.Study, FengShuiElement.Kim, 0.05m),
        // Reading: tĩnh (Thủy) + sinh khí nhẹ (Mộc).
        (WorkPurpose.Reading, FengShuiElement.Thuy, 0.10m),
        (WorkPurpose.Reading, FengShuiElement.Moc, 0.05m),
        // Creative: sinh trưởng (Mộc) + linh hoạt (Thủy).
        (WorkPurpose.Creative, FengShuiElement.Moc, 0.10m),
        (WorkPurpose.Creative, FengShuiElement.Thuy, 0.05m),
        // Gaming: năng lượng (Hỏa) + phản xạ (Kim), giảm tĩnh.
        (WorkPurpose.Gaming, FengShuiElement.Hoa, 0.05m),
        (WorkPurpose.Gaming, FengShuiElement.Kim, 0.05m),
        (WorkPurpose.Gaming, FengShuiElement.Thuy, -0.05m),
        // Mixed: cân bằng nhẹ về Thổ.
        (WorkPurpose.Mixed, FengShuiElement.Tho, 0.05m),
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var set = _context.Set<WorkPurposeElementModifier>();
        var existing = (await set.Select(x => new { x.WorkPurpose, x.Element }).ToListAsync(ct))
            .Select(x => (x.WorkPurpose, x.Element)).ToHashSet();

        int added = 0;
        foreach (var (purpose, element, delta) in Rows)
        {
            if (existing.Contains((purpose, element))) continue;
            await set.AddAsync(new WorkPurposeElementModifier
            {
                WorkPurpose = purpose,
                Element = element,
                Delta = delta,
            }, ct);
            added++;
        }
        if (added > 0) await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seed work_purpose_element_modifiers: thêm {Added} row.", added);
    }
}
