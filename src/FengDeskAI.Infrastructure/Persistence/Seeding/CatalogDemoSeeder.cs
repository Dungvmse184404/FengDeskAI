using FengDeskAI.Application.Interfaces.Security;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Entities.Vendor;
using FengDeskAI.Domain.Enums;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed dữ liệu Catalog mẫu để test luồng cart/checkout: 1 vendor user + 1 store +
/// categories + tags + products (kèm items/ảnh/liên kết). Idempotent: bỏ qua nếu đã có product.
/// </summary>
public class CatalogDemoSeeder : IDataSeeder
{
    private const string VendorEmail = "vendor@fengdesk.local";
    private const string VendorPassword = "Vendor@123";

    private readonly AppDbContext _context;
    private readonly IPasswordService _passwords;
    private readonly ILogger<CatalogDemoSeeder> _logger;

    public CatalogDemoSeeder(AppDbContext context, IPasswordService passwords, ILogger<CatalogDemoSeeder> logger)
    {
        _context = context;
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

        var owner = await EnsureVendorAsync(ct);
        var store = new GardenStore
        {
            OwnerUserId = owner.Id,
            Name = "Vườn Phong Thủy Demo",
            Description = "Cửa hàng mẫu phục vụ test luồng đặt hàng.",
            Hotline = "1900 1234",
            OpeningHours = "08:00 - 21:00",
            IsActive = true,
        };
        await _context.Set<GardenStore>().AddAsync(store, ct);

        var categories = new Dictionary<string, Category>();
        foreach (var name in new[] { "Cây để bàn", "Đá phong thủy", "Đèn trang trí", "Tượng phong thủy" })
        {
            var cat = new Category { Name = name, IsActive = true };
            categories[name] = cat;
            await _context.Set<Category>().AddAsync(cat, ct);
        }

        var tags = new Dictionary<string, Tag>();
        foreach (var name in new[] { "Mộc", "Thủy", "Hỏa", "Thổ", "Kim", "Giảm căng thẳng", "Hút tài lộc" })
        {
            var tag = new Tag { Name = name };
            tags[name] = tag;
            await _context.Set<Tag>().AddAsync(tag, ct);
        }

        var products = new List<Product>
        {
            BuildProduct(store.Id, "Cây Kim Tiền để bàn", "Cây phong thủy hút tài lộc, hợp mệnh Mộc.",
                categories["Cây để bàn"], new[] { tags["Mộc"], tags["Hút tài lộc"] },
                ("Chậu sứ trắng", 250_000m, 30, "KT-WHITE"),
                ("Chậu sứ xanh", 280_000m, 20, "KT-BLUE")),

            BuildProduct(store.Id, "Cây Lưỡi Hổ mini", "Thanh lọc không khí, giảm căng thẳng.",
                categories["Cây để bàn"], new[] { tags["Mộc"], tags["Giảm căng thẳng"] },
                ("Size nhỏ", 150_000m, 50, "LH-S")),

            BuildProduct(store.Id, "Cầu thạch anh tím", "Đá phong thủy ổn định năng lượng.",
                categories["Đá phong thủy"], new[] { tags["Thổ"], tags["Giảm căng thẳng"] },
                ("Đường kính 6cm", 450_000m, 15, "TA-6"),
                ("Đường kính 8cm", 650_000m, 10, "TA-8")),

            BuildProduct(store.Id, "Tượng Tỳ Hưu đồng", "Linh vật chiêu tài, hợp mệnh Kim.",
                categories["Tượng phong thủy"], new[] { tags["Kim"], tags["Hút tài lộc"] },
                ("Cao 10cm", 890_000m, 8, "TH-10")),

            BuildProduct(store.Id, "Đèn muối Himalaya", "Ánh sáng ấm, thư giãn, hợp mệnh Hỏa.",
                categories["Đèn trang trí"], new[] { tags["Hỏa"], tags["Giảm căng thẳng"] },
                ("Loại 2-3kg", 320_000m, 25, "DM-23")),
        };
        await _context.Set<Product>().AddRangeAsync(products, ct);

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seed catalog demo xong: vendor {Email} / {Password}, store '{Store}', {Cat} categories, {Tag} tags, {Prod} products.",
            VendorEmail, VendorPassword, store.Name, categories.Count, tags.Count, products.Count);
    }

    private async Task<User> EnsureVendorAsync(CancellationToken ct)
    {
        var existing = await _context.Set<User>().FirstOrDefaultAsync(u => u.Email == VendorEmail, ct);
        if (existing is not null) return existing;

        var user = new User
        {
            Email = VendorEmail,
            PasswordHash = _passwords.Hash(VendorPassword),
            FullName = "Demo Vendor",
            Gender = Gender.Unspecified,
            Role = UserRole.Manager,
            IsActive = true,
        };
        await _context.Set<User>().AddAsync(user, ct);
        return user;
    }

    private static Product BuildProduct(
        Guid storeId, string name, string description,
        Category category, Tag[] tagList,
        params (string? Name, decimal Price, int Stock, string Sku)[] items)
    {
        var product = new Product
        {
            GardenStoreId = storeId,
            Name = name,
            Description = description,
            IsActive = true,
        };

        var slug = Uri.EscapeDataString(name);
        product.Images.Add(new ProductImage { Url = $"https://picsum.photos/seed/{slug}/600", SortOrder = 0 });

        foreach (var (itemName, price, stock, sku) in items)
            product.Items.Add(new ProductItem { Name = itemName, Price = price, Stock = stock, Sku = sku });

        product.ProductCategories.Add(new ProductCategory { CategoryId = category.Id });
        foreach (var tag in tagList)
            product.ProductTags.Add(new ProductTag { TagId = tag.Id });

        return product;
    }
}
