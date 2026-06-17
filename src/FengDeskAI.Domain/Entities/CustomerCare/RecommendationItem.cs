using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Catalog;

namespace FengDeskAI.Domain.Entities.CustomerCare;

/// <summary>
/// Một sản phẩm trong kết quả gợi ý. Giữ cả thứ hạng engine (<see cref="BaseRank"/>) và
/// thứ hạng cuối sau khi AI tinh chỉnh (<see cref="FinalRank"/>) để audit AI đã đảo gì.
/// </summary>
public class RecommendationItem : BaseEntity
{
    public Guid RecommendationId { get; set; }
    public Guid ProductId { get; set; }

    /// <summary>Điểm engine .NET chấm (deterministic).</summary>
    public decimal BaseScore { get; set; }

    /// <summary>Thứ hạng theo điểm engine (1 = cao nhất).</summary>
    public int BaseRank { get; set; }

    /// <summary>Thứ hạng cuối hiển thị cho khách (sau khi AI có thể đảo trong top-N).</summary>
    public int FinalRank { get; set; }

    /// <summary>Các "sự thật" tích cực engine tính ra, JSON array. AI giải thích dựa trên đây.</summary>
    public string MatchFacts { get; set; } = "[]";

    /// <summary>Các lưu ý/cảnh báo (vd "đã bỏ yếu tố hướng"), JSON array.</summary>
    public string? CautionFacts { get; set; }

    /// <summary>Đoạn diễn giải thuyết phục do AI sinh.</summary>
    public string? AiExplanation { get; set; }

    public Recommendation Recommendation { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
