using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Backfill thuộc tính phong thủy (ProductFengShui + Vibes + Styles) cho sản phẩm demo còn thiếu,
/// để engine gợi ý có ứng viên. Idempotent: chỉ gán cho product chưa có FengShui.
/// Map theo tên sản phẩm; product lạ dùng mặc định Thổ/Medium/Focus.
/// </summary>
public class ProductFengShuiDemoSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProductFengShuiDemoSeeder> _logger;

    public ProductFengShuiDemoSeeder(AppDbContext context, ILogger<ProductFengShuiDemoSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public int Order => 21; // sau CatalogDemoSeeder (20)
    public string Name => "Product feng shui (backfill demo products)";

    // (Tên chứa, hành chính, mã vibe, kích thước, mã phong cách) — code khớp bảng vibes/styles.
    private static readonly (string Match, FengShuiElement Element, string Vibe, SizeClass Size, string Style)[] Map =
    {
        ("Kim Tiền", FengShuiElement.Moc, "Focus", SizeClass.Small, "Scandinavian"),
        ("Lưỡi Hổ", FengShuiElement.Moc, "Calm", SizeClass.Small, "Minimal"),
        ("thạch anh", FengShuiElement.Tho, "Calm", SizeClass.Small, "Modern"),
        ("Tỳ Hưu", FengShuiElement.Kim, "Focus", SizeClass.Small, "Classic"),
        ("muối Himalaya", FengShuiElement.Hoa, "Relax", SizeClass.Medium, "Bohemian"),
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var products = await _context.Set<Product>()
            .Include(p => p.Elements)
            .Where(p => !p.Elements.Any())
            .ToListAsync(ct);

        if (products.Count == 0)
        {
            _logger.LogInformation("Mọi product đã có thuộc tính phong thủy — bỏ qua backfill.");
            return;
        }

        foreach (var p in products)
        {
            var m = Map.FirstOrDefault(x => p.Name.Contains(x.Match, StringComparison.OrdinalIgnoreCase));
            // default khi không khớp tên nào
            var element = m.Match is null ? FengShuiElement.Tho : m.Element;
            var vibe = m.Match is null ? "Focus" : m.Vibe;
            var size = m.Match is null ? SizeClass.Medium : m.Size;
            var style = m.Match is null ? "Modern" : m.Style;

            p.SizeClass = size;
            await _context.Set<ProductElement>().AddAsync(new ProductElement
            {
                ProductId = p.Id,
                Element = element,
                IsPrimary = true,
            }, ct);
            await _context.Set<ProductVibe>().AddAsync(new ProductVibe { ProductId = p.Id, VibeCode = vibe }, ct);
            await _context.Set<ProductStyle>().AddAsync(new ProductStyle { ProductId = p.Id, StyleCode = style }, ct);
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Backfill thuộc tính phong thủy cho {Count} product.", products.Count);
    }
}
