namespace FengDeskAI.Domain.Enums.Recommendation;

/// <summary>Trạng thái một phiên gợi ý sản phẩm.</summary>
public enum RecommendationStatus
{
    /// <summary>Engine .NET đã chấm điểm xong, chưa gọi/đợi AI giải thích.</summary>
    Scored,

    /// <summary>AI đã trả về diễn giải + thứ tự cuối — hoàn tất.</summary>
    Completed,

    /// <summary>Lỗi trong quá trình chấm điểm hoặc gọi AI.</summary>
    Failed,
}
