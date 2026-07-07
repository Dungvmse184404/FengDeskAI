using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Vendor;
using FengDeskAI.Domain.Enums.Catalog;

namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>
/// Sản phẩm (mặt hàng cha) thuộc một garden store. Giá + tồn kho nằm ở các
/// <see cref="ProductItem"/> (biến thể/SKU). Thuộc tính phong thủy khai báo qua
/// <see cref="Elements"/>/<see cref="Vibes"/>/<see cref="Styles"/> (tags đã ngừng dùng).
/// </summary>
public class Product : BaseEntity
{
    public Guid GardenStoreId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public GardenStore Store { get; set; } = null!;
    public ICollection<ProductItem> Items { get; set; } = new List<ProductItem>();
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();

    /// <summary>Phân loại kích thước (so với diện tích mặt bàn khi chấm điểm). Null nếu chưa khai báo phong thủy.</summary>
    public SizeClass? SizeClass { get; set; }

    /// <summary>Các hành phong thủy của sản phẩm (nhiều-nhiều). Đúng một dòng IsPrimary = hành chính.</summary>
    public ICollection<ProductElement> Elements { get; set; } = new List<ProductElement>();
    public ICollection<ProductVibe> Vibes { get; set; } = new List<ProductVibe>();
    public ICollection<ProductStyle> Styles { get; set; } = new List<ProductStyle>();

    /// <summary>Model 3D sinh từ ảnh (1–1). Null nếu chưa yêu cầu sinh.</summary>
    public ProductModel3D? Model3D { get; set; }

    // ── Cache vector ngũ hành (engine v3) — 5 cột numeric(4,3), Σ≈1 khi đã tính ──
    public decimal? ElementTho { get; set; }
    public decimal? ElementKim { get; set; }
    public decimal? ElementThuy { get; set; }
    public decimal? ElementMoc { get; set; }
    public decimal? ElementHoa { get; set; }

    /// <summary>True → dùng 5 cột vector admin/vendor nhập tay, bỏ auto-calc (tầng 1 fallback).</summary>
    public bool IsVectorOverridden { get; set; }
}
