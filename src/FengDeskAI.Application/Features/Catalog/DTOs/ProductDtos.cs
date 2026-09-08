using FengDeskAI.Application.Common.Models;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.Catalog.DTOs;

public class ProductItemResponse
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string? Sku { get; set; }
    /// <summary>Small/Medium/Large của CHÍNH biến thể này. Null nếu chưa khai báo.</summary>
    public string? SizeClass { get; set; }
    public int WeightGram { get; set; }
    public int LengthCm { get; set; }
    public int WidthCm { get; set; }
    public int HeightCm { get; set; }
}

public class ProductImageResponse
{
    public Guid Id { get; set; }
    public string Url { get; set; } = null!;
    public int SortOrder { get; set; }
}

/// <summary>Trạng thái + kết quả model 3D hiện tại của sản phẩm ("bản mới nhất đã Succeeded").</summary>
public class ProductModel3DResponse
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductImageId { get; set; }

    /// <summary>Pending | Processing | Succeeded | Failed.</summary>
    public string Status { get; set; } = null!;
    public int Progress { get; set; }

    public string SourceImageUrl { get; set; } = null!;

    /// <summary>URL file GLB (đã re-host trên storage). Null tới khi Succeeded.</summary>
    public string? ModelUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>Toggle hiển thị của owner/garden staff — false thì FE ẩn hẳn phần 3D (giữ nguyên dữ liệu).</summary>
    public bool IsEnabled { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CategoryRefResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}

/// <summary>Card sản phẩm trong danh sách (rút gọn). Kèm biến thể (giá + tồn kho) để FE/AI khỏi gọi chi tiết.</summary>
public class ProductListItemResponse
{
    public Guid Id { get; set; }
    public Guid GardenStoreId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
    public decimal? MinPrice { get; set; }
    public string? PrimaryImageUrl { get; set; }

    /// <summary>URL file GLB khi sản phẩm có model 3D xem được. Null nếu chưa có / owner đã tắt hiển thị.</summary>
    public string? Model3DUrl { get; set; }
    public string? Model3DThumbnailUrl { get; set; }

    /// <summary>Các biến thể (SKU) của sản phẩm — mỗi cái mang giá + tồn kho riêng.</summary>
    public List<ProductItemResponse> Items { get; set; } = new();
}

