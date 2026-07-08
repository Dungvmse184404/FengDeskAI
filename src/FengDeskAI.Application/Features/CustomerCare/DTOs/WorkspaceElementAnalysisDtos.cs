namespace FengDeskAI.Application.Features.CustomerCare.DTOs;

/// <summary>
/// Phân tích ngũ hành của một workspace (không chạy cả phiên recommendation) — cho FE hiển thị
/// "phòng của bạn đang thiếu/thừa hành gì".
/// </summary>
public sealed record WorkspaceElementAnalysisResponse
{
    public Guid WorkspaceProfileId { get; init; }

    /// <summary>Hành có gap dương lớn nhất (thiếu nhiều nhất).</summary>
    public string DominantNeed { get; init; } = null!;

    /// <summary>Từng hành, sắp giảm dần theo Gap (thiếu nhất → thừa nhất).</summary>
    public List<ElementAnalysisRow> Elements { get; init; } = new();
}

public sealed record ElementAnalysisRow
{
    /// <summary>Kim / Moc / Thuy / Hoa / Tho.</summary>
    public string Element { get; init; } = null!;
    public decimal Ideal { get; init; }
    public decimal AdjustedIdeal { get; init; }
    public decimal Current { get; init; }

    /// <summary>AdjustedIdeal − Current: + thiếu, − thừa.</summary>
    public decimal Gap { get; init; }
}
