using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Geography;

/// <summary>Quận/huyện — đơn vị hành chính cấp 2, thuộc một <see cref="Province"/>.</summary>
public class District : BaseEntity
{
    public Guid ProvinceId { get; set; }
    public string Name { get; set; } = null!;
    public int Code { get; set; }

    /// <summary>Mã quận/huyện theo GHN (DistrictID). Điền qua đồng bộ master-data GHN. Xem Documents/GHN_INTEGRATION.md §3.1.</summary>
    public int? GhnDistrictId { get; set; }

    public Province Province { get; set; } = null!;
    public ICollection<Ward> Wards { get; set; } = new List<Ward>();
}
