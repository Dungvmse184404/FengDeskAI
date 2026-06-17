using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed bảng tra cứu <c>styles</c> + <c>vibes</c> (thay cho enum cũ). Idempotent: chỉ thêm code còn thiếu.
/// Chạy SỚM (Order=1) vì product_styles/product_vibes/workspace_profiles có FK tới các code này.
/// </summary>
public class StyleVibeSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<StyleVibeSeeder> _logger;

    public StyleVibeSeeder(AppDbContext context, ILogger<StyleVibeSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public int Order => 1;
    public string Name => "Styles & Vibes lookup";

    // (code bất biến, tên hiển thị, thứ tự)
    private static readonly (string Code, string Name, int Sort)[] Styles =
    {
        ("Modern", "Hiện đại", 1),
        ("Classic", "Cổ điển", 2),
        ("Minimal", "Tối giản", 3),
        ("Industrial", "Công nghiệp", 4),
        ("Scandinavian", "Bắc Âu", 5),
        ("Bohemian", "Bohemian", 6),
        ("Other", "Khác", 99),
    };

    private static readonly (string Code, string Name, int Sort)[] Vibes =
    {
        ("Focus", "Tập trung", 1),
        ("Relax", "Thư giãn", 2),
        ("Creative", "Sáng tạo", 3),
        ("Calm", "Tĩnh tại", 4),
        ("Energize", "Năng lượng", 5),
    };

    // Ngũ hành cố định 5 — code khớp enum FengShuiElement mà engine dùng.
    private static readonly (string Code, string Name, int Sort)[] Elements =
    {
        ("Kim", "Kim (Metal)", 1),
        ("Moc", "Mộc (Wood)", 2),
        ("Thuy", "Thủy (Water)", 3),
        ("Hoa", "Hỏa (Fire)", 4),
        ("Tho", "Thổ (Earth)", 5),
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var styleSet = _context.Set<Style>();
        var existingStyles = await styleSet.Select(s => s.Code).ToListAsync(ct);
        foreach (var (code, name, sort) in Styles)
            if (!existingStyles.Contains(code))
                await styleSet.AddAsync(new Style { Code = code, Name = name, SortOrder = sort }, ct);

        var vibeSet = _context.Set<Vibe>();
        var existingVibes = await vibeSet.Select(v => v.Code).ToListAsync(ct);
        foreach (var (code, name, sort) in Vibes)
            if (!existingVibes.Contains(code))
                await vibeSet.AddAsync(new Vibe { Code = code, Name = name, SortOrder = sort }, ct);

        var elementSet = _context.Set<Element>();
        var existingElements = await elementSet.Select(e => e.Code).ToListAsync(ct);
        foreach (var (code, name, sort) in Elements)
            if (!existingElements.Contains(code))
                await elementSet.AddAsync(new Element { Code = code, Name = name, SortOrder = sort }, ct);

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seed styles/vibes/elements lookup xong.");
    }
}
