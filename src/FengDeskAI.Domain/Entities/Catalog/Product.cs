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

    /// <summary>
    /// Vị trí/cách sử dụng — quyết định engine chấm điểm thế nào (đồ để bàn theo gap phòng, vật đeo
    /// theo bản mệnh, hàng tiêu hao không gợi ý). Mặc định <see cref="ProductPlacement.Desk"/> nên
    /// catalog cũ giữ nguyên hành vi; NOT NULL, không có trạng thái "chưa khai".
    /// </summary>
    public ProductPlacement Placement { get; set; } = ProductPlacement.Desk;

    /// <summary>Các hành phong thủy của sản phẩm (nhiều-nhiều). Đúng một dòng IsPrimary = hành chính.</summary>
    public ICollection<ProductElement> Elements { get; set; } = new List<ProductElement>();
    public ICollection<ProductVibe> Vibes { get; set; } = new List<ProductVibe>();

    /// <summary>
    /// Mục tiêu phong thủy vật phẩm phục vụ (tài lộc, sức khỏe…). Dùng để LỌC ứng viên khi người dùng
    /// nêu ý định; chỉ dòng <see cref="ProductAspiration.IsApproved"/> mới được tính.
    /// </summary>
    public ICollection<ProductAspiration> Aspirations { get; set; } = new List<ProductAspiration>();
    public ICollection<ProductStyle> Styles { get; set; } = new List<ProductStyle>();

    /// <summary>Các model 3D theo từng ảnh/kiểu dáng của sản phẩm.</summary>
    public ICollection<ProductModel3D> Models3D { get; set; } = new List<ProductModel3D>();

    /// <summary>Lịch sử yêu cầu sinh/tạo lại model 3D (n–1). Xem <see cref="Model3DRequest"/>.</summary>
    public ICollection<Model3DRequest> Model3DRequests { get; set; } = new List<Model3DRequest>();

    // ── Cache vector ngũ hành (engine v3) — 5 cột numeric(4,3), Σ≈1 khi đã tính ──
    public decimal? ElementTho { get; set; }
    public decimal? ElementKim { get; set; }
    public decimal? ElementThuy { get; set; }
    public decimal? ElementMoc { get; set; }
    public decimal? ElementHoa { get; set; }

    /// <summary>True → dùng 5 cột vector admin/vendor nhập tay, bỏ auto-calc (tầng 1 fallback).</summary>
    public bool IsVectorOverridden { get; set; }
}
