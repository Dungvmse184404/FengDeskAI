using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FengDeskAI.Application.Features.CustomerCare;
using FengDeskAI.Application.Interfaces.External;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Infrastructure.ExternalServices.Ai;

/// <summary>
/// Gọi LLM hội thoại qua endpoint kiểu Ollama <c>POST /api/chat</c> (stream=false).
/// Cấu hình ở section "AiChat". Trung lập provider: chỉ map message → wire format.
/// </summary>
public sealed class OllamaChatClient : IAiChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly AiChatOptions _options;
    private readonly ILogger<OllamaChatClient> _logger;

    public OllamaChatClient(HttpClient http, IOptions<AiChatOptions> options, ILogger<OllamaChatClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        _http = http;
        _http.BaseAddress = new Uri(_options.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _http.DefaultRequestHeaders.TryAddWithoutValidation(_options.ApiKeyHeader, _options.ApiKey);
    }

    public async Task<AiChatCompletion> CompleteAsync(
        string model, IReadOnlyList<AiChatMessage> messages, CancellationToken ct = default)
    {
        var payload = new OllamaChatRequest
        {
            Model = model,
            Stream = false,
            Messages = messages.Select(m => new OllamaMessage
            {
                Role = m.Role,
                Content = m.Content,
                Images = m.Images is { Count: > 0 } ? m.Images.ToList() : null,
            }).ToList(),
        };

        _logger.LogInformation("[AiChat] POST {Path} model={Model} ({Count} tin nhắn).",
            _options.ChatPath, model, payload.Messages.Count);

        using var resp = await _http.PostAsJsonAsync(_options.ChatPath, payload, JsonOptions, ct);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, ct);
        if (body?.Message is null || string.IsNullOrEmpty(body.Message.Content))
            throw new InvalidOperationException("LLM trả về body rỗng.");

        return new AiChatCompletion(body.Message.Content, body.Model ?? model);
    }

    // ── Wire format (Ollama /api/chat) ─────────────────────────────────────────
    private sealed class OllamaChatRequest
    {
        public string Model { get; init; } = string.Empty;
        public bool Stream { get; init; }
        public List<OllamaMessage> Messages { get; init; } = new();
    }

    private sealed class OllamaMessage
    {
        public string Role { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;

        /// <summary>Ảnh base64 thuần (đa phương thức). Null → không serialize.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Images { get; init; }
    }

    private sealed class OllamaChatResponse
    {
        public string? Model { get; init; }
        public OllamaMessage? Message { get; init; }

        [JsonPropertyName("done")]
        public bool Done { get; init; }
    }
}
