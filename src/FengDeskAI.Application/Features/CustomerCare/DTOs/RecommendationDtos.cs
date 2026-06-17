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
    public List<RecommendationItemResponse> Items { get; init; } = new();
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
    public string? Explanation { get; init; }
}
