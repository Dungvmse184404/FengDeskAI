using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Catalog;

namespace FengDeskAI.Domain.Entities.Sales;

/// <summary>
/// Dòng hàng trong order. Snapshot tên + đơn giá tại thời điểm đặt để lịch sử đơn
/// không đổi khi catalog thay đổi. Store của dòng suy ra qua <see cref="Delivery"/>.
/// DeliveryId null khi đơn online chưa thanh toán (delivery chỉ tạo sau khi đã trả tiền).
/// </summary>
public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    /// <summary>
    /// Biến thể đã bán. <b>Null khi Manager xoá cứng sản phẩm</b> (FK <c>ON DELETE SET NULL</c>) — mọi thứ đơn cần
    /// hiển thị đều nằm ở các cột chụp lại bên dưới, không đọc qua biến thể/sản phẩm (sản phẩm có thể bị xoá mềm/cứng).
    /// </summary>
    public Guid? ProductItemId { get; set; }
    public Guid? DeliveryId { get; set; }

    public string ProductName { get; set; } = null!;

    // ── Ảnh chụp lúc đặt (28/09/2026) — đơn hiển thị đủ dù sản phẩm sau đó bị xoá ──
    /// <summary>Mã sản phẩm lúc đặt — chỉ để dẫn link, KHÔNG có FK (sản phẩm có thể đã bị xoá cứng).</summary>
    public Guid? ProductId { get; set; }
    /// <summary>Cửa hàng bán món này — gom delivery lúc webhook PayOS và thống kê đọc từ đây.</summary>
    public Guid? GardenStoreId { get; set; }
    public string? VariantName { get; set; }
    public string? Sku { get; set; }
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    public Order Order { get; set; } = null!;
    public Delivery? Delivery { get; set; }
    public ProductItem? ProductItem { get; set; }
}
