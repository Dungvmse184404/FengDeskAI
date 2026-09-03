using FengDeskAI.Domain.Enums.Catalog;

namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>
/// Bảng nối Product ↔ <see cref="Aspiration"/> — "vật phẩm này phục vụ mục tiêu gì".
/// Junction thuần: composite key (product_id, aspiration), không audit/soft-delete — cùng pattern
/// <see cref="ProductVibe"/>/<see cref="ProductElement"/>.
/// <para>
/// Vendor đề xuất, admin bật <see cref="IsApproved"/>. <b>Chỉ dòng đã duyệt mới tham gia bộ lọc gợi ý</b> —
/// thẻ "Tài lộc" là lời hứa nghiệp vụ, không để vendor tự gắn bừa cho lên top.
/// </para>
/// </summary>
public class ProductAspiration
{
    public Guid ProductId { get; set; }
    public Aspiration Aspiration { get; set; }

    /// <summary>Admin đã duyệt chưa. False = vendor mới đề xuất, engine BỎ QUA.</summary>
    public bool IsApproved { get; set; }

    /// <summary>User id của admin/manager đã duyệt. Null khi chưa duyệt.</summary>
    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public Product Product { get; set; } = null!;
}
