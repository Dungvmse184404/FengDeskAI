using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed 9 tham số engine chấm điểm v3 (PHẦN F). Data đọc từ <c>seed-data/scoring-params.json</c>.
/// Idempotent theo code. LƯU Ý: các cặp *Share cần giữ tổng = 1.0 — chỉ chỉnh scale khi hiểu rõ engine.
/// </summary>
public class ScoringParamSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly SeedDataLoader _loader;
    private readonly ILogger<ScoringParamSeeder> _logger;

    public ScoringParamSeeder(AppDbContext context, SeedDataLoader loader, ILogger<ScoringParamSeeder> logger)
    {
        _context = context;
        _loader = loader;
        _logger = logger;
    }

    public int Order => 2;
    public string Name => "Scoring params (engine v3)";

    public sealed class Row
    {
        public string Code { get; set; } = "";
        public decimal Value { get; set; }
        public string Description { get; set; } = "";
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var file = _loader.Load<WeightedSeedFile<Row>>("scoring-params.json");
        var scale = _loader.EffectiveScale(file.WeightScale);

        var set = _context.Set<ScoringParam>();
        var existing = await set.Select(x => x.Code).ToListAsync(ct);
        int added = 0;
        foreach (var row in file.Rows)
        {
            if (existing.Contains(row.Code)) continue;
            await set.AddAsync(new ScoringParam
            {
                Code = row.Code,
                Value = row.Value * scale,
                Description = row.Description,
            }, ct);
            added++;
        }
        if (added > 0) await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seed scoring_params: thêm {Added} row (scale {Scale}).", added, scale);
    }
}
