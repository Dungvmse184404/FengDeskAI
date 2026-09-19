using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Entities.Workspace;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed vector Ideal + Interior cho từng loại không gian hệ thống (khớp theo Name).
/// Data đọc từ <c>seed-data/workspace-type-elements.json</c>. Chạy sau <see cref="WorkspaceTypeSeeder"/>.
/// Khớp theo (type, source, element). Weight nhân với hệ số scale.
/// <para>
/// <b>UPSERT, không chỉ insert</b>: row đã tồn tại mà weight khác file seed thì được CẬP NHẬT.
/// Trước đây seeder chỉ Add rồi bỏ qua row cũ — nên sửa vector trong JSON xong chạy lại seed vẫn báo
/// "thêm 0 row", DB giữ giá trị cũ, file seed nói một đằng DB chạy một nẻo mà không ai biết.
/// </para>
/// <para>
/// Chỉ đụng loại phòng <c>IsSystemSeeded = true</c>. Row có trong DB nhưng KHÔNG còn trong file seed
/// được GIỮ NGUYÊN (không xóa) — tránh thổi bay hành mà admin cố ý thêm tay trong console.
/// </para>
/// <para>
/// <b>Và chỉ đụng row chưa ai chỉnh tay</b> (<c>UpdatedBy is null</c>). Bảng này CÓ endpoint admin —
/// <c>PUT /api/admin/scoring/workspace-type-elements</c> — nên upsert vô điều kiện như trước là mỗi
/// lần deploy lại thổi bay toàn bộ hiệu chỉnh vector phòng của người vận hành, im lặng. Vector phòng
/// là thứ quyết định gap của MỌI gợi ý, nên mất hiệu chỉnh ở đây là đổi ranking toàn hệ thống.
/// </para>
/// <para>
/// Hệ quả cần biết: sau khi admin chỉnh một hành qua API, hành đó <b>ngừng</b> nhận cập nhật từ file
/// seed. Muốn đổi lại giá trị nền cho row đã bị chỉnh thì đi bằng data migration có mệnh đề
/// <c>WHERE</c> canh đúng giá trị cũ, giống <c>ScoringPenaltiesV32</c>.
/// </para>
/// </summary>
public class WorkspaceTypeElementSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly SeedDataLoader _loader;
    private readonly ILogger<WorkspaceTypeElementSeeder> _logger;

    public WorkspaceTypeElementSeeder(AppDbContext context, SeedDataLoader loader, ILogger<WorkspaceTypeElementSeeder> logger)
    {
        _context = context;
        _loader = loader;
        _logger = logger;
    }

    public int Order => 6; // sau WorkspaceTypeSeeder (Order = 5)
    public string Name => "Workspace type elements (ideal + interior vectors)";

    /// <summary>Vector 5 hành, tổng nên = 1.0 (trước khi scale).</summary>
    public sealed class Vector
    {
        public decimal Tho { get; set; }
        public decimal Kim { get; set; }
        public decimal Thuy { get; set; }
        public decimal Moc { get; set; }
        public decimal Hoa { get; set; }
    }

    public sealed class Row
    {
        public string Name { get; set; } = "";
        public Vector Ideal { get; set; } = new();
        public Vector Interior { get; set; } = new();
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var file = _loader.Load<WeightedSeedFile<Row>>("workspace-type-elements.json");
        var scale = _loader.EffectiveScale(file.WeightScale);
        var byName = file.Rows.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

        var types = await _context.Set<WorkspaceType>()
            .Where(t => t.IsSystemSeeded)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(ct);

        var set = _context.Set<WorkspaceTypeElement>();
        // Nạp ENTITY (không projection) để còn sửa được weight — projection chỉ đọc, không update nổi.
        var existing = (await set.ToListAsync(ct))
            .ToDictionary(x => (x.WorkspaceTypeId, x.Source, x.Element));

        int added = 0, updated = 0, kept = 0;
        foreach (var type in types)
        {
            if (!byName.TryGetValue(type.Name, out var cfg)) continue;

            var (a1, u1, k1) = await UpsertSourceAsync(set, existing, type.Id, WorkspaceElementSources.Ideal, cfg.Ideal, scale, ct);
            var (a2, u2, k2) = await UpsertSourceAsync(set, existing, type.Id, WorkspaceElementSources.Interior, cfg.Interior, scale, ct);
            added += a1 + a2;
            updated += u1 + u2;
            kept += k1 + k2;
        }

        if (added > 0 || updated > 0) await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Seed workspace_type_elements: thêm {Added} row, cập nhật {Updated} row, "
            + "giữ nguyên {Kept} row admin đã chỉnh (scale {Scale}).",
            added, updated, kept, scale);
    }

    /// <summary>
    /// Đồng bộ 5 hành của một (type, source) về đúng file seed. Trả về (số row thêm, số row sửa).
    /// Cột weight là <c>numeric(4,3)</c> nên so sánh sau khi làm tròn 3 chữ số — nếu không, sai số
    /// thập phân khiến lần seed nào cũng báo "đã cập nhật" dù giá trị không hề đổi.
    /// </summary>
    private static async Task<(int Added, int Updated, int Kept)> UpsertSourceAsync(
        DbSet<WorkspaceTypeElement> set,
        Dictionary<(Guid, string, FengShuiElement), WorkspaceTypeElement> existing,
        Guid typeId, string source, Vector v, decimal scale,
        CancellationToken ct)
    {
        var rows = new (FengShuiElement Element, decimal Weight)[]
        {
            (FengShuiElement.Tho, v.Tho),
            (FengShuiElement.Kim, v.Kim),
            (FengShuiElement.Thuy, v.Thuy),
            (FengShuiElement.Moc, v.Moc),
            (FengShuiElement.Hoa, v.Hoa),
        };

        int added = 0, updated = 0, kept = 0;
        foreach (var (element, weight) in rows)
        {
            if (weight <= 0m) continue;
            var target = Math.Round(weight * scale, 3);

            if (existing.TryGetValue((typeId, source, element), out var current))
            {
                if (Math.Round(current.Weight, 3) == target) continue;

                if (current.UpdatedBy is not null)
                {
                    kept++; // admin đã chỉnh qua API — deploy không được đè
                    continue;
                }

                current.Weight = target;
                updated++;
                continue;
            }

            var entity = new WorkspaceTypeElement
            {
                WorkspaceTypeId = typeId,
                Source = source,
                Element = element,
                Weight = target,
            };
            await set.AddAsync(entity, ct);
            existing[(typeId, source, element)] = entity;
            added++;
        }
        return (added, updated, kept);
    }
}
