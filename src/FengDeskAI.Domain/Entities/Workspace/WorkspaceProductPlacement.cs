using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Sales;

namespace FengDeskAI.Domain.Entities.Workspace;

/// <summary>
/// Sản phẩm ĐÃ MUA được user "đặt" vào một workspace — chỉ lưu mapping, KHÔNG lưu vector gộp
/// (vector Current + sản phẩm tính lại mỗi lần đọc element-analysis).
/// 1 order item chỉ nằm ở tối đa 1 workspace (unique). Trạng thái preview/thật suy từ
/// Delivery.Status của order item lúc đọc: chưa Delivered → chỉ tính vào vector preview.
/// </summary>
public class WorkspaceProductPlacement : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid WorkspaceProfileId { get; set; }
    public Guid OrderItemId { get; set; }
    /// <summary>Denormalize từ OrderItem→ProductItem→ProductId để build vector không phải join sâu.</summary>
    public Guid ProductId { get; set; }
    public DateTime PlacedAt { get; set; }

    public WorkspaceProfile WorkspaceProfile { get; set; } = null!;
    public OrderItem OrderItem { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
