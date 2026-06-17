namespace FengDeskAI.Application.Features.CustomerCare;

/// <summary>
/// Cấu hình session chat AI (bind từ section "AiChat" trong appsettings).
/// Một class duy nhất: phần transport (BaseUrl/ChatPath/...) dùng bởi client trong Infrastructure,
/// phần policy (model + lịch sử) dùng bởi <c>AiChatService</c> trong Application.
/// </summary>
public sealed class AiChatOptions
{
    public const string SectionName = "AiChat";

    // ── Transport ────────────────────────────────────────────────────────────
    /// <summary>Base URL của LLM, vd "http://localhost:11434" hoặc URL ngrok.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Đường dẫn endpoint chat (Ollama: "/api/chat").</summary>
    public string ChatPath { get; set; } = "/api/chat";

    /// <summary>Timeout (giây) cho 1 lượt gọi — LLM local thường chậm, để rộng.</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Khoá xác thực gửi kèm header (nếu LLM yêu cầu). Bỏ trống → không gửi.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Tên header chứa <see cref="ApiKey"/>.</summary>
    public string ApiKeyHeader { get; set; } = "Authorization";

    // ── Policy ───────────────────────────────────────────────────────────────
    /// <summary>Model mặc định khi request không chỉ định.</summary>
    public string DefaultModel { get; set; } = "gemma3:4b";

    /// <summary>Danh sách model được phép đổi. Rỗng → chấp nhận mọi model client gửi.</summary>
    public List<string> AllowedModels { get; set; } = new();

    /// <summary>Số lượt (user+assistant) gần nhất được nhớ. Mặc định 5.</summary>
    public int MaxHistoryTurns { get; set; } = 5;

    /// <summary>System prompt định hướng trợ lý. Bỏ trống → không gắn.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>Thời gian sống của một phiên hội thoại trong cache (phút).</summary>
    public int SessionTtlMinutes { get; set; } = 60;
}
