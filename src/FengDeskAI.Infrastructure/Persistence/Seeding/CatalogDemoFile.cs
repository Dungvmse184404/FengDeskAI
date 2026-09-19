namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Shape của <c>catalog-demo.json</c> — <b>một nguồn dữ liệu duy nhất</b> cho 3 seeder demo:
/// <see cref="CatalogDemoSeeder"/> (tạo vendor/store/category/tag/sản phẩm),
/// <see cref="ProductFengShuiDemoSeeder"/> (hành + vibe + style + size từng SKU),
/// <see cref="ProductElementInputDemoSeeder"/> (chất liệu/màu/hình → vector tầng 2).
/// <para>
/// Trước đây mỗi seeder giữ bảng riêng và khớp sản phẩm theo <b>chuỗi con trong tên</b>
/// ("Kim Tiền" ⊂ "Cây Kim Tiền để bàn"). Đổi tên một sản phẩm là hai seeder kia âm thầm không khớp
/// nữa, không lỗi, chỉ là sản phẩm thiếu thuộc tính. Gom về một file để tên chỉ khai đúng một chỗ
/// và khớp bằng <b>tên đầy đủ</b>.
/// </para>
/// </summary>
public sealed class CatalogDemoFile
{
    public VendorRow Vendor { get; set; } = new();
    public StoreRow Store { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public List<string> Tags { get; set; } = new();

    /// <summary><c>{slug}</c> được thay bằng tên sản phẩm đã URL-encode.</summary>
    public string ImageUrlTemplate { get; set; } = "";

    /// <summary>Áp cho sản phẩm KHÔNG có trong <see cref="Products"/> mà chưa khai phong thủy.</summary>
    public DefaultsRow Defaults { get; set; } = new();

    public List<ProductRow> Products { get; set; } = new();

    /// <summary>Tra sản phẩm theo tên đầy đủ, không phân biệt hoa/thường.</summary>
    public IReadOnlyDictionary<string, ProductRow> ByName()
        => Products
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    public sealed class VendorRow
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string FullName { get; set; } = "";
    }

    public sealed class StoreRow
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? Hotline { get; set; }
        public string? OpeningHours { get; set; }
        public string? StreetAddress { get; set; }
    }

    public sealed class DefaultsRow
    {
        public string PrimaryElement { get; set; } = "Tho";
        public string Vibe { get; set; } = "Focus";
        public string Style { get; set; } = "Modern";
        public string SizeClass { get; set; } = "Medium";
    }

    public sealed class ProductRow
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? Category { get; set; }

        /// <summary>Desk | Living | Carry | Consumable. Bỏ trống → <c>Desk</c>.</summary>
        public string? Placement { get; set; }

        public string? PrimaryElement { get; set; }
        public List<string> SecondaryElements { get; set; } = new();
        public List<string> Vibes { get; set; } = new();
        public List<string> Styles { get; set; } = new();
        public List<InputRow> ElementInputs { get; set; } = new();
        public List<ItemRow> Items { get; set; } = new();
    }

    public sealed class InputRow
    {
        /// <summary>Color | Material | Shape | DecorItem.</summary>
        public string Kind { get; set; } = "";
        public string Code { get; set; } = "";
    }

    public sealed class ItemRow
    {
        public string? Name { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string? Sku { get; set; }

        /// <summary>Small | Medium | Large — của CHÍNH biến thể này, không phải của sản phẩm cha.</summary>
        public string? SizeClass { get; set; }
    }
}
