using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed <c>occupation_element_profiles</c> từ <c>seed-data/occupation-element-profiles.json</c> — hồ sơ
/// ngũ hành Σ=1 của từng nghề (N3, ADR <c>occupation-product-fit-v1.md</c> §2).
///
/// <para>
/// <b>Khác P5.4:</b> seeder này chèn thẳng bản nháp thay vì để trống — người ra đề đã chọn seed sẵn.
/// Chốt an toàn dời sang trọng số: <c>OCCUPATION_WEIGHT</c> seed 0 nên hồ sơ có trong DB vẫn chưa tác
/// động điểm cho tới khi được nâng qua API admin sau golden set.
/// </para>
///
/// <para>
/// <b>Idempotent theo NGHỀ, không theo dòng:</b> chỉ chèn cho nghề <i>chưa có dòng nào</i>. Hồ sơ là
/// một phân bố trọn vẹn — chèn lẻ vài hành còn thiếu vào một hồ sơ admin đã sửa là phá Σ=1 của họ.
/// Muốn đổi giá trị nền của nghề đã có hồ sơ thì đi bằng data migration (xem <c>ScoringPenaltiesV32</c>).
/// </para>
///
/// <para>
/// <b>Ném khi hồ sơ trong file lệch Σ=1:</b> dữ liệu seed sai phải chặn ngay lúc deploy, không để engine
/// chạy trên một trục nghề nghiêng về một phía không ai chủ ý.
/// </para>
/// </summary>
public class OccupationElementProfileSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly SeedDataLoader _loader;
    private readonly ILogger<OccupationElementProfileSeeder> _logger;

    public OccupationElementProfileSeeder(
        AppDbContext context, SeedDataLoader loader, ILogger<OccupationElementProfileSeeder> logger)
    {
        _context = context;
        _loader = loader;
        _logger = logger;
    }

    /// <summary>Sau <c>OccupationSeeder</c> (Order 2): cần nghề tồn tại để nối FK.</summary>
    public int Order => 3;

    public string Name => "Occupation element profiles (N3)";

    public sealed class Row
    {
        public string Occupation { get; set; } = "";
        public FengShuiElement Element { get; set; }
        public decimal Share { get; set; }
        public string? Why { get; set; }
    }

    public sealed class FileModel
    {
        public List<Row> Rows { get; set; } = new();
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var file = _loader.Load<FileModel>("occupation-element-profiles.json");

        var byOccupation = file.Rows
            .GroupBy(r => r.Occupation, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Chặn file sai TRƯỚC khi chạm DB: một nghề lệch tổng thì cả đợt seed dừng, không seed nửa chừng.
        foreach (var (code, rows) in byOccupation)
        {
            var error = OccupationProfileRules.Validate(rows.Select(r => (r.Element, r.Share)).ToList());
            if (error is not null)
                throw new InvalidOperationException(
                    $"occupation-element-profiles.json: hồ sơ nghề '{code}' không hợp lệ - {error}");
        }

        var occupations = await _context.Set<Occupation>()
            .Include(o => o.Profile)
            .ToListAsync(ct);
        var byCode = occupations.ToDictionary(o => o.Code, StringComparer.OrdinalIgnoreCase);

        var set = _context.Set<OccupationElementProfile>();
        int seededOccupations = 0, skippedUnknown = 0, skippedExisting = 0;

        foreach (var (code, rows) in byOccupation)
        {
            if (!byCode.TryGetValue(code, out var occupation))
            {
                skippedUnknown++;
                _logger.LogWarning(
                    "Seed occupation profiles: bỏ qua '{Code}' - nghề chưa có trong bảng occupations.", code);
                continue;
            }

            if (occupation.Profile.Count > 0)
            {
                skippedExisting++;
                continue;
            }

            foreach (var row in rows.Where(r => r.Share > 0m))
                await set.AddAsync(new OccupationElementProfile
                {
                    OccupationId = occupation.Id,
                    Element = row.Element,
                    Share = row.Share,
                }, ct);

            seededOccupations++;
        }

        if (seededOccupations > 0) await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Seed occupation_element_profiles: chèn hồ sơ cho {Seeded} nghề, giữ nguyên {Existing} nghề đã có, "
            + "bỏ qua {Unknown} mã lạ. Trọng số OCCUPATION_WEIGHT vẫn do scoring_params quyết định.",
            seededOccupations, skippedExisting, skippedUnknown);
    }
}
