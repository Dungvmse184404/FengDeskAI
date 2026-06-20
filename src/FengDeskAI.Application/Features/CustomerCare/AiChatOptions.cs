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

    /// <summary>
    /// Cửa sổ ngữ cảnh (token) gửi Ollama qua options.num_ctx. Mặc định Ollama ~2048 → hội thoại dài
    /// (RoomContextMessages=30 + system prompt + tools) bị tràn → model trả rỗng → mất lượt trả lời.
    /// Đặt rộng để chứa lịch sử. 0 = không gửi (dùng mặc định model).
    /// </summary>
    public int NumCtx { get; set; } = 8192;

    /// <summary>
    /// Giữ model nóng trong RAM giữa các lượt (Ollama "keep_alive"), vd "30m", "1h", "-1" = vĩnh viễn.
    /// Bỏ trống → dùng mặc định của Ollama (~5 phút). Đây là cách giảm độ trễ load model hiệu quả nhất.
    /// </summary>
    public string? KeepAlive { get; set; } = "30m";

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

    /// <summary>Số tin gần nhất của CHÍNH phòng được nạp khi @AI ở phòng nhiều người (để nhớ ngữ cảnh trong phòng).</summary>
    public int RoomContextMessages { get; set; } = 30;

    /// <summary>
    /// Số ảnh GẦN NHẤT (tính trên toàn cửa sổ lịch sử) được encode base64 đưa cho LLM mỗi lượt.
    /// Trước đây chỉ encode ảnh của đúng tin cuối → khi user gửi ảnh rồi hỏi tiếp ở lượt sau, AI "mù" ảnh.
    /// Giữ ảnh "dính" với hội thoại như Messenger, nhưng giới hạn để khỏi tràn <see cref="NumCtx"/>. 0 = không encode.
    /// </summary>
    public int VisionMaxImages { get; set; } = 3;

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
    public int MaxToolIterations { get; set; } = 3;

    /// <summary>Lọc tool được phép (theo Name). Rỗng → cho phép tất cả tool đã đăng ký.</summary>
    public List<string> EnabledTools { get; set; } = new();
}
