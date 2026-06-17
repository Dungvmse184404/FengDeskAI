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
/// Cổng gọi LLM hội thoại (Ollama / OpenAI-compatible) — thuần transport, không giữ state.
/// Việc nhớ lịch sử + chọn model + vòng lặp tool do <c>AiChatService</c> đảm nhiệm phía Application.
/// </summary>
public interface IAiChatClient
{
    Task<AiChatCompletion> CompleteAsync(
        string model,
        IReadOnlyList<AiChatMessage> messages,
        IReadOnlyList<AiToolSpec>? tools = null,
        CancellationToken ct = default);
}
