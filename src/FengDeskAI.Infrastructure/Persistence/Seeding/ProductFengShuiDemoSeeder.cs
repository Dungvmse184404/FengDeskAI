using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Backfill thuộc tính phong thủy (hành + vibe + style + size từng SKU) cho sản phẩm demo còn thiếu,
/// để engine gợi ý có ứng viên. Data đọc từ <c>catalog-demo.json</c> (xem <see cref="CatalogDemoFile"/>),
/// khớp theo <b>tên đầy đủ</b>; sản phẩm không có trong file dùng <c>defaults</c>.
/// Idempotent: chỉ chạm product chưa có <see cref="ProductElement"/> nào.
/// </summary>
public class ProductFengShuiDemoSeeder : IDataSeeder
{
    private const string FileName = "catalog-demo.json";

    private readonly AppDbContext _context;
    private readonly SeedDataLoader _loader;
    private readonly ILogger<ProductFengShuiDemoSeeder> _logger;

    public ProductFengShuiDemoSeeder(
        AppDbContext context, SeedDataLoader loader, ILogger<ProductFengShuiDemoSeeder> logger)
    {
        _context = context;
        _loader = loader;
        _logger = logger;
    }

    public int Order => 21; // sau CatalogDemoSeeder (20)
    public string Name => "Product feng shui (backfill demo products)";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var products = await _context.Set<Product>()
            .Include(p => p.Elements)
            .Include(p => p.Items)
            .Where(p => !p.Elements.Any())
            .ToListAsync(ct);

        if (products.Count == 0)
        {
            _logger.LogInformation("Mọi product đã có thuộc tính phong thủy — bỏ qua backfill.");
            return;
        }

        var file = _loader.Load<CatalogDemoFile>(FileName);
        var byName = file.ByName();
        var d = file.Defaults;

        // Chỉ gắn code đã tồn tại trong bảng tra cứu — tránh nổ FK khi file lệch với seed lookup.
        var knownVibes = (await _context.Set<Vibe>().Select(v => v.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownStyles = (await _context.Set<Style>().Select(s => s.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var p in products)
        {
            byName.TryGetValue(p.Name, out var row);

            var element = ParseElement(row?.PrimaryElement) ?? ParseElement(d.PrimaryElement) ?? FengShuiElement.Tho;
            var vibes = (row?.Vibes.Count > 0 ? row.Vibes : new List<string> { d.Vibe })
                .Where(knownVibes.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var styles = (row?.Styles.Count > 0 ? row.Styles : new List<string> { d.Style })
                .Where(knownStyles.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            await _context.Set<ProductElement>().AddAsync(new ProductElement
            {
                ProductId = p.Id,
                Element = element,
                IsPrimary = true,
            }, ct);

            foreach (var secondary in (row?.SecondaryElements ?? new List<string>())
                         .Select(ParseElement).Where(e => e is not null && e != element).Distinct())
            {
                await _context.Set<ProductElement>().AddAsync(new ProductElement
                {
                    ProductId = p.Id,
                    Element = secondary!.Value,
                    IsPrimary = false,
                }, ct);
            }

            foreach (var v in vibes)
                await _context.Set<ProductVibe>().AddAsync(new ProductVibe { ProductId = p.Id, VibeCode = v }, ct);
            foreach (var s in styles)
                await _context.Set<ProductStyle>().AddAsync(new ProductStyle { ProductId = p.Id, StyleCode = s }, ct);

            // SizeClass nằm ở TỪNG SKU (migration MoveSizeClassToProductItem). Khớp theo Sku, không có
            // thì theo Name; không khớp dòng nào → dùng defaults. Chỉ điền SKU còn trống.
            foreach (var item in p.Items.Where(i => i.SizeClass is null))
            {
                var itemRow = row?.Items.FirstOrDefault(x =>
                    (!string.IsNullOrWhiteSpace(x.Sku) && string.Equals(x.Sku, item.Sku, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(x.Name) && string.Equals(x.Name, item.Name, StringComparison.OrdinalIgnoreCase)));

                if (Enum.TryParse<SizeClass>(itemRow?.SizeClass ?? d.SizeClass, ignoreCase: true, out var size))
                    item.SizeClass = size;
            }
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Backfill thuộc tính phong thủy cho {Count} product.", products.Count);
    }

    private static FengShuiElement? ParseElement(string? code)
        => Enum.TryParse<FengShuiElement>(code, ignoreCase: true, out var e) ? e : null;
}
