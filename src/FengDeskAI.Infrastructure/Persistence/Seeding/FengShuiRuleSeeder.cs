using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed 25 luật ngũ hành (5 mệnh × 5 hành sản phẩm) với điểm mặc định lấy từ
/// <see cref="FengShuiCalculator"/> — đảm bảo nhất quán với engine. Admin có thể chỉnh điểm sau.
/// Idempotent: bỏ qua nếu bảng đã có dữ liệu.
/// </summary>
public class FengShuiRuleSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<FengShuiRuleSeeder> _logger;

    public FengShuiRuleSeeder(AppDbContext context, ILogger<FengShuiRuleSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public int Order => 6;
    public string Name => "Feng shui rules (25 element relations)";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _context.Set<FengShuiRule>().AnyAsync(ct))
        {
            _logger.LogInformation("feng_shui_rules đã có dữ liệu — bỏ qua seeding.");
            return;
        }

        var rules = new List<FengShuiRule>();
        foreach (var subject in FengShuiCalculator.AllElements)
        foreach (var obj in FengShuiCalculator.AllElements)
        {
            var relation = FengShuiCalculator.GetRelation(subject, obj);
            rules.Add(new FengShuiRule
            {
                SubjectElement = subject,
                ObjectElement = obj,
                Relation = relation,
                Score = FengShuiCalculator.DefaultScore(relation),
                Description = $"Mệnh {subject} với hành {obj}: {relation}.",
            });
        }

        await _context.Set<FengShuiRule>().AddRangeAsync(rules, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Seed {Count} luật ngũ hành.", rules.Count);
    }
}
