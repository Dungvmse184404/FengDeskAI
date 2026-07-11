namespace FengDeskAI.Application.Features.CustomerCare;

/// <summary>
/// Cấu hình AI cho chức năng CHATBOX (bind từ section "Ai:Chat").
/// Transport (BaseUrl/Timeout/...) nằm ở "Ai:Provider" (<c>AiProviderOptions</c>, Infrastructure);
/// intake/autofill nằm ở "Ai:Intake" (<c>WorkspaceIntakeOptions</c>).
/// </summary>
public sealed class AiChatOptions
{
    public const string SectionName = "Ai:Chat";

    /// <summary>Model mặc định khi request không chỉ định.</summary>
    public string DefaultModel { get; set; } = "gemma3:4b";

    /// <summary>Temperature cho hội thoại tự do. null = theo mặc định của model/provider.</summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// Bật/tắt thinking của model (Ollama "think", model hỗ trợ như qwen3). null = theo mặc định model.
    /// false → model trả lời thẳng vào content, hết bệnh "lạc" câu trả lời vào thinking block.
    /// </summary>
    public bool? Think { get; set; } = true;
    /// <summary>Danh sách model được phép đổi. Rỗng → chấp nhận mọi model client gửi.</summary>
    public List<string> AllowedModels { get; set; } = new();

    /// <summary>Số lượt (user+assistant) gần nhất được nhớ. Mặc định 5.</summary>
    public int MaxHistoryTurns { get; set; } = 5;

    /// <summary>Số tin gần nhất của CHÍNH phòng được nạp khi @AI ở phòng nhiều người (để nhớ ngữ cảnh trong phòng).</summary>
    public int RoomContextMessages { get; set; } = 10;

    /// <summary>
    /// Số ảnh GẦN NHẤT (tính trên toàn cửa sổ lịch sử) được encode base64 đưa cho LLM mỗi lượt.
    /// Trước đây chỉ encode ảnh của đúng tin cuối → khi user gửi ảnh rồi hỏi tiếp ở lượt sau, AI "mù" ảnh.
    /// Giữ ảnh "dính" với hội thoại như Messenger, nhưng giới hạn để khỏi tràn <see cref="NumCtx"/>. 0 = không encode.
    /// </summary>
    public int VisionMaxImages { get; set; } = 1;

    /// <summary>System prompt định hướng trợ lý. Bỏ trống → không gắn.</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Giới hạn độ dài câu trả lời AI khi @AI trong phòng nhỏ (widget). Prompt sẽ yêu cầu ≤ (giá trị này − 100)
    /// để chừa biên an toàn. Trang AI lớn (assistant) KHÔNG áp giới hạn này. 0 = không giới hạn.
    /// </summary>
    public int RoomReplyMaxChars { get; set; } = 1200;

    /// <summary>Thời gian sống của một phiên hội thoại trong cache (phút).</summary>
    public int SessionTtlMinutes { get; set; } = 60;

    /// <summary>Số phòng "chung" tối đa được nạp làm ngữ cảnh khi AI trả lời ở phòng riêng (chống phình token).</summary>
    public int SharedContextRoomLimit { get; set; } = 3;

    /// <summary>Số tin gần nhất lấy từ mỗi phòng chung khi gom ngữ cảnh.</summary>
    public int SharedRoomMessages { get; set; } = 6;

    // ── Tool calling ───────────────────────────────────────────────────────────
    /// <summary>Bật function-calling (model phải hỗ trợ tools; không thì AI vẫn chat thường).</summary>
    public bool EnableTools { get; set; } = true;

    /// <summary>Số vòng gọi tool tối đa cho 1 lượt chat (chặn lặp vô hạn).</summary>
    public int MaxToolIterations { get; set; } = 5;

    /// <summary>Lọc tool được phép (theo Name). Rỗng → cho phép tất cả tool đã đăng ký.</summary>
    public List<string> EnabledTools { get; set; } = new();
}
