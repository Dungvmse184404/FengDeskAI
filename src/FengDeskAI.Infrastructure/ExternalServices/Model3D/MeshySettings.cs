namespace FengDeskAI.Infrastructure.ExternalServices.Model3D;

/// <summary>Cấu hình tích hợp Meshy AI (image-to-3D). Bí mật (ApiKey) để trong secrets/Development.</summary>
public class MeshySettings
{
    public const string SectionName = "MeshySettings";

    /// <summary>True → dùng mock (không gọi Meshy, không tốn credit); False → gọi Meshy thật.</summary>
    public bool UseMock { get; set; } = true;

    /// <summary>Base URL của Meshy OpenAPI.</summary>
    public string BaseUrl { get; set; } = "https://api.meshy.ai";

    /// <summary>Đường dẫn endpoint image-to-3D.</summary>
    public string ImageTo3DPath { get; set; } = "/openapi/v1/image-to-3d";

    /// <summary>API key Meshy (Bearer). Bỏ trống → client vẫn resolve được nhưng gọi thật sẽ 401.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Timeout (giây) cho 1 request HTTP tới Meshy.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Chu kỳ worker nền poll trạng thái job (giây).</summary>
    public int PollIntervalSeconds { get; set; } = 15;

    // ----- Tham số render (gửi kèm khi tạo job) -----

    /// <summary>Model AI của Meshy, vd "meshy-5".</summary>
    public string AiModel { get; set; } = "meshy-5";

    /// <summary>Topology: "triangle" hoặc "quad".</summary>
    public string Topology { get; set; } = "triangle";

    /// <summary>Số polygon mục tiêu.</summary>
    public int TargetPolycount { get; set; } = 30000;

    /// <summary>Có sinh texture hay không.</summary>
    public bool ShouldTexture { get; set; } = true;

    /// <summary>URL mock trả về khi <see cref="UseMock"/> = true (một file GLB công khai để demo).</summary>
    public string MockGlbUrl { get; set; } = "https://modelviewer.dev/shared-assets/models/Astronaut.glb";
}
