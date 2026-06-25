using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>
/// Biến thể/SKU của một <see cref="Product"/> — đơn vị mang giá và tồn kho.
/// Cart và Order tham chiếu trực tiếp tới product item này.
/// </summary>
public class ProductItem : BaseEntity
{
    public Guid ProductId { get; set; }

    /// <summary>Tên biến thể, vd "Đỏ / Size L".</summary>
    public string? Name { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string? Sku { get; set; }

    // ===== Thông số kiện hàng cho vận chuyển (GHN yêu cầu weight + kích thước) =====
    /// <summary>Cân nặng (gram). Mặc định 500g.</summary>
    public int WeightGram { get; set; } = 500;
    /// <summary>Chiều dài (cm). Mặc định 10cm.</summary>
    public int LengthCm { get; set; } = 10;
    /// <summary>Chiều rộng (cm). Mặc định 10cm.</summary>
    public int WidthCm { get; set; } = 10;
    /// <summary>Chiều cao (cm). Mặc định 10cm.</summary>
    public int HeightCm { get; set; } = 10;

    public Product Product { get; set; } = null!;
}
