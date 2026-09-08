using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed bảng tra cứu <c>occupations</c> từ <c>seed-data/occupations.json</c>. Idempotent theo <c>code</c>.
///
/// <para>
/// <b>⚠️ CHỈ seed danh sách nghề, KHÔNG seed delta ngũ hành.</b> Danh sách nghề là một
/// <i>taxonomy</i> — thêm "Y tế" vào danh sách không phát biểu điều gì về phong thủy. Còn
/// <c>occupation_element_modifiers.delta</c> <i>là</i> một phát biểu phong thủy ("nghề này hợp hành
/// Thủy hơn"), nên nó phải đi qua chuyên gia duyệt rồi nhập bằng
/// <c>PUT /api/admin/scoring/occupations/{code}/modifiers</c>, đúng cách <c>feng_shui_rules</c> đã làm.
/// Bản nháp để chuyên gia bắt đầu nằm ở <c>docs/adr/score-explainability-v3.2.md</c> §11.5.
/// </para>
///
/// <para>
/// <b>⚠️ Cố ý CHỈ CHÈN, không đồng bộ row đã có.</b> Tên/mô tả sửa được runtime qua
/// <c>PUT /api/admin/scoring/occupations/{code}</c> — đồng bộ mỗi lần deploy sẽ đè mất chỉnh sửa của
/// admin. Muốn đổi giá trị nền của row đã tồn tại thì đi bằng <b>data migration</b> có mệnh đề
/// <c>WHERE</c> canh đúng giá trị cũ, xem <c>ScoringPenaltiesV32</c>.
/// </para>
/// </summary>
public class OccupationSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly SeedDataLoader _loader;
    private readonly ILogger<OccupationSeeder> _logger;

    public OccupationSeeder(AppDbContext context, SeedDataLoader loader, ILogger<OccupationSeeder> logger)
    {
        _context = context;
        _loader = loader;
        _logger = logger;
    }

    /// <summary>Chạy trước <c>AdminUserSeeder</c>: <c>users.occupation_id</c> có FK tới bảng này.</summary>
    public int Order => 2;

    public string Name => "Occupations lookup";

    public sealed class Row
    {
        public string Code { get; set; } = "";
        public string NameVi { get; set; } = "";
        public string? Description { get; set; }
        public int Sort { get; set; }
    }

    public sealed class FileModel
    {
        public List<Row> Occupations { get; set; } = new();
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var file = _loader.Load<FileModel>("occupations.json");

        var set = _context.Set<Occupation>();
        var existing = (await set.Select(o => o.Code).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        int added = 0;
        foreach (var row in file.Occupations)
        {
            if (existing.Contains(row.Code)) continue;
            await set.AddAsync(new Occupation
            {
                Code = row.Code,
                NameVi = row.NameVi,
                Description = row.Description,
                SortOrder = row.Sort,
                IsSystemSeeded = true,
            }, ct);
            added++;
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Seed occupations: thêm {Added} nghề (tổng {Total} trong file). Delta ngũ hành để trống — chờ chuyên gia duyệt.",
            added, file.Occupations.Count);
    }
}