public class ProductDetailResponse
{
    public Guid Id { get; set; }
    public Guid GardenStoreId { get; set; }
    public string? StoreName { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public List<ProductItemResponse> Items { get; set; } = new();
    public List<ProductImageResponse> Images { get; set; } = new();
    public List<CategoryRefResponse> Categories { get; set; } = new();

    // ===== Thuộc tính phong thủy (thay cho tags) =====
    /// <summary>Hành chính (Kim/Moc/Thuy/Hoa/Tho). Null nếu chưa khai báo phong thủy.</summary>
    public string? PrimaryElement { get; set; }
    public List<string> SecondaryElements { get; set; } = new();

    /// <summary>Desk | Living | Carry | Consumable — quyết định engine chấm điểm thế nào.</summary>
    public string Placement { get; set; } = null!;

    public List<string> Vibes { get; set; } = new();
    public List<string> Styles { get; set; } = new();

    /// <summary>Mục tiêu phong thủy ĐÃ DUYỆT (Wealth/Career/Health/Relationship/Study) — thẻ chưa duyệt không lộ ra.</summary>
    public List<string> Aspirations { get; set; } = new();

    /// <summary>Các model 3D theo từng ảnh của sản phẩm.</summary>
    public List<ProductModel3DResponse> Models3D { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateProductItemRequest
{
    public string? Name { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string? Sku { get; set; }
    /// <summary>Small/Medium/Large của biến thể này (chậu mini vs chậu để sàn). Bỏ trống = chưa khai.</summary>
    public SizeClass? SizeClass { get; set; }
    /// <summary>Cân nặng (gram). Bỏ trống → 500g.</summary>
    public int WeightGram { get; set; } = 500;
    /// <summary>Kích thước kiện (cm) cho GHN. Bỏ trống → 10cm.</summary>
    public int LengthCm { get; set; } = 10;
    public int WidthCm { get; set; } = 10;
    public int HeightCm { get; set; } = 10;
}

public class UpdateProductItemRequest
{
    public string? Name { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string? Sku { get; set; }
    public SizeClass? SizeClass { get; set; }
    public int WeightGram { get; set; } = 500;
    public int LengthCm { get; set; } = 10;
    public int WidthCm { get; set; } = 10;
    public int HeightCm { get; set; } = 10;
}

public class CreateProductImageRequest
{
    public string Url { get; set; } = null!;
    public int SortOrder { get; set; }
}

public class CreateProductRequest
{
    public Guid GardenStoreId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public List<CreateProductItemRequest> Items { get; set; } = new();
    public List<CreateProductImageRequest> Images { get; set; } = new();
    public List<Guid> CategoryIds { get; set; } = new();

    // ===== Thuộc tính phong thủy (tùy chọn) — khai báo luôn khi tạo để thành ứng viên gợi ý. =====

    /// <summary>Tín hiệu vật lý (vật liệu/màu/hình khối) — nguồn auto-calc vector (tầng 2), ưu tiên hơn PrimaryElement.</summary>
    public List<ProductElementInputDto> ElementInputs { get; set; } = new();

    /// <summary>Đường advanced / fallback tầng 3 — chỉ dùng khi không có ElementInputs. Null → không gắn phong thủy khi tạo.</summary>
    public FengShuiElement? PrimaryElement { get; set; }
    /// <summary>Các hành phụ (0..n) của đường advanced. Trùng hành chính sẽ bị bỏ qua.</summary>
    public List<FengShuiElement> SecondaryElements { get; set; } = new();

    /// <summary>
    /// Vị trí/cách dùng — quyết định luồng gợi ý (đồ để bàn theo gap phòng, vật đeo theo bản mệnh,
    /// hàng tiêu hao không gợi ý). Bỏ trống → <c>Desk</c>.
    /// </summary>
    public ProductPlacement? Placement { get; set; }

    /// <summary>Mã vibe (vibes.code), vd "Focus".</summary>
    public List<string> Vibes { get; set; } = new();
    /// <summary>Mã phong cách (styles.code), vd "Minimal".</summary>
    public List<string> Styles { get; set; } = new();
}

public class UpdateProductRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class SetCategoriesRequest
{
    public List<Guid> CategoryIds { get; set; } = new();
}

/// <summary>Query params bind từ URL cho danh sách sản phẩm.</summary>
public class ProductQueryParams : PageRequest
{
    public Guid? StoreId { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Search { get; set; }

    /// <summary>Lọc theo hành phong thủy (Kim/Moc/Thuy/Hoa/Tho) — khớp cả hành chính lẫn hành phụ.</summary>
    public FengShuiElement? Element { get; set; }

    /// <summary>
    /// Lọc theo MỤC TIÊU phong thủy (Tài lộc / Sức khỏe…). Chỉ tính thẻ ĐÃ DUYỆT
    /// (<c>product_aspirations.is_approved</c>) — vendor tự gắn không đủ để lên kết quả.
    /// </summary>
    public Aspiration? Aspiration { get; set; }

    /// <summary>
    /// Lọc theo nhóm CÁCH DÙNG. Hai luồng gợi ý tách theo cột này nên tìm kiếm cũng cần lọc được:
    /// <c>Desk</c>/<c>Living</c> đặt trong phòng · <c>Carry</c> mang theo người · <c>Consumable</c> tiêu hao.
    /// </summary>
    public ProductPlacement? Placement { get; set; }

    /// <summary>true → chỉ trả sản phẩm có model 3D xem được. Trang chủ dùng để bốc ngẫu nhiên 1 model.</summary>
    public bool? HasModel3D { get; set; }
}
