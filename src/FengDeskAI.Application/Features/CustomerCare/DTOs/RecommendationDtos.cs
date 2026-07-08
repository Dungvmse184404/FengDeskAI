namespace FengDeskAI.Application.Features.CustomerCare.DTOs;

/// <summary>Yêu cầu tạo gợi ý cho một workspace đã lưu.</summary>
public sealed record GenerateRecommendationRequest
{
    public Guid WorkspaceProfileId { get; init; }

    /// <summary>Số sản phẩm muốn gợi ý (mặc định 8, kẹp 1..20).</summary>
    public int? TopN { get; init; }
}

public sealed record RecommendationResponse
{
    public Guid Id { get; init; }
    public string? CustomerElement { get; init; }
    public int? KuaNumber { get; init; }
    public string? KuaGroup { get; init; }
    public decimal PersonalWeight { get; init; }
    public string Status { get; init; } = null!;
    public string? Summary { get; init; }

    /// <summary>Chênh lệch ngũ hành lý tưởng vs hiện trạng phòng (engine v3). Null khi đọc lại phiên cũ.</summary>
    public GapBreakdownResponse? Gap { get; init; }

    public List<RecommendationItemResponse> Items { get; init; } = new();
}

/// <summary>Breakdown Gap = adjustedIdeal − current cho từng hành, để FE hiển thị phòng đang thiếu/thừa gì.</summary>
public sealed record GapBreakdownResponse
{
    public List<GapElementRow> Elements { get; init; } = new();
}

public sealed record GapElementRow
{
    public string Element { get; init; } = null!;
    public decimal Ideal { get; init; }
    public decimal Current { get; init; }
    public decimal Gap { get; init; }
}

public sealed record RecommendationItemResponse
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = null!;
    public decimal? Price { get; init; }
    public string? ImageUrl { get; init; }
    public decimal Score { get; init; }
    public int Rank { get; init; }
    public List<string> MatchFacts { get; init; } = new();
    public List<string> CautionFacts { get; init; } = new();

    /// <summary>Gợi ý hướng đặt vật phẩm (engine v3, Directional Validation). Null nếu không có.</summary>
    public string? PlacementHint { get; init; }

    public string? Explanation { get; init; }
}

/// <summary>
/// Độ phù hợp của 1 sản phẩm với 1 workspace (trang chi tiết sản phẩm). Khác <see cref="RecommendationResponse"/>:
/// KHÔNG loại sản phẩm — xung mệnh/lệch vibe chỉ phản ánh vào score/cautionFacts, luôn có kết quả.
/// </summary>
public sealed record ProductFitResponse
{
    public Guid ProductId { get; init; }
    public Guid WorkspaceProfileId { get; init; }

    /// <summary>Điểm phù hợp ∈ [-1,1]. Âm = xung khắc/lệch nhu cầu phòng, dương = phù hợp.</summary>
    public decimal Score { get; init; }

    public List<string> MatchFacts { get; init; } = new();
    public List<string> CautionFacts { get; init; } = new();
    public string? PlacementHint { get; init; }

    /// <summary>Vector ngũ hành của phòng (ideal/adjustedIdeal/current/gap) — cùng shape với element-analysis.</summary>
    public List<ElementAnalysisRow> Gap { get; init; } = new();

    /// <summary>Vector ngũ hành của sản phẩm (Σ=1) — để FE so sánh cạnh Gap (sản phẩm cấp gì vs phòng cần gì).</summary>
    public List<ProductElementRow> ProductVector { get; init; } = new();
}

public sealed record ProductElementRow
{
    /// <summary>Kim / Moc / Thuy / Hoa / Tho.</summary>
    public string Element { get; init; } = null!;
    public decimal Value { get; init; }
}
