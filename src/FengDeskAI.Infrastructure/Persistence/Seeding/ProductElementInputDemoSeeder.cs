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
/// Idempotent: bỏ qua product đã có input. Chạy sau khi <c>element_input_map</c> + demo products đã seed.
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

        // Chỉ nhận (kind, code) đã có trong element_input_map — code lạ sẽ không đóng góp gì vào vector
        // mà vẫn nằm lại DB gây hiểu nhầm "đã khai rồi".
        var knownInputs = map
            .Select(m => (m.InputKind, Code: m.InputCode))
            .ToHashSet();

        var resolver = new ElementInputResolver(map);
        var prms = ScoringParameters.FromRows(await _context.Set<ScoringParam>().AsNoTracking().ToListAsync(ct));

        var inputSet = _context.Set<ProductElementInput>();
        var products = await _context.Set<Product>().Include(p => p.Elements).ToListAsync(ct);

        int touched = 0;
        foreach (var p in products)
        {
            if (!byName.TryGetValue(p.Name, out var row) || row.ElementInputs.Count == 0) continue;
            if (await inputSet.AnyAsync(i => i.ProductId == p.Id, ct)) continue; // đã có input

            var entities = new List<ProductElementInput>();
            foreach (var i in row.ElementInputs)
            {
                if (!Enum.TryParse<ElementInputKind>(i.Kind, ignoreCase: true, out var kind))
                {
                    _logger.LogWarning("{File}: inputKind '{Kind}' không hợp lệ ('{Name}') — bỏ qua.",
                        FileName, i.Kind, p.Name);
                    continue;
                }
                if (!knownInputs.Contains((kind, i.Code)))
                {
                    _logger.LogWarning("{File}: ({Kind}, {Code}) không có trong element_input_map ('{Name}') — bỏ qua.",
                        FileName, kind, i.Code, p.Name);
                    continue;
                }

                entities.Add(new ProductElementInput { ProductId = p.Id, InputKind = kind, InputCode = i.Code });
            }

            if (entities.Count == 0) continue;
            await inputSet.AddRangeAsync(entities, ct);

            // Cache vector (tầng 2) vào cột products.
            var vector = ProductVectorProvider.Build(
                isOverridden: false, overriddenVector: null, inputs: entities, resolver: resolver,
                productElements: p.Elements.Select(e => (e.Element, e.IsPrimary)), p: prms);
            p.ElementTho = vector.Tho;
            p.ElementKim = vector.Kim;
            p.ElementThuy = vector.Thuy;
            p.ElementMoc = vector.Moc;
            p.ElementHoa = vector.Hoa;
            touched++;
        }

        if (touched > 0) await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seed product_element_inputs cho {Count} product demo.", touched);
    }
}
