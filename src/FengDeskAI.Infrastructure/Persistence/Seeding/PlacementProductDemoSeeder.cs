using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Entities.Vendor;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed sản phẩm demo cho một <see cref="ProductPlacement"/> cụ thể — data ĐỌC TỪ FILE, không hardcode
/// trong seeder. Hiện dùng cho <c>carry-products-demo.json</c> (vật phẩm mang theo người), nhưng file
/// nào cùng shape cũng chạy được vì <c>placement</c> khai ngay trong file.
/// Idempotent theo tên sản phẩm; bỏ qua nếu chưa có store demo.
/// </summary>
public class PlacementProductDemoSeeder : IDataSeeder
{
    private const string FileName = "carry-products-demo.json";

    private readonly AppDbContext _context;
    private readonly SeedDataLoader _loader;
    private readonly ILogger<PlacementProductDemoSeeder> _logger;

    public PlacementProductDemoSeeder(
        AppDbContext context, SeedDataLoader loader, ILogger<PlacementProductDemoSeeder> logger)
    {
        _context = context;
        _loader = loader;
        _logger = logger;
    }

    public int Order => 23; // sau ProductElementInputDemoSeeder (22)
    public string Name => "Placement product demo (carry-on items)";

    public sealed class FileModel
    {
        public string StoreName { get; set; } = "";

        /// <summary>Desk | Living | Carry | Consumable.</summary>
        public string Placement { get; set; } = nameof(ProductPlacement.Desk);

        /// <summary><c>{slug}</c> được thay bằng tên sản phẩm đã URL-encode.</summary>
        public string ImageUrlTemplate { get; set; } = "";

        public List<Row> Rows { get; set; } = new();
    }

    public sealed class Row
    {
        public string Name { get; set; } = "";

        /// <summary>
        /// Loại vật (taxonomy duyệt hàng): "Trang sức phong thủy", "Đá phong thủy"… KHÔNG phải nơi
        /// dùng — cái đó là <see cref="FileModel.Placement"/>. Đừng tạo category phản chiếu placement,
        /// hai trục sẽ lệch nhau và không biết cái nào đúng.
        /// </summary>
        public string? Category { get; set; }

        public string? Description { get; set; }
        public string? PrimaryElement { get; set; }
        public List<string> SecondaryElements { get; set; } = new();
        public List<string> Vibes { get; set; } = new();
        public List<InputRow> ElementInputs { get; set; } = new();
        public List<ItemRow> Items { get; set; } = new();
    }

    public sealed class InputRow
    {
        /// <summary>Color | Material | Shape | DecorItem.</summary>
        public string Kind { get; set; } = "";

        /// <summary>Khớp <c>element_input_map.input_code</c> (xem seed-data/element-input-map.json).</summary>
        public string Code { get; set; } = "";
    }

