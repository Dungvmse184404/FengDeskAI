using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.CustomerCare;

/// <summary>
/// Nhật ký từng bước của một phiên gợi ý (engine chấm, gọi AI, AI trả về, AI đảo thứ tự, lỗi)
/// — phục vụ debug & audit hành vi AI.
/// </summary>
public class RecommendationLog : BaseEntity
{
    public Guid RecommendationId { get; set; }

    /// <summary>Nhãn giai đoạn, vd "EngineScored", "AiRequested", "AiReordered", "Error".</summary>
    public string Stage { get; set; } = null!;

    /// <summary>Chi tiết (payload/response/diff) dạng JSON hoặc text.</summary>
    public string? Detail { get; set; }

    public Recommendation Recommendation { get; set; } = null!;
}
