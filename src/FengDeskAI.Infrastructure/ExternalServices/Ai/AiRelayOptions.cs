namespace FengDeskAI.Infrastructure.ExternalServices.Ai;

/// <summary>
/// Cấu hình 1 backend AI trong chuỗi relay. Thứ tự trong <see cref="AiRelayOptions.Providers"/> = độ ưu tiên
/// (phần tử đầu Enabled = primary). Đổi primary = đổi thứ tự / bật-tắt Enabled, KHÔNG đụng code.
/// </summary>
public sealed class AiProviderConfig
{
    /// <summary>Tên gợi nhớ để log (vd "ollama", "deepseek"). Không ảnh hưởng hành vi.</summary>
    public string Name { get; set; } = "provider";

    /// <summary>"Ollama" (wire /api/chat) | "OpenAiCompatible" (wire /chat/completions — DeepSeek/OpenRouter/Groq…).</summary>
    public string Type { get; set; } = "Ollama";

    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "";
    public string ChatPath { get; set; } = "/api/chat";

    /// <summary>Khóa API (bỏ trống nếu backend không cần, vd Ollama local). Lấy từ .env, KHÔNG commit.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Header chứa khóa. "Authorization" → gửi "Bearer {key}" (OpenAI/DeepSeek); khác → gửi raw.</summary>
    public string ApiKeyHeader { get; set; } = "Authorization";

    /// <summary>
    /// Model của provider này. Ollama: bỏ trống → dùng model do caller chọn (vd qwen3-vl khi có ảnh).
    /// Cloud: bắt buộc (vd "deepseek-chat") — bỏ qua tên model Ollama mà caller truyền vào.
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>Provider nhìn được ảnh? Request CÓ ảnh chỉ chạy trên provider SupportsVision=true (DeepSeek=false).</summary>
    public bool SupportsVision { get; set; } = true;

    public int TimeoutSeconds { get; set; } = 60;

    // ── Chỉ Ollama dùng ──
    public int NumCtx { get; set; }
    public string? KeepAlive { get; set; }

    /// <summary>
    /// URL endpoint TUYỆT ĐỐI = BaseUrl + ChatPath. Dùng thay vì HttpClient.BaseAddress + path tương đối —
    /// tránh bẫy: path bắt đầu bằng '/' sẽ ghi đè cả path của BaseUrl (mất /openai/v1, /v1beta/openai…).
    /// </summary>
    public string ChatUrl => $"{BaseUrl.TrimEnd('/')}/{ChatPath.TrimStart('/')}";
}

/// <summary>
/// Chuỗi relay AI (section "Ai:Relay"): thử các provider theo thứ tự cho tới khi 1 cái thành công.
/// Dùng cho cả chat lẫn intake qua <c>RelayChatClient</c> — không service nào phải biết có nhiều backend.
/// </summary>
public sealed class AiRelayOptions
{
    public const string SectionName = "Ai:Relay";

    public List<AiProviderConfig> Providers { get; set; } = new();
}
