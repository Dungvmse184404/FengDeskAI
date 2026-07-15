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

/// <summary>Trạng thái + kết quả model 3D của sản phẩm.</summary>
public class ProductModel3DResponse
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }

    /// <summary>Pending | Processing | Succeeded | Failed.</summary>
    public string Status { get; set; } = null!;
    public int Progress { get; set; }

    public string SourceImageUrl { get; set; } = null!;

    /// <summary>URL file GLB (đã re-host trên storage). Null tới khi Succeeded.</summary>
    public string? ModelUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Yêu cầu sinh model 3D. Bỏ trống <see cref="SourceImageId"/> → dùng ảnh primary (SortOrder nhỏ nhất).</summary>
public class GenerateModel3DRequest
{
    public Guid? SourceImageId { get; set; }
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
    /// <summary>Small/Medium/Large. Null nếu chưa khai báo.</summary>
    public string? SizeClass { get; set; }
    public List<string> Vibes { get; set; } = new();
    public List<string> Styles { get; set; } = new();

    /// <summary>Model 3D (nếu đã sinh). Null nếu sản phẩm chưa có.</summary>
    public ProductModel3DResponse? Model3D { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateProductItemRequest
{
    public string? Name { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string? Sku { get; set; }
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
    public SizeClass? SizeClass { get; set; }
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
}
