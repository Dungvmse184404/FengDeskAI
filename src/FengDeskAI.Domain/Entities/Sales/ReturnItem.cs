using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Catalog;

namespace FengDeskAI.Domain.Entities.Sales;

/// <summary>
/// Dòng hàng trong một <see cref="ReturnRequest"/> — trả một <see cref="OrderItem"/> với số lượng cụ thể.
/// Snapshot <see cref="UnitPrice"/> tại thời điểm tạo yêu cầu để tính tiền hoàn không đổi.
/// Khi đổi hàng, <see cref="ExchangeProductItemId"/> trỏ tới biến thể thay thế.
/// </summary>
public class ReturnItem : BaseEntity
{
    public Guid ReturnRequestId { get; set; }
    public Guid OrderItemId { get; set; }

    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>Biến thể muốn đổi sang (chỉ dùng khi ReturnRequest.Type = Exchange).</summary>
    public Guid? ExchangeProductItemId { get; set; }

    public ReturnRequest ReturnRequest { get; set; } = null!;
    public OrderItem OrderItem { get; set; } = null!;
    public ProductItem? ExchangeProductItem { get; set; }
}
