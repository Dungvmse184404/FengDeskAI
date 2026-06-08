using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Sales;

/// <summary>Giỏ hàng — mỗi user có đúng một giỏ.</summary>
public class Cart : BaseEntity
{
    public Guid CustomerId { get; set; }

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
