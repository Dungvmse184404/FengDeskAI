using FengDeskAI.Application.Features.Workspace.DTOs;

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

    /// <summary>% phòng đúng chuẩn lý tưởng đã điều chỉnh theo mục đích + bản mệnh (0-100).</summary>
    public int CompatibilityPercent { get; init; }

    /// <summary>3 nhận định (status/detail/action) sinh ở BE theo case A/B/C.</summary>
    public SpaceInsights Insights { get; init; } = null!;

    // ===== Sản phẩm đã mua đặt vào phòng (tính lúc đọc, không lưu vector) =====

    /// <summary>true khi có sản phẩm CHƯA GIAO đặt trong phòng → FE vẽ thêm lớp radar preview (nét đứt).</summary>
    public bool HasPreview { get; init; }

    /// <summary>% tương thích của vector preview (gồm cả hàng đang giao). = CompatibilityPercent khi không có preview.</summary>
    public int PreviewCompatibilityPercent { get; init; }

    /// <summary>Danh sách sản phẩm đang đặt trong phòng (cả đã giao + đang giao).</summary>
    public List<PlacedProductResponse> PlacedProducts { get; init; } = new();
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

    /// <summary>Current NẾU tính cả sản phẩm chưa giao tới (= Current khi không có preview).</summary>
    public decimal PreviewCurrent { get; init; }

    /// <summary>AdjustedIdeal − PreviewCurrent.</summary>
    public decimal PreviewGap { get; init; }
}

/// <summary>Case: "Imbalanced" (A) | "Balanced" (B) | "Toxic" (C).</summary>
public sealed record SpaceInsights(string Case, IReadOnlyList<SpaceInsightLine> Lines);

/// <summary>Kind: "status" | "detail" | "action" — FE map icon theo Kind, Title đến từ BE.</summary>
public sealed record SpaceInsightLine(string Kind, string Title, string Text);
