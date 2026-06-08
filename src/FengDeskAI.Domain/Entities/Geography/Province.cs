using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Geography;

/// <summary>Tỉnh/thành phố — đơn vị hành chính cấp 1.</summary>
public class Province : BaseEntity
{
    public string Name { get; set; } = null!;
    public int Code { get; set; }

    public ICollection<District> Districts { get; set; } = new List<District>();
}
