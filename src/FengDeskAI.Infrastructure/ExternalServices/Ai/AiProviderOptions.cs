namespace FengDeskAI.Infrastructure.ExternalServices.Ai;

/// <summary>
/// Cấu hình TRANSPORT tới LLM provider (bind từ section "Ai:Provider") — dùng chung cho mọi
/// chức năng AI (chatbox, workspace intake...). Cấu hình per-function (model, temperature)
/// nằm ở section riêng: "Ai:Chat", "Ai:Intake".
/// </summary>
public sealed class AiProviderOptions
{
    public const string SectionName = "Ai:Provider";

    /// <summary>Base URL của LLM, vd "http://localhost:11434" hoặc URL ngrok.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Đường dẫn endpoint chat (Ollama: "/api/chat").</summary>
    public string ChatPath { get; set; } = "/api/chat";

    /// <summary>Timeout (giây) cho 1 lượt gọi — LLM local thường chậm, để rộng.</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Cửa sổ ngữ cảnh (token) gửi Ollama qua options.num_ctx. Mặc định Ollama ~2048 → hội thoại dài
    /// (RoomContextMessages=30 + system prompt + tools) bị tràn → model trả rỗng → mất lượt trả lời.
    /// Đặt rộng để chứa lịch sử. 0 = không gửi (dùng mặc định model).
    /// </summary>
    public int NumCtx { get; set; } = 16384;

    /// <summary>
    /// Giữ model nóng trong RAM giữa các lượt (Ollama "keep_alive"), vd "30m", "1h", "-1" = vĩnh viễn.
    /// Bỏ trống → dùng mặc định của Ollama (~5 phút). Đây là cách giảm độ trễ load model hiệu quả nhất.
    /// </summary>
    public string? KeepAlive { get; set; } = "30m";

    /// <summary>Khoá xác thực gửi kèm header (nếu LLM yêu cầu). Bỏ trống → không gửi.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Tên header chứa <see cref="ApiKey"/>.</summary>
    public string ApiKeyHeader { get; set; } = "Authorization";
}
