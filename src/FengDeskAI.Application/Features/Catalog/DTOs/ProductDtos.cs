using FengDeskAI.Application.Common.Models;

namespace FengDeskAI.Application.Features.Catalog.DTOs;

public class ProductItemResponse
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string? Sku { get; set; }
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

public class TagRefResponse
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
    public List<TagRefResponse> Tags { get; set; } = new();

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
}

public class UpdateProductItemRequest
{
    public string? Name { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string? Sku { get; set; }
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
    public List<Guid> TagIds { get; set; } = new();
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

public class SetTagsRequest
{
    public List<Guid> TagIds { get; set; } = new();
}

/// <summary>Query params bind từ URL cho danh sách sản phẩm.</summary>
public class ProductQueryParams : PageRequest
{
    public Guid? StoreId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? TagId { get; set; }
    public string? Search { get; set; }
}
