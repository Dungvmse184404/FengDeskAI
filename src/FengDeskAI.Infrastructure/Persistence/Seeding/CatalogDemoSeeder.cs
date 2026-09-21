using FengDeskAI.Application.Interfaces.Security;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Entities.Vendor;
using FengDeskAI.Domain.Enums;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed dữ liệu Catalog mẫu để test luồng cart/checkout: 1 vendor user + 1 store + categories + tags
/// + products (kèm items/ảnh/liên kết). Data đọc từ <c>catalog-demo.json</c> (xem <see cref="CatalogDemoFile"/>),
/// không hardcode. Idempotent: bỏ qua nếu đã có product.
/// </summary>
public class CatalogDemoSeeder : IDataSeeder
{
    private const string FileName = "catalog-demo.json";

    private readonly AppDbContext _context;
    private readonly SeedDataLoader _loader;
    private readonly IPasswordService _passwords;
    private readonly ILogger<CatalogDemoSeeder> _logger;

    public CatalogDemoSeeder(
        AppDbContext context, SeedDataLoader loader, IPasswordService passwords, ILogger<CatalogDemoSeeder> logger)
    {
        _context = context;
        _loader = loader;
        _passwords = passwords;
        _logger = logger;
    }

    public int Order => 20;
    public string Name => "Catalog demo (vendor + store + categories/tags + products)";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _context.Set<Product>().AnyAsync(ct))
        {
            _logger.LogInformation("Catalog đã có product — bỏ qua seeding.");
            return;
        }

        var file = _loader.Load<CatalogDemoFile>(FileName);
        if (file.Products.Count == 0)
        {
            _logger.LogWarning("{File} không có sản phẩm nào — bỏ qua.", FileName);
            return;
        }

        var owner = await EnsureVendorAsync(file.Vendor, ct);

        var store = new GardenStore
        {
            Name = file.Store.Name,
            Description = file.Store.Description,
            Hotline = file.Store.Hotline,
            OpeningHours = file.Store.OpeningHours,
            IsActive = true,
        };
        store.Owners.Add(new GardenStoreOwner
        {
            OwnerUserId = owner.Id,
            IsPrimary = true,
            AssignedAt = DateTime.UtcNow,
        });
        await _context.Set<GardenStore>().AddAsync(store, ct);

        // Mỗi store có 1 địa chỉ (1-1). Gắn vào ward bất kỳ đã được GeographySeeder (Order 10) seed trước.
        var ward = await _context.Set<Ward>().OrderBy(w => w.Name).FirstOrDefaultAsync(ct);
        if (ward is not null)
        {
            await _context.Set<StoreAddress>().AddAsync(new StoreAddress
            {
                StoreId = store.Id,
                WardId = ward.Id,
                StreetAddress = file.Store.StreetAddress ?? "",
                IsActive = true,
            }, ct);
        }
        else
        {
            _logger.LogWarning("Chưa có dữ liệu Ward — bỏ qua seed địa chỉ cho store demo.");
        }

        var categories = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in file.Categories)
        {
            var cat = new Category { Name = name, IsActive = true };
            categories[name] = cat;
            await _context.Set<Category>().AddAsync(cat, ct);
        }

        foreach (var name in file.Tags)
            await _context.Set<Tag>().AddAsync(new Tag { Name = name }, ct);

        var products = new List<Product>();
        foreach (var row in file.Products)
        {
            if (string.IsNullOrWhiteSpace(row.Name)) continue;

            // Placement quyết định sản phẩm đi luồng gợi ý nào — khai sai/bỏ trống thì về Desk (mặc định an toàn).
            if (!Enum.TryParse<ProductPlacement>(row.Placement, ignoreCase: true, out var placement))
            {
                if (!string.IsNullOrWhiteSpace(row.Placement))
                    _logger.LogWarning("{File}: placement '{P}' của '{Name}' không hợp lệ — dùng Desk.",
                        FileName, row.Placement, row.Name);
                placement = ProductPlacement.Desk;
            }

            var product = new Product
            {
                GardenStoreId = store.Id,
                Name = row.Name,
                Description = row.Description,
                Placement = placement,
                IsActive = true,
            };

            var slug = Uri.EscapeDataString(row.Name);
            if (!string.IsNullOrWhiteSpace(file.ImageUrlTemplate))
            {
                product.Images.Add(new ProductImage
                {
                    Url = file.ImageUrlTemplate.Replace("{slug}", slug),
                    SortOrder = 0,
                });
            }

            // SizeClass nằm ở TỪNG SKU (migration MoveSizeClassToProductItem) — mỗi biến thể một giá trị,
            // không còn gán chung ở product cha nữa.
            foreach (var it in row.Items)
            {
                var item = new ProductItem
                {
                    Name = it.Name,
                    Price = it.Price,
                    Stock = it.Stock,
                    Sku = it.Sku,
                };
                if (Enum.TryParse<SizeClass>(it.SizeClass, ignoreCase: true, out var size))
                    item.SizeClass = size;
                product.Items.Add(item);
            }

            if (!string.IsNullOrWhiteSpace(row.Category) && categories.TryGetValue(row.Category, out var cat))
                product.ProductCategories.Add(new ProductCategory { CategoryId = cat.Id });
            else if (!string.IsNullOrWhiteSpace(row.Category))
                _logger.LogWarning("{File}: category '{Cat}' của '{Name}' không có trong danh sách categories.",
                    FileName, row.Category, row.Name);

            products.Add(product);
        }

        await _context.Set<Product>().AddRangeAsync(products, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seed catalog demo xong: vendor {Email} / {Password}, store '{Store}', {Cat} categories, {Tag} tags, {Prod} products.",
            file.Vendor.Email, file.Vendor.Password, store.Name, categories.Count, file.Tags.Count, products.Count);
    }

    private async Task<User> EnsureVendorAsync(CatalogDemoFile.VendorRow vendor, CancellationToken ct)
    {
        var existing = await _context.Set<User>().FirstOrDefaultAsync(u => u.Email == vendor.Email, ct);
        if (existing is not null) return existing;

        var user = new User
        {
            Email = vendor.Email,
            PasswordHash = _passwords.Hash(vendor.Password),
            FullName = vendor.FullName,
            Gender = Gender.Unspecified,
            Role = UserRole.Manager | UserRole.GardenOwner,
            IsActive = true,
        };
        await _context.Set<User>().AddAsync(user, ct);
        return user;
    }
}
