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

    public Product Product { get; set; } = null!;
}
