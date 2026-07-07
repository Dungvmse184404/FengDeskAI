using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Domain.Entities.Recommendation;

/// <summary>
/// Chất liệu / màu / hình khối vendor khai cho sản phẩm — nguồn auto-calc <c>productVector</c>
/// (chất liệu 60% + màu/hình 40%). Khi bộ input đổi, cache vector trên <c>products</c> tính lại.
/// </summary>
public class ProductElementInput : BaseEntity
{
    public Guid ProductId { get; set; }
    public ElementInputKind InputKind { get; set; }
    public string InputCode { get; set; } = null!;
}
