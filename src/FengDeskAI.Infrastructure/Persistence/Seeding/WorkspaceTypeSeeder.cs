using FengDeskAI.Domain.Entities.Workspace;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed các loại không gian hệ thống + trọng số cá nhân (riêng tư = 1.0, công cộng = 0.5).
/// Data đọc từ <c>seed-data/workspace-types.json</c>. Idempotent theo tên.
/// PersonalWeight nhân với hệ số scale.
/// </summary>
public class WorkspaceTypeSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly SeedDataLoader _loader;
    private readonly ILogger<WorkspaceTypeSeeder> _logger;

    public WorkspaceTypeSeeder(AppDbContext context, SeedDataLoader loader, ILogger<WorkspaceTypeSeeder> logger)
    {
        _context = context;
        _loader = loader;
        _logger = logger;
    }

    public int Order => 5;
    public string Name => "Workspace types (system + personal weights)";

    public sealed class Row
    {
        public string Name { get; set; } = "";
        public bool IsPublic { get; set; }
        public decimal PersonalWeight { get; set; }
        public WorkspaceScope Scope { get; set; }
        public string Description { get; set; } = "";
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var file = _loader.Load<WeightedSeedFile<Row>>("workspace-types.json");
        var scale = _loader.EffectiveScale(file.WeightScale);

        // Idempotent THEO TỪNG TÊN — cho phép thêm loại mới vào file JSON ở các lần deploy sau.
        var existing = await _context.Set<WorkspaceType>()
            .Where(t => t.IsSystemSeeded)
            .ToListAsync(ct);
        var existingByName = existing
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Scope quyết định Wp, Description là chữ hiển thị — KHÔNG endpoint admin nào sửa được hai cột
        // này (WorkspaceTypesController chỉ có GET + POST tạo loại của user), nên seeder là đường duy
        // nhất và phải ĐỒNG BỘ chứ không chỉ chèn tên mới. Nếu không, việc sửa scope trong JSON
        // (v3.2 §4 — 🔴E: Bếp/Phòng khách/Phòng ăn/Home Theater/Guest Room/Phòng thờ từ Private sang
        // Shared) sẽ vô tác dụng trên mọi DB đã seed.
        //
        // Cố ý KHÔNG đồng bộ PersonalWeight: đó là cột legacy v2 đang chờ gỡ (ADR §7.3 · C.2), engine
        // v3 đọc Wp từ scoring_params theo Scope chứ không đọc cột này.
        //
        // Chỉ đụng row IsSystemSeeded — loại do user tự tạo không bị chạm tới. Không cần guard
        // UpdatedBy như các seeder khác vì không có API nào ghi vào hai cột này để mà bảo vệ.
        int rescoped = 0, redescribed = 0;
        foreach (var row in file.Rows)
        {
            if (!existingByName.TryGetValue(row.Name, out var current)) continue;

            if (current.Scope != row.Scope)
            {
                _logger.LogInformation(
                    "Đổi scope workspace type {Name}: {Old} → {New}.", current.Name, current.Scope, row.Scope);
                current.Scope = row.Scope;
                rescoped++;
            }

            if (current.Description != row.Description)
            {
                current.Description = row.Description;
                redescribed++;
            }
        }

        var toAdd = file.Rows.Where(t => !existingByName.ContainsKey(t.Name)).ToList();
        if (toAdd.Count == 0 && rescoped == 0 && redescribed == 0)
        {
            _logger.LogInformation("Workspace types hệ thống đã đầy đủ — bỏ qua seeding.");
            return;
        }

        var entities = toAdd.Select(t => new WorkspaceType
        {
            Name = t.Name,
            Description = t.Description,
            IsPublic = t.IsPublic,
            PersonalWeight = t.PersonalWeight * scale,
            Scope = t.Scope,
            IsSystemSeeded = true,
        });

        await _context.Set<WorkspaceType>().AddRangeAsync(entities, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seed {Count} workspace types hệ thống mới (scale {Scale}); "
            + "đồng bộ scope cho {Rescoped} loại, mô tả cho {Redescribed} loại đã có.",
            toAdd.Count, scale, rescoped, redescribed);
    }
}
