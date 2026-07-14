using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FengDeskAI.Application.Interfaces.External;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.ExternalServices.Ai;

/// <summary>
/// Transport OpenAI-compatible <c>POST /chat/completions</c> (DeepSeek, OpenRouter, Groq, Together…).
/// Non-stream (backend cloud ổn định, không cần giữ tunnel). KHÔNG gửi ảnh (các model text-only sẽ lỗi;
/// chuỗi relay đã lọc để request có ảnh không rơi vào đây). Dịch tool-call: sinh tool_call_id theo THỨ TỰ
/// (Ollama không có id) — khớp được vì tool result luôn nối ngay sau, đúng thứ tự tool_calls.
/// </summary>
internal sealed class OpenAiTransport : IAiChatTransport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly ILogger<OpenAiTransport> _logger;

    public OpenAiTransport(ILogger<OpenAiTransport> logger) => _logger = logger;

    public string Type => "OpenAiCompatible";

    public async Task<AiChatCompletion> CompleteAsync(
        HttpClient http, AiProviderConfig cfg, string model, IReadOnlyList<AiChatMessage> messages,
        IReadOnlyList<AiToolSpec>? tools, AiCompletionOptions? options, IProgress<AiStreamChunk>? onDelta, CancellationToken ct)
    {
        // Non-stream (cloud nhanh, không cần) → bỏ qua onDelta. Muốn stream reasoning từ deepseek-reasoner
        // thì mới cần bật SSE + đọc delta.reasoning_content — để pha sau.
        // Cloud không hiểu tên model Ollama (qwen3-vl…) → luôn dùng model cấu hình của provider này.
        var effectiveModel = string.IsNullOrWhiteSpace(cfg.Model) ? model : cfg.Model;

        var payload = new OpenAiRequest
        {
            Model = effectiveModel,
            Messages = ToWireMessages(messages, cfg.SupportsVision),
            Tools = tools is { Count: > 0 } ? tools.Select(ToWireTool).ToList() : null,
            Temperature = options?.Temperature,
            ResponseFormat = options?.JsonMode == true ? new OpenAiResponseFormat { Type = "json_object" } : null,
            Stream = false,
        };

        _logger.LogInformation("[OpenAI] POST {Url} model={Model} ({Count} tin, {Tools} tools).",
            cfg.ChatUrl, effectiveModel, payload.Messages.Count, payload.Tools?.Count ?? 0);

        using var request = new HttpRequestMessage(HttpMethod.Post, cfg.ChatUrl)
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        using var resp = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogError("[OpenAI] Lỗi {Status}: {Err}", resp.StatusCode, err);
            resp.EnsureSuccessStatusCode();
        }

        var body = await resp.Content.ReadFromJsonAsync<OpenAiResponse>(JsonOptions, ct);
        var choice = body?.Choices?.FirstOrDefault();
        if (choice?.Message is null)
            throw new InvalidOperationException("OpenAI-compatible trả về body rỗng.");

        var toolCalls = choice.Message.ToolCalls?
            .Where(tc => tc.Function is not null && !string.IsNullOrWhiteSpace(tc.Function!.Name))
            .Select(tc => new AiToolCall(tc.Function!.Name, tc.Function.Arguments ?? "{}"))
            .ToList();

        var content = choice.Message.Content ?? string.Empty;
        if ((toolCalls is null || toolCalls.Count == 0) && string.IsNullOrEmpty(content))
            throw new InvalidOperationException("OpenAI-compatible trả về tin rỗng.");

        return new AiChatCompletion(content, body!.Model ?? effectiveModel,
            toolCalls is { Count: > 0 } ? toolCalls : null);
    }

    /// <summary>
    /// Dịch message trung lập → wire OpenAI. tool_call_id sinh theo thứ tự toàn cục ("call_N"):
    /// assistant.tool_calls nhận id, các message role=tool ngay sau nhận lại id theo đúng thứ tự (FIFO).
    /// Ảnh: chỉ gửi khi <paramref name="supportsVision"/> (vd Gemini) — dạng content-part image_url (data URI);
    /// provider text-only (DeepSeek/Groq text) thì bỏ ảnh (relay cũng đã lọc nên thường không tới đây).
    /// </summary>
    private static List<OpenAiMessage> ToWireMessages(IReadOnlyList<AiChatMessage> messages, bool supportsVision)
    {
        var result = new List<OpenAiMessage>(messages.Count);
        var pendingIds = new Queue<string>();
        var counter = 0;

        foreach (var m in messages)
        {
            if (m.Role == AiChatRoles.Tool)
            {
                var id = pendingIds.Count > 0 ? pendingIds.Dequeue() : $"call_{counter++}";
                result.Add(new OpenAiMessage { Role = "tool", ToolCallId = id, Content = m.Content });
                continue;
            }

            if (m.Role == AiChatRoles.Assistant && m.ToolCalls is { Count: > 0 })
            {
                var calls = m.ToolCalls.Select(tc =>
                {
                    var id = $"call_{counter++}";
                    pendingIds.Enqueue(id);
                    return new OpenAiToolCall
                    {
                        Id = id,
                        Function = new OpenAiToolCallFunction
                        {
                            Name = tc.Name,
                            Arguments = string.IsNullOrWhiteSpace(tc.ArgumentsJson) ? "{}" : tc.ArgumentsJson,
                        },
                    };
                }).ToList();
                result.Add(new OpenAiMessage
                {
                    Role = "assistant",
                    Content = string.IsNullOrEmpty(m.Content) ? null : m.Content,
                    ToolCalls = calls,
                });
                continue;
            }

            // Có ảnh + provider nhìn được ảnh → content dạng mảng part (text + image_url data URI).
            if (m.Images is { Count: > 0 } && supportsVision)
            {
                var parts = new List<OpenAiContentPart>(m.Images.Count + 1);
                if (!string.IsNullOrEmpty(m.Content))
                    parts.Add(new OpenAiContentPart { Type = "text", Text = m.Content });
                foreach (var b64 in m.Images)
                    parts.Add(new OpenAiContentPart
                    {
                        Type = "image_url",
                        ImageUrl = new OpenAiImageUrl { Url = $"data:{DetectMime(b64)};base64,{b64}" },
                    });
                result.Add(new OpenAiMessage { Role = m.Role, Content = parts });
                continue;
            }

            // system / user / assistant thường (text-only, hoặc ảnh bị bỏ nếu provider không vision).
            result.Add(new OpenAiMessage { Role = m.Role, Content = m.Content });
        }
        return result;
    }

    /// <summary>Đoán MIME từ vài ký tự đầu base64 (magic bytes) — đủ cho JPG/PNG/GIF/BMP, mặc định jpeg.</summary>
    private static string DetectMime(string base64) => base64 switch
    {
        _ when base64.StartsWith("/9j/", StringComparison.Ordinal) => "image/jpeg",
        _ when base64.StartsWith("iVBOR", StringComparison.Ordinal) => "image/png",
        _ when base64.StartsWith("R0lGOD", StringComparison.Ordinal) => "image/gif",
        _ when base64.StartsWith("Qk", StringComparison.Ordinal) => "image/bmp",
        _ => "image/jpeg",
    };

    private static OpenAiTool ToWireTool(AiToolSpec spec) => new()
    {
        Function = new OpenAiFunction
        {
            Name = spec.Name,
            Description = spec.Description,
            Parameters = new OpenAiParams
            {
                Properties = spec.Parameters.ToDictionary(
                    p => p.Key,
                    p => new OpenAiProp { Type = p.Value.Type, Description = p.Value.Description, Enum = p.Value.Enum?.ToList() }),
                Required = spec.Parameters.Where(p => p.Value.Required).Select(p => p.Key).ToList(),
            },
        },
    };

    // ── Wire format (OpenAI /chat/completions) ─────────────────────────────────
    private sealed class OpenAiRequest
    {
        [JsonPropertyName("model")] public string Model { get; init; } = string.Empty;
        [JsonPropertyName("messages")] public List<OpenAiMessage> Messages { get; init; } = new();

        [JsonPropertyName("tools")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<OpenAiTool>? Tools { get; init; }

        [JsonPropertyName("temperature")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Temperature { get; init; }

        [JsonPropertyName("response_format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OpenAiResponseFormat? ResponseFormat { get; init; }

        [JsonPropertyName("stream")] public bool Stream { get; init; }
    }

    private sealed class OpenAiResponseFormat
    {
        [JsonPropertyName("type")] public string Type { get; init; } = "text";
    }

    private sealed class OpenAiMessage
    {
        [JsonPropertyName("role")] public string Role { get; init; } = string.Empty;

        /// <summary>string (text thuần) HOẶC List&lt;OpenAiContentPart&gt; (multimodal: text + image_url).</summary>
        [JsonPropertyName("content")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Content { get; init; }

        [JsonPropertyName("tool_calls")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<OpenAiToolCall>? ToolCalls { get; init; }

        [JsonPropertyName("tool_call_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolCallId { get; init; }
    }

    private sealed class OpenAiContentPart
    {
        [JsonPropertyName("type")] public string Type { get; init; } = "text";

        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; init; }

        [JsonPropertyName("image_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OpenAiImageUrl? ImageUrl { get; init; }
    }

    private sealed class OpenAiImageUrl
    {
        [JsonPropertyName("url")] public string Url { get; init; } = string.Empty;
    }

    private sealed class OpenAiToolCall
    {
        [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; init; } = "function";
        [JsonPropertyName("function")] public OpenAiToolCallFunction Function { get; init; } = new();
    }

    private sealed class OpenAiToolCallFunction
    {
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;

        /// <summary>OpenAI trả arguments dạng CHUỖI JSON (khác Ollama trả object).</summary>
        [JsonPropertyName("arguments")] public string Arguments { get; init; } = "{}";
    }

    private sealed class OpenAiTool
    {
        [JsonPropertyName("type")] public string Type { get; init; } = "function";
        [JsonPropertyName("function")] public OpenAiFunction Function { get; init; } = new();
    }

    private sealed class OpenAiFunction
    {
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("description")] public string Description { get; init; } = string.Empty;
        [JsonPropertyName("parameters")] public OpenAiParams Parameters { get; init; } = new();
    }

    private sealed class OpenAiParams
    {
        [JsonPropertyName("type")] public string Type { get; init; } = "object";
        [JsonPropertyName("properties")] public Dictionary<string, OpenAiProp> Properties { get; init; } = new();
        [JsonPropertyName("required")] public List<string> Required { get; init; } = new();
    }

    private sealed class OpenAiProp
    {
        [JsonPropertyName("type")] public string Type { get; init; } = "string";
        [JsonPropertyName("description")] public string Description { get; init; } = string.Empty;

        [JsonPropertyName("enum")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Enum { get; init; }
    }

    private sealed class OpenAiResponse
    {
        [JsonPropertyName("model")] public string? Model { get; init; }
        [JsonPropertyName("choices")] public List<OpenAiChoice>? Choices { get; init; }
    }

    private sealed class OpenAiChoice
    {
        [JsonPropertyName("message")] public OpenAiRespMessage? Message { get; init; }
    }

    private sealed class OpenAiRespMessage
    {
        [JsonPropertyName("content")] public string? Content { get; init; }
        [JsonPropertyName("tool_calls")] public List<OpenAiRespToolCall>? ToolCalls { get; init; }
    }

    private sealed class OpenAiRespToolCall
    {
        [JsonPropertyName("function")] public OpenAiToolCallFunction? Function { get; init; }
    }
}