    public sealed class ItemRow
    {
        public string? Name { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string? Sku { get; set; }

        /// <summary>Small | Medium | Large — của CHÍNH biến thể này. Bỏ trống = chưa khai.</summary>
        public string? SizeClass { get; set; }

        public int WeightGram { get; set; } = 500;
        public int LengthCm { get; set; } = 10;
        public int WidthCm { get; set; } = 10;
        public int HeightCm { get; set; } = 10;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var file = _loader.Load<FileModel>(FileName);
        if (file.Rows.Count == 0) return;

        if (!Enum.TryParse<ProductPlacement>(file.Placement, ignoreCase: true, out var placement))
        {
            _logger.LogWarning("{File}: placement '{Placement}' không hợp lệ — bỏ qua.", FileName, file.Placement);
            return;
        }

        // Bám theo store demo của CatalogDemoSeeder (Order 20). Không có → chưa seed demo, bỏ qua.
        var store = await _context.Set<GardenStore>()
            .FirstOrDefaultAsync(s => s.Name == file.StoreName, ct);
        if (store is null)
        {
            _logger.LogInformation("Chưa có store '{Store}' — bỏ qua seed sản phẩm {Placement}.", file.StoreName, placement);
            return;
        }

        var map = await _context.Set<ElementInputMap>().AsNoTracking().ToListAsync(ct);
        var resolver = new ElementInputResolver(map);
        var prms = ScoringParameters.FromRows(await _context.Set<ScoringParam>().AsNoTracking().ToListAsync(ct));

        // Chỉ gắn vibe/element code đã tồn tại trong bảng tra cứu — tránh nổ FK khi file lệch seed lookup.
        var knownVibes = (await _context.Set<Vibe>().Select(v => v.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Category = LOẠI VẬT (khai theo từng dòng), tái dùng category sẵn có nếu trùng tên.
        var categories = (await _context.Set<Category>().ToListAsync(ct))
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var existingNames = (await _context.Set<Product>().Select(p => p.Name).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int added = 0;
        foreach (var row in file.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.Name) || existingNames.Contains(row.Name)) continue;

            var product = new Product
            {
                GardenStoreId = store.Id,
                Name = row.Name,
                Description = row.Description,
                IsActive = true,
                Placement = placement,
            };

            if (!string.IsNullOrWhiteSpace(file.ImageUrlTemplate))
            {
                product.Images.Add(new ProductImage
                {
                    Url = file.ImageUrlTemplate.Replace("{slug}", Uri.EscapeDataString(row.Name)),
                    SortOrder = 0,
                });
            }

            foreach (var item in row.Items)
            {
                product.Items.Add(new ProductItem
                {
                    Name = item.Name,
                    Price = item.Price,
                    Stock = item.Stock,
                    Sku = item.Sku,
                    SizeClass = Enum.TryParse<SizeClass>(item.SizeClass, ignoreCase: true, out var size)
                        ? size
                        : null,
                    WeightGram = item.WeightGram,
                    LengthCm = item.LengthCm,
                    WidthCm = item.WidthCm,
                    HeightCm = item.HeightCm,
                });
            }

            if (!string.IsNullOrWhiteSpace(row.Category))
            {
                if (!categories.TryGetValue(row.Category, out var category))
                {
                    category = new Category { Name = row.Category, IsActive = true };
                    await _context.Set<Category>().AddAsync(category, ct);
                    categories[row.Category] = category;
                }
                product.ProductCategories.Add(new ProductCategory { CategoryId = category.Id });
            }

            // Hành chính/phụ — tầng 3 fallback của ProductVectorProvider.
            if (Enum.TryParse<FengShuiElement>(row.PrimaryElement, ignoreCase: true, out var primary))
            {
                product.Elements.Add(new ProductElement { Element = primary, IsPrimary = true });
                foreach (var code in row.SecondaryElements.Distinct(StringComparer.OrdinalIgnoreCase))
                    if (Enum.TryParse<FengShuiElement>(code, ignoreCase: true, out var sec) && sec != primary)
                        product.Elements.Add(new ProductElement { Element = sec, IsPrimary = false });
            }

            foreach (var code in row.Vibes.Distinct(StringComparer.OrdinalIgnoreCase).Where(knownVibes.Contains))
                product.Vibes.Add(new ProductVibe { VibeCode = code });

            var inputs = row.ElementInputs
                .Where(i => Enum.TryParse<ElementInputKind>(i.Kind, ignoreCase: true, out _))
                .Select(i => new ProductElementInput
                {
                    ProductId = product.Id,
                    InputKind = Enum.Parse<ElementInputKind>(i.Kind, ignoreCase: true),
                    InputCode = i.Code,
                })
                .ToList();

            // Cache vector ngũ hành ngay lúc seed — engine đọc 5 cột này thay vì tính lại mỗi lần chấm.
            var vector = ProductVectorProvider.Build(
                isOverridden: false, overriddenVector: null, inputs: inputs, resolver: resolver,
                productElements: product.Elements.Select(e => (e.Element, e.IsPrimary)), p: prms);
            product.ElementTho = vector.Tho;
            product.ElementKim = vector.Kim;
            product.ElementThuy = vector.Thuy;
            product.ElementMoc = vector.Moc;
            product.ElementHoa = vector.Hoa;

            await _context.Set<Product>().AddAsync(product, ct);
            if (inputs.Count > 0)
                await _context.Set<ProductElementInput>().AddRangeAsync(inputs, ct);

            existingNames.Add(row.Name);
            added++;
        }

        if (added > 0) await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seed {Count} sản phẩm demo placement {Placement} từ {File}.", added, placement, FileName);
    }

}
