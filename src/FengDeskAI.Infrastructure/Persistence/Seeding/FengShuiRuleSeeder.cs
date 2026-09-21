using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed 25 luật ngũ hành (5 mệnh × 5 hành sản phẩm) với điểm mặc định lấy từ
/// <see cref="FengShuiCalculator"/> — đảm bảo nhất quán với engine.
///
/// <para>
/// <b>ĐỒNG BỘ theo từng cặp, không phải "bảng có dữ liệu thì bỏ qua"</b>. Cách cũ dùng
/// <c>AnyAsync()</c> làm cờ chặn: chỉ cần bảng có ĐÚNG MỘT dòng là cả 25 luật bị bỏ qua vĩnh viễn —
/// một lần seed hỏng giữa chừng, hay một cặp bị xoá tay, là engine im lặng rơi về
/// <see cref="FengShuiCalculator.DefaultScore"/> cho cặp thiếu mà không ai biết.
/// </para>
///
/// <para>
/// <b>Chỉ đụng row chưa ai chỉnh tay</b> (<c>UpdatedBy is null</c>). Bảng này chưa có endpoint admin
/// nên hôm nay điều kiện đó luôn đúng và guard là no-op; khi UI chỉnh luật lên thì mọi bản ghi admin
/// sửa tự động được bảo vệ, không phải quay lại sửa seeder. Cùng nguyên tắc với
/// <c>ScoringPenaltiesV32</c>: dữ liệu người vận hành chỉnh được thì deploy không được đè.
/// </para>
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
        var set = _context.Set<FengShuiRule>();
        // Nạp ENTITY (không projection) để còn sửa được — projection chỉ đọc, không update nổi.
        var existing = (await set.ToListAsync(ct))
            .ToDictionary(r => (r.SubjectElement, r.ObjectElement));

        int added = 0, updated = 0, kept = 0;
        foreach (var subject in FengShuiCalculator.AllElements)
        foreach (var obj in FengShuiCalculator.AllElements)
        {
            var relation = FengShuiCalculator.GetRelation(subject, obj);
            decimal score = FengShuiCalculator.DefaultScore(relation);
            string description = $"Mệnh {subject} với hành {obj}: {relation}.";

            if (existing.TryGetValue((subject, obj), out var current))
            {
                if (current.UpdatedBy is not null)
                {
                    kept++; // admin đã chỉnh tay — deploy không được đè lên quyết định của họ
                    continue;
                }

                if (current.Relation == relation && current.Score == score && current.Description == description)
                    continue;

                current.Relation = relation;
                current.Score = score;
                current.Description = description;
                updated++;
                continue;
            }

            await set.AddAsync(new FengShuiRule
            {
                SubjectElement = subject,
                ObjectElement = obj,
                Relation = relation,
                Score = score,
                Description = description,
            }, ct);
            added++;
        }

        if (added > 0 || updated > 0) await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Seed feng_shui_rules: thêm {Added} luật, cập nhật {Updated}, giữ nguyên {Kept} luật admin đã chỉnh.",
            added, updated, kept);
    }
}
