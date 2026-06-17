using FengDeskAI.Contracts.Recommendation;

namespace FengDeskAI.Application.Interfaces.External;

/// <summary>
/// Cổng gọi AI microservice để diễn giải + sắp xếp gợi ý. Hiện có impl mock trong Infrastructure;
/// thay bằng HTTP client gọi Python sau. Phải tuân thủ luật trong Contracts/Recommendation/CONTRACT.md.
/// </summary>
public interface IAiRecommendationClient
{
    Task<AiRecommendationResponse> ExplainAsync(AiRecommendationRequest request, CancellationToken ct = default);
}
