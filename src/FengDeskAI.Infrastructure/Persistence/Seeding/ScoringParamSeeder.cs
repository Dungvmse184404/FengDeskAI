using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>Seed 9 tham số engine chấm điểm v3 (PHẦN F). Idempotent theo code.</summary>
public class ScoringParamSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<ScoringParamSeeder> _logger;

    public ScoringParamSeeder(AppDbContext context, ILogger<ScoringParamSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public int Order => 2;
    public string Name => "Scoring params (engine v3)";

    private static readonly (string Code, decimal Value, string Desc)[] Params =
    {
        (ScoringParamCodes.SelfShare, 0.60m, "Tỉ trọng bản mệnh trong personalVector."),
        (ScoringParamCodes.SupportShare, 0.30m, "Tỉ trọng hành sinh ra mệnh (mẹ)."),
        (ScoringParamCodes.ChildShare, 0.10m, "Tỉ trọng hành mệnh sinh ra (con)."),
        (ScoringParamCodes.MaterialShare, 0.60m, "Tỉ trọng chất liệu trong productVector."),
        (ScoringParamCodes.ColorShare, 0.40m, "Tỉ trọng màu/hình khối trong productVector."),
        (ScoringParamCodes.UserConflictPenalty, 0.30m, "Trừ điểm khi hành sản phẩm khắc mệnh (scope Shared/Public)."),
        (ScoringParamCodes.DirectionPenalty, 0.15m, "Trừ điểm khi mọi hướng hợp vật phẩm đều bị chắn."),
        (ScoringParamCodes.FallbackPrimary, 0.70m, "Trọng số hành chính khi backfill từ product_elements."),
        (ScoringParamCodes.FallbackSecondary, 0.30m, "Trọng số hành phụ khi backfill từ product_elements."),
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var set = _context.Set<ScoringParam>();
        var existing = await set.Select(x => x.Code).ToListAsync(ct);
        int added = 0;
        foreach (var (code, value, desc) in Params)
        {
            if (existing.Contains(code)) continue;
            await set.AddAsync(new ScoringParam { Code = code, Value = value, Description = desc }, ct);
            added++;
        }
        if (added > 0) await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seed scoring_params: thêm {Added} row.", added);
    }
}
