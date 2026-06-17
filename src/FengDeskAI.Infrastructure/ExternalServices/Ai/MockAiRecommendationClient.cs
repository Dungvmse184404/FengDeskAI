using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Contracts.Recommendation;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.ExternalServices.Ai;

/// <summary>
/// Mock của AI microservice — tuân thủ <c>Contracts/Recommendation/CONTRACT.md</c>:
/// chỉ diễn giải dựa trên MatchFacts/CautionFacts, GIỮ NGUYÊN tập sản phẩm, không bịa luật.
/// Giữ thứ tự engine (FinalRank = BaseRank). Thay bằng HTTP client gọi Python khi sẵn sàng.
/// </summary>
public sealed class MockAiRecommendationClient : IAiRecommendationClient
{
    private readonly ILogger<MockAiRecommendationClient> _logger;

    public MockAiRecommendationClient(ILogger<MockAiRecommendationClient> logger)
    {
        _logger = logger;
    }

    public Task<AiRecommendationResponse> ExplainAsync(AiRecommendationRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("[MockAI] Diễn giải {Count} ứng viên cho mệnh {Element}.",
            request.Candidates.Count, request.Customer.Element ?? "(không xác định)");

        // Luật: KHÔNG đổi tập sản phẩm, chỉ giữ nguyên thứ tự engine.
        var items = request.Candidates
            .OrderBy(c => c.BaseRank)
            .Select(c => new AiExplainedItem
            {
                ProductId = c.ProductId,
                FinalRank = c.BaseRank,
                Explanation = BuildExplanation(c),
            })
            .ToList();

        return Task.FromResult(new AiRecommendationResponse
        {
            Summary = BuildSummary(request),
            Items = items,
        });
    }

    private static string BuildExplanation(AiCandidate c)
    {
        var parts = new List<string> { $"**{c.Name}** là lựa chọn đáng cân nhắc." };

        if (c.MatchFacts.Count > 0)
            parts.Add(string.Join(" ", c.MatchFacts));
        else
            parts.Add("Phù hợp về công năng cho không gian của bạn.");

        if (c.CautionFacts.Count > 0)
            parts.Add("Lưu ý: " + string.Join(" ", c.CautionFacts));

        parts.Add("Một điểm nhấn vừa hợp phong thủy vừa nâng tầm bàn làm việc — rất đáng để sở hữu.");
        return string.Join(" ", parts);
    }

    private static string BuildSummary(AiRecommendationRequest req)
    {
        int n = req.Candidates.Count;
        return req.Customer.Element is { } element
            ? $"Dựa trên bản mệnh {element} và không gian \"{req.Workspace.Type}\", đây là {n} sản phẩm hợp phong thủy nhất dành cho bạn."
            : $"Dựa trên công năng của không gian \"{req.Workspace.Type}\", đây là {n} sản phẩm phù hợp nhất dành cho bạn.";
    }
}
