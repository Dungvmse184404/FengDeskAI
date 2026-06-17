namespace FengDeskAI.Application.Interfaces.External;

/// <summary>Vai trò của một tin nhắn trong hội thoại — khớp closed-set của LLM (Ollama/OpenAI).</summary>
public static class AiChatRoles
{
    public const string System = "system";
    public const string User = "user";
    public const string Assistant = "assistant";
}

/// <summary>
/// Một tin nhắn hội thoại trung lập với provider. <paramref name="Role"/> là role wire
/// (system/user/assistant). <paramref name="Images"/> là base64 thuần (Ollama field "images").
/// </summary>
public sealed record AiChatMessage(string Role, string Content, IReadOnlyList<string>? Images = null);

/// <summary>Kết quả 1 lượt hoàn thành từ LLM.</summary>
public sealed record AiChatCompletion(string Content, string Model);

/// <summary>
/// Cổng gọi LLM hội thoại (Ollama / OpenAI-compatible) — thuần transport, không giữ state.
/// Việc nhớ lịch sử + chọn model do <c>AiChatService</c> đảm nhiệm phía Application.
/// </summary>
public interface IAiChatClient
{
    Task<AiChatCompletion> CompleteAsync(
        string model, IReadOnlyList<AiChatMessage> messages, CancellationToken ct = default);
}
