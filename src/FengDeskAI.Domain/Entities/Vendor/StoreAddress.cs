using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Geography;

namespace FengDeskAI.Domain.Entities.Vendor;

/// <summary>Địa chỉ của một garden store (quan hệ 1-1 với store).</summary>
public class StoreAddress : BaseEntity
{
    public Guid StoreId { get; set; }
    public Guid WardId { get; set; }

    public string StreetAddress { get; set; } = null!;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Người gửi cho GHN (from_name). Hotline store có thể là số 1900 không hợp lệ.</summary>
    public string? SenderName { get; set; }
    /// <summary>SĐT người gửi cho GHN (from_phone) — cần số di động hợp lệ.</summary>
    public string? SenderPhone { get; set; }

    public GardenStore Store { get; set; } = null!;
    public Ward Ward { get; set; } = null!;
}
