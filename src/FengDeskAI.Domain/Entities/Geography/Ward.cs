using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Geography;

/// <summary>Phường/xã — đơn vị hành chính cấp 3, thuộc một <see cref="District"/>.</summary>
public class Ward : BaseEntity
{
    public Guid DistrictId { get; set; }
    public string Name { get; set; } = null!;
    public int Code { get; set; }

    public District District { get; set; } = null!;
}
