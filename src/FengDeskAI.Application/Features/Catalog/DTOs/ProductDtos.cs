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

/// <summary>Card sản phẩm trong danh sách (rút gọn).</summary>
public class ProductListItemResponse
{
    public Guid Id { get; set; }
    public Guid GardenStoreId { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
    public decimal? MinPrice { get; set; }
    public string? PrimaryImageUrl { get; set; }
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
