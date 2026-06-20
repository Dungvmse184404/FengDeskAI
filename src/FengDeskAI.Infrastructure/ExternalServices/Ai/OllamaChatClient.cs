using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FengDeskAI.Application.Features.CustomerCare;
using FengDeskAI.Application.Interfaces.External;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Infrastructure.ExternalServices.Ai;

/// <summary>
/// Gọi LLM hội thoại kiểu Ollama <c>POST /api/chat</c> (stream=false). Hỗ trợ tool calling:
/// gửi <c>tools</c> + parse <c>message.tool_calls</c>. Trung lập provider: chỉ map sang wire format.
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
        string model, IReadOnlyList<AiChatMessage> messages, IReadOnlyList<AiToolSpec>? tools = null, CancellationToken ct = default)
    {
        var payload = new OllamaChatRequest
        {
            Model = model,
            Stream = false,
            KeepAlive = string.IsNullOrWhiteSpace(_options.KeepAlive) ? null : _options.KeepAlive,
            Messages = messages.Select(ToWire).ToList(),
            Tools = tools is { Count: > 0 } ? tools.Select(ToWireTool).ToList() : null,
            Options = _options.NumCtx > 0 ? new OllamaOptions { NumCtx = _options.NumCtx } : null,
        };

        _logger.LogInformation("[AiChat] POST {Path} model={Model} ({Count} tin nhắn, {Tools} tools).",
            _options.ChatPath, model, payload.Messages.Count, payload.Tools?.Count ?? 0);

        using var resp = await _http.PostAsJsonAsync(_options.ChatPath, payload, JsonOptions, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            // Model không hỗ trợ tools → thử lại KHÔNG kèm tools để chat vẫn chạy.
            if (payload.Tools is { Count: > 0 } && err.Contains("does not support tools", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[AiChat] Model {Model} không hỗ trợ tools — fallback chat thường.", model);
                return await CompleteAsync(model, messages, tools: null, ct);
            }
            _logger.LogError("[AiChat] LLM lỗi {Status}: {Err}", resp.StatusCode, err);
            resp.EnsureSuccessStatusCode(); // ném lỗi
        }

        var body = await resp.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, ct);
        if (body?.Message is null)
            throw new InvalidOperationException("LLM trả về body rỗng.");

        var toolCalls = body.Message.ToolCalls?
            .Where(tc => tc.Function is not null)
            .Select(tc => new AiToolCall(tc.Function!.Name, tc.Function.Arguments?.ToJsonString() ?? "{}"))
            .ToList();

        // Có tool_calls → content có thể rỗng (model chờ kết quả tool).
        if ((toolCalls is null || toolCalls.Count == 0) && string.IsNullOrEmpty(body.Message.Content))
            throw new InvalidOperationException("LLM trả về tin rỗng (không content, không tool call).");

        return new AiChatCompletion(body.Message.Content ?? string.Empty, body.Model ?? model,
            toolCalls is { Count: > 0 } ? toolCalls : null);
    }

    private static OllamaMessage ToWire(AiChatMessage m) => new()
    {
        Role = m.Role,
        Content = m.Content,
        ToolName = m.ToolName,
        Images = m.Images is { Count: > 0 } ? m.Images.ToList() : null,
        ToolCalls = m.ToolCalls is { Count: > 0 }
            ? m.ToolCalls.Select(tc => new OllamaToolCall
            {
                Function = new OllamaToolCallFunction
                {
                    Name = tc.Name,
                    Arguments = JsonNode.Parse(string.IsNullOrWhiteSpace(tc.ArgumentsJson) ? "{}" : tc.ArgumentsJson),
                },
            }).ToList()
            : null,
    };

    private static OllamaTool ToWireTool(AiToolSpec spec) => new()
    {
        Function = new OllamaFunction
        {
            Name = spec.Name,
            Description = spec.Description,
            Parameters = new OllamaParams
            {
                Properties = spec.Parameters.ToDictionary(
                    p => p.Key,
                    p => new OllamaProp { Type = p.Value.Type, Description = p.Value.Description, Enum = p.Value.Enum?.ToList() }),
                Required = spec.Parameters.Where(p => p.Value.Required).Select(p => p.Key).ToList(),
            },
        },
    };

    // ── Wire format (Ollama /api/chat) ─────────────────────────────────────────
    private sealed class OllamaChatRequest
    {
        public string Model { get; init; } = string.Empty;
        public bool Stream { get; init; }
        public List<OllamaMessage> Messages { get; init; } = new();

        [JsonPropertyName("keep_alive")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? KeepAlive { get; init; }

        [JsonPropertyName("tools")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<OllamaTool>? Tools { get; init; }

        [JsonPropertyName("options")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OllamaOptions? Options { get; init; }
    }

    private sealed class OllamaOptions
    {
        [JsonPropertyName("num_ctx")]
        public int NumCtx { get; init; }
    }

    private sealed class OllamaMessage
    {
        public string Role { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Images { get; init; }

        [JsonPropertyName("tool_calls")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<OllamaToolCall>? ToolCalls { get; init; }

        [JsonPropertyName("tool_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolName { get; init; }
    }

    private sealed class OllamaToolCall
    {
        [JsonPropertyName("function")]
        public OllamaToolCallFunction? Function { get; init; }
    }

    private sealed class OllamaToolCallFunction
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("arguments")]
        public JsonNode? Arguments { get; init; }
    }

    private sealed class OllamaTool
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "function";

        [JsonPropertyName("function")]
        public OllamaFunction Function { get; init; } = new();
    }

    private sealed class OllamaFunction
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("parameters")]
        public OllamaParams Parameters { get; init; } = new();
    }

    private sealed class OllamaParams
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "object";

        [JsonPropertyName("properties")]
        public Dictionary<string, OllamaProp> Properties { get; init; } = new();

        [JsonPropertyName("required")]
        public List<string> Required { get; init; } = new();
    }

    private sealed class OllamaProp
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "string";

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("enum")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Enum { get; init; }
    }

    private sealed class OllamaChatResponse
    {
        public string? Model { get; init; }
        public OllamaMessage? Message { get; init; }

        [JsonPropertyName("done")]
        public bool Done { get; init; }
    }
}
