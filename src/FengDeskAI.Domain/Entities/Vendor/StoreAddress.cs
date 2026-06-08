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

    public GardenStore Store { get; set; } = null!;
    public Ward Ward { get; set; } = null!;
}
