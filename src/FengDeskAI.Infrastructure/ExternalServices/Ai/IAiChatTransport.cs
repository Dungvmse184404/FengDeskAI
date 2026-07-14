using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.Infrastructure.ExternalServices.Ai;

/// <summary>
/// Một transport gọi LLM theo 1 wire format cụ thể (Ollama / OpenAI-compatible). Stateless — nhận
/// <see cref="HttpClient"/> (đã set BaseAddress/Timeout/auth) + <see cref="AiProviderConfig"/> theo từng lượt,
/// nên 1 instance phục vụ được nhiều provider cùng format. <c>RelayChatClient</c> chọn transport theo Type.
/// </summary>
internal interface IAiChatTransport
{
    /// <summary>Loại wire format transport này xử lý — khớp <see cref="AiProviderConfig.Type"/>.</summary>
    string Type { get; }

    Task<AiChatCompletion> CompleteAsync(
        HttpClient http,
        AiProviderConfig cfg,
        string model,
        IReadOnlyList<AiChatMessage> messages,
        IReadOnlyList<AiToolSpec>? tools,
        AiCompletionOptions? options,
        IProgress<AiStreamChunk>? onDelta,
        CancellationToken ct);
}
