namespace FengDeskAI.Infrastructure.ExternalServices.Ai;

public class AiRecommendationSettings
{
    public const string SectionName = "AiRecommendationSettings";

    /// <summary>True → dùng mock trong .NET; False → gọi AI microservice (Python) qua HTTP.</summary>
    public bool UseMock { get; set; } = true;

    /// <summary>Base URL của AI microservice, vd "http://localhost:8000".</summary>
    public string BaseUrl { get; set; } = "http://localhost:8000";

    /// <summary>Đường dẫn endpoint diễn giải.</summary>
    public string ExplainPath { get; set; } = "/recommendations/explain";

    /// <summary>Timeout (giây) cho 1 lần gọi AI.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Khóa xác thực service-to-service khi gọi AI microservice (KHÔNG phải key của LLM provider —
    /// key Anthropic/OpenAI nằm trong cấu hình của chính Python service). Bỏ trống → không gửi header.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>Tên header chứa <see cref="ApiKey"/>. Mặc định "x-api-key".</summary>
    public string ApiKeyHeader { get; set; } = "x-api-key";
}
