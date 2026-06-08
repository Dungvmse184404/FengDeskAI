using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Geography;

/// <summary>
/// Địa chỉ giao hàng của user. Một user có nhiều địa chỉ, một địa chỉ mặc định.
/// Order tham chiếu địa chỉ này làm nơi giao (shipping_address_id).
/// </summary>
public class UserAddress : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid WardId { get; set; }

    public string StreetAddress { get; set; } = null!;
    public string RecipientName { get; set; } = null!;
    public string RecipientPhone { get; set; } = null!;

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public bool IsDefault { get; set; }

    /// <summary>Nhãn do user đặt: "Nhà", "Công ty", ...</summary>
    public string? Label { get; set; }

    public Ward Ward { get; set; } = null!;
}
