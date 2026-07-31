namespace FengDeskAI.Infrastructure.ExternalServices.Model3D;

/// <summary>Cấu hình tích hợp Meshy AI (multi-image-to-3D). Bí mật (ApiKey) để trong secrets/Development.</summary>
public class MeshySettings
{
    public const string SectionName = "MeshySettings";

    /// <summary>Base URL của Meshy OpenAPI.</summary>
    public string BaseUrl { get; set; } = "https://api.meshy.ai";

    /// <summary>
    /// Đường dẫn endpoint multi-image-to-3D — dùng chung cho MỌI trường hợp (Initial 1 ảnh lẫn
    /// Regenerate nhiều ảnh, Meshy nhận 1–4 ảnh trên cùng endpoint này).
    /// </summary>
    public string MultiImageTo3DPath { get; set; } = "/openapi/v1/multi-image-to-3d";

    /// <summary>API key Meshy (Bearer). Bỏ trống → client vẫn resolve được nhưng gọi thật sẽ 401.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Timeout (giây) cho 1 request HTTP tới Meshy.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Chu kỳ worker nền poll trạng thái job (giây).</summary>
    public int PollIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// Backoff (phút) trước khi worker thử lại 1 request Initial sau khi Meshy trả 402 (hết credit).
    /// Xem <c>Model3DRequest.NextAttemptAt</c>.
    /// </summary>
    public int InsufficientCreditsBackoffMinutes { get; set; } = 10;

    // ----- Tham số render (gửi kèm khi tạo job) -----

    /// <summary>Model AI của Meshy, vd "meshy-5".</summary>
    public string AiModel { get; set; } = "meshy-5";

    /// <summary>Topology: "triangle" hoặc "quad".</summary>
    public string Topology { get; set; } = "triangle";

    /// <summary>Số polygon mục tiêu.</summary>
    public int TargetPolycount { get; set; } = 30000;

    /// <summary>Có sinh texture hay không.</summary>
    public bool ShouldTexture { get; set; } = true;
}
