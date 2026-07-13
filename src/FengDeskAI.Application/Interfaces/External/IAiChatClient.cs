namespace FengDeskAI.Application.Interfaces.External;

/// <summary>Vai trò của một tin nhắn trong hội thoại — khớp closed-set của LLM (Ollama/OpenAI).</summary>
public static class AiChatRoles
{
    public const string System = "system";
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string Tool = "tool";
}

/// <summary>
/// Một tin nhắn hội thoại trung lập với provider. <paramref name="Images"/> là base64 thuần (Ollama "images").
/// <paramref name="ToolCalls"/>: khi role=assistant echo lại lời gọi tool. <paramref name="ToolName"/>: khi role=tool, tên tool đã chạy.
/// </summary>
public sealed record AiChatMessage(
    string Role,
    string Content,
    IReadOnlyList<string>? Images = null,
    IReadOnlyList<AiToolCall>? ToolCalls = null,
    string? ToolName = null);

/// <summary>Kết quả 1 lượt hoàn thành từ LLM. <see cref="ToolCalls"/> khác rỗng → cần chạy tool rồi gọi lại.</summary>
public sealed record AiChatCompletion(string Content, string Model, IReadOnlyList<AiToolCall>? ToolCalls = null);

/// <summary>
/// Tinh chỉnh một lượt gọi cụ thể — dùng cho tác vụ trích xuất có cấu trúc (vd workspace intake) cần
/// output ổn định/deterministic hơn hội thoại tự do. Bỏ trống → theo mặc định của model/provider.
/// </summary>
/// <param name="Think">Bật/tắt thinking của model (Ollama "think"). null = theo mặc định model.
/// Tắt (false) giúp model nhỏ đỡ "lạc" câu trả lời vào thinking block.</param>
/// <param name="Stream">Ollama "stream":true — đọc phản hồi theo từng chunk (NDJSON) thay vì đợi
/// 1 lần rồi gộp lại, KHÔNG đổi contract phía trên (vẫn trả về 1 <see cref="AiChatCompletion"/> đầy
/// đủ). Mục đích duy nhất: giữ traffic chảy liên tục qua proxy/tunnel (vd ngrok free) có ngắt kết nối
/// khi im lặng quá lâu — câu trả lời càng dài (ảnh, tool nhiều bước...) càng dễ dính nếu stream=false.</param>
public sealed record AiCompletionOptions(
    double? Temperature = null, bool JsonMode = false, bool? Think = null, bool Stream = false);

/// <summary>
/// Cổng gọi LLM hội thoại (Ollama / OpenAI-compatible) — thuần transport, không giữ state.
/// Việc nhớ lịch sử + chọn model + vòng lặp tool do <c>AiChatService</c> đảm nhiệm phía Application.
/// </summary>
public interface IAiChatClient
{
    Task<AiChatCompletion> CompleteAsync(
        string model,
        IReadOnlyList<AiChatMessage> messages,
        IReadOnlyList<AiToolSpec>? tools = null,
        AiCompletionOptions? options = null,
        CancellationToken ct = default);
}
