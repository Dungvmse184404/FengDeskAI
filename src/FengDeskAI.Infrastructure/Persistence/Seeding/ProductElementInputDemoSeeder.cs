using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Gán chất liệu/màu/hình khối (<c>product_element_inputs</c>) cho sản phẩm demo → engine dùng
/// auto-calc vector (tầng 2) + cache vào 5 cột <c>products.element_*</c>. Data đọc từ
/// <c>catalog-demo.json</c> (xem <see cref="CatalogDemoFile"/>), khớp theo <b>tên đầy đủ</b>.
///
/// <para>
/// Idempotent theo kiểu <b>đồng bộ</b>, không phải "đã có thì bỏ qua": tập (kind, code) trong DB được
/// kéo về đúng file, vector cache tính lại, rồi <see cref="DemoProductFengShuiSync.Audit"/> cảnh báo nếu
/// vector thuần một hành hoặc hành trội ≠ hành chính khai. Chạy sau <c>element_input_map</c> + seeder 21.
/// </para>
/// </summary>
public class ProductElementInputDemoSeeder : IDataSeeder
{
    private const string FileName = "catalog-demo.json";

    private readonly AppDbContext _context;
    private readonly SeedDataLoader _loader;
    private readonly ILogger<ProductElementInputDemoSeeder> _logger;

    public ProductElementInputDemoSeeder(
        AppDbContext context, SeedDataLoader loader, ILogger<ProductElementInputDemoSeeder> logger)
    {
        _context = context;
        _loader = loader;
        _logger = logger;
    }

    public int Order => 22; // sau ProductFengShuiDemoSeeder (21)
    public string Name => "Product element inputs (demo tier-2 vectors)";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var map = await _context.Set<ElementInputMap>().AsNoTracking().ToListAsync(ct);
        if (map.Count == 0)
        {
            _logger.LogInformation("element_input_map trống — bỏ qua seed product element inputs.");
            return;
        }

        var byName = _loader.Load<CatalogDemoFile>(FileName).ByName();
        if (byName.Count == 0) return;

        var resolver = new ElementInputResolver(map);
        var prms = ScoringParameters.FromRows(await _context.Set<ScoringParam>().AsNoTracking().ToListAsync(ct));

        var inputSet = _context.Set<ProductElementInput>();
        var names = byName.Keys.ToList();
        var products = await _context.Set<Product>()
            .Include(p => p.Elements)
            .Where(p => names.Contains(p.Name))
            .ToListAsync(ct);
        var productIds = products.Select(p => p.Id).ToList();
        var existingByProduct = (await inputSet.Where(i => productIds.Contains(i.ProductId)).ToListAsync(ct))
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        int touched = 0;
        foreach (var p in products)
        {
            var row = byName[p.Name];
            if (row.ElementInputs.Count == 0 || p.IsVectorOverridden) continue; // override tay thì file không có quyền

            var desired = DemoProductFengShuiSync.ParseInputs(
                row.ElementInputs.Select(i => (i.Kind, i.Code)), _logger, FileName, p.Name);
            var existing = existingByProduct.GetValueOrDefault(p.Id) ?? new List<ProductElementInput>();

            var (inputs, changed) = DemoProductFengShuiSync.SyncInputs(
                p, desired, existing, inputSet, resolver, _logger, FileName);

            // Cache lại vector mỗi lần: hành chính/phụ (seeder 21) hoặc element_input_map có thể vừa đổi
            // dù tập input không đổi.
            var before = (p.ElementTho, p.ElementKim, p.ElementThuy, p.ElementMoc, p.ElementHoa);
            var vector = DemoProductFengShuiSync.CacheVector(p, inputs.ToList(), resolver, prms);
            DemoProductFengShuiSync.Audit(p, vector, _logger, FileName);

            if (changed || before != (p.ElementTho, p.ElementKim, p.ElementThuy, p.ElementMoc, p.ElementHoa))
                touched++;
        }

        if (touched > 0) await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Đồng bộ product_element_inputs + vector cho {Count} product demo.", touched);
    }
}
