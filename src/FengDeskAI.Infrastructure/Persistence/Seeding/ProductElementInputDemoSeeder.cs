using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Gán chất liệu/màu/hình khối (product_element_inputs) cho sản phẩm demo → engine dùng auto-calc
/// vector (tầng 2) + cache vào 5 cột products. Idempotent: bỏ qua product đã có input.
/// Chạy sau khi element_input_map + demo products đã seed.
/// </summary>
public class ProductElementInputDemoSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProductElementInputDemoSeeder> _logger;

    public ProductElementInputDemoSeeder(AppDbContext context, ILogger<ProductElementInputDemoSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public int Order => 22; // sau ProductFengShuiDemoSeeder (21)
    public string Name => "Product element inputs (demo tier-2 vectors)";

    // (Tên chứa, [(kind, code)...]) — code khớp ElementInputMapSeeder.
    private static readonly (string Match, (ElementInputKind Kind, string Code)[] Inputs)[] Map =
    {
        ("Kim Tiền",      new[] { (ElementInputKind.Material, "Wood"),     (ElementInputKind.Color, "Green") }),
        ("Lưỡi Hổ",       new[] { (ElementInputKind.Material, "Wood"),     (ElementInputKind.Color, "Green") }),
        ("thạch anh",     new[] { (ElementInputKind.Material, "Crystal"),  (ElementInputKind.Color, "White") }),
        ("Tỳ Hưu",        new[] { (ElementInputKind.Material, "Metal"),    (ElementInputKind.Color, "White") }),
        ("muối Himalaya", new[] { (ElementInputKind.Material, "SaltRock"), (ElementInputKind.Color, "Orange"), (ElementInputKind.Shape, "Round") }),
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var map = await _context.Set<ElementInputMap>().AsNoTracking().ToListAsync(ct);
        if (map.Count == 0)
        {
            _logger.LogInformation("element_input_map trống — bỏ qua seed product element inputs.");
            return;
        }
        var resolver = new ElementInputResolver(map);
        var prms = ScoringParameters.FromRows(await _context.Set<ScoringParam>().AsNoTracking().ToListAsync(ct));

        var inputSet = _context.Set<ProductElementInput>();
        var products = await _context.Set<Product>().Include(p => p.Elements).ToListAsync(ct);

        int touched = 0;
        foreach (var p in products)
        {
            var m = Map.FirstOrDefault(x => p.Name.Contains(x.Match, StringComparison.OrdinalIgnoreCase));
            if (m.Match is null) continue;
            if (await inputSet.AnyAsync(i => i.ProductId == p.Id, ct)) continue; // đã có input

            var entities = m.Inputs.Select(i => new ProductElementInput
            {
                ProductId = p.Id,
                InputKind = i.Kind,
                InputCode = i.Code,
            }).ToList();
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
