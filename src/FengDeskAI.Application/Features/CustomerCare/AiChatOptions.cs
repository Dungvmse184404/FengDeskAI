namespace FengDeskAI.Application.Features.CustomerCare;

/// <summary>
/// Cấu hình AI cho chức năng CHATBOX (bind từ section "Ai:Chat").
/// Transport (BaseUrl/Timeout/...) nằm ở "Ai:Provider" (<c>AiProviderOptions</c>, Infrastructure);
/// intake/autofill nằm ở "Ai:Intake" (<c>WorkspaceIntakeOptions</c>).
/// </summary>
public sealed class AiChatOptions
{
    public const string SectionName = "Ai:Chat";

    public string DefaultModel { get; set; } = "qwen3.5:latest";

    public double? Temperature { get; set; }

    public bool? Think { get; set; } = true;

    public bool Stream { get; set; } = true;

    /// <summary>Danh sách model được phép đổi. Rỗng → chấp nhận mọi model client gửi.</summary>
    public List<string> AllowedModels { get; set; } = new();

    /// <summary>Số lượt (user+assistant) gần nhất được nhớ. Mặc định 5.</summary>
    public int MaxHistoryTurns { get; set; } = 5;

    /// <summary>Số tin gần nhất của CHÍNH phòng được nạp khi @AI ở phòng nhiều người (để nhớ ngữ cảnh trong phòng).</summary>
    public int RoomContextMessages { get; set; } = 10;

    /// <summary>
    /// Số ảnh GẦN NHẤT (tính trên toàn cửa sổ lịch sử) được encode base64 đưa cho LLM mỗi lượt.
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
    public int SharedContextRoomLimit { get; set; } = 0;

    /// <summary>Số tin gần nhất lấy từ mỗi phòng chung khi gom ngữ cảnh.</summary>
    public int SharedRoomMessages { get; set; } = 6;

    // ── Tool calling ───────────────────────────────────────────────────────────
    /// <summary>Bật function-calling (model phải hỗ trợ tools; không thì AI vẫn chat thường).</summary>
    public bool EnableTools { get; set; } = true;

    /// <summary>Số vòng gọi tool tối đa cho 1 lượt chat (chặn lặp vô hạn).</summary>
    public int MaxToolIterations { get; set; } = 10;

    /// <summary>Lọc tool được phép (theo Name). Rỗng → cho phép tất cả tool đã đăng ký.</summary>
    public List<string> EnabledTools { get; set; } = new();

    // ── Stall check (chống "hứa suông") ─────────────────────────────────────────
    /// <summary>
    /// Model NHỎ/NHANH riêng để xác nhận model chính có đang "hứa hẹn" gọi tool mà không emit tool_calls
    /// (vd "để mình kiểm tra nhé..."). Chỉ được gọi khi keyword heuristic (<c>LooksLikeToolStall</c>) đã
    /// nghi ngờ trước — dùng làm bộ xác nhận thứ 2, không thay thế heuristic hoàn toàn.
    /// Bỏ trống/null → tắt tính năng, giữ hành vi cũ (chỉ tin keyword heuristic).
    /// </summary>
    public string? StallCheckModel { get; set; }
}
