using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
    private readonly AiProviderOptions _options;
    private readonly ILogger<OllamaChatClient> _logger;

    public OllamaChatClient(HttpClient http, IOptions<AiProviderOptions> options, ILogger<OllamaChatClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        _http = http;
        _http.BaseAddress = new Uri(_options.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            _http.DefaultRequestHeaders.TryAddWithoutValidation(_options.ApiKeyHeader, _options.ApiKey);
    }

    /// <summary>Số lần thử lại tối đa khi lỗi transport tạm thời (socket bị OS/tunnel cắt ngang giữa chừng).</summary>
    private const int MaxTransientRetries = 2;

    /// <summary>Delay giữa các lần retry — ngắn vì Ollama có prompt cache checkpoint, thử lại gần như không mất phí xử lý lại prompt.</summary>
    private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(1200) };

    /// <summary>
    /// Lỗi "transient" ở tầng transport (socket bị Windows/ngrok cắt ngang giữa chừng — WSA 995,
    /// connection reset...) — AN TOÀN để thử lại vì <paramref name="ct"/> (request gốc của user) CHƯA bị hủy.
    /// Nếu <paramref name="ct"/> đã bị hủy thật (user đóng tab/reload) thì KHÔNG retry.
    /// </summary>
    private static bool IsTransient(Exception ex, CancellationToken ct)
        => !ct.IsCancellationRequested
           && ex is HttpRequestException or IOException or SocketException or OperationCanceledException;

    public async Task<AiChatCompletion> CompleteAsync(
        string model, IReadOnlyList<AiChatMessage> messages, IReadOnlyList<AiToolSpec>? tools = null,
        AiCompletionOptions? options = null, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await SendOnceAsync(model, messages, tools, options, ct);
            }
            catch (Exception ex) when (attempt <= MaxTransientRetries && IsTransient(ex, ct))
            {
                _logger.LogWarning(ex,
                    "[AiChat] Ollama lỗi kết nối tạm thời (lần {Attempt}/{Max}) — thử lại (prompt cache sẽ giúp lần sau nhanh hơn).",
                    attempt, MaxTransientRetries);
                await Task.Delay(RetryDelays[attempt - 1], ct);
            }
        }
    }

    private async Task<AiChatCompletion> SendOnceAsync(
        string model, IReadOnlyList<AiChatMessage> messages, IReadOnlyList<AiToolSpec>? tools,
        AiCompletionOptions? options, CancellationToken ct)
    {
        var streaming = options?.Stream ?? false;
        var payload = new OllamaChatRequest
        {
            Model = model,
            Stream = streaming,
            KeepAlive = string.IsNullOrWhiteSpace(_options.KeepAlive) ? null : _options.KeepAlive,
            Messages = messages.Select(ToWire).ToList(),
            Tools = tools is { Count: > 0 } ? tools.Select(ToWireTool).ToList() : null,
            Format = options?.JsonMode == true ? "json" : null,
            Think = options?.Think,
            Options = BuildOllamaOptions(options),
        };

        _logger.LogInformation("[AiChat] POST {Path} model={Model} stream={Stream} ({Count} tin nhắn, {Tools} tools).",
            _options.ChatPath, model, streaming, payload.Messages.Count, payload.Tools?.Count ?? 0);

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ChatPath)
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        using var resp = await _http.SendAsync(
            request,
            streaming ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
            ct);

        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            // Model không hỗ trợ tools → thử lại KHÔNG kèm tools để chat vẫn chạy.
            if (payload.Tools is { Count: > 0 } && err.Contains("does not support tools", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[AiChat] Model {Model} không hỗ trợ tools — fallback chat thường.", model);
                return await CompleteAsync(model, messages, tools: null, options: options, ct: ct);
            }
            _logger.LogError("[AiChat] LLM lỗi {Status}: {Err}", resp.StatusCode, err);
            resp.EnsureSuccessStatusCode(); // ném lỗi
        }

        var body = streaming
            ? await ReadStreamedResponseAsync(resp, ct)
            : await resp.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, ct);
        if (body?.Message is null)
            throw new InvalidOperationException("LLM trả về body rỗng.");

        var toolCalls = body.Message.ToolCalls?
            .Where(tc => tc.Function is not null)
            .Select(tc => new AiToolCall(tc.Function!.Name, tc.Function.Arguments?.ToJsonString() ?? "{}"))
            .ToList();

        var content = body.Message.Content;

        // Model thinking (vd qwen3.5) thỉnh thoảng viết CẢ câu trả lời vào "thinking", content rỗng
        // → cứu bằng thinking thay vì ném lỗi làm mất lượt. (Muốn tắt hẳn: Ai:Chat:Think=false.)
        if ((toolCalls is null || toolCalls.Count == 0) && string.IsNullOrEmpty(content)
            && !string.IsNullOrWhiteSpace(body.Message.Thinking))
        {
            _logger.LogWarning("[AiChat] Content rỗng nhưng thinking có nội dung — dùng thinking làm câu trả lời.");
            content = body.Message.Thinking;
        }

        // Có tool_calls → content có thể rỗng (model chờ kết quả tool).
        if ((toolCalls is null || toolCalls.Count == 0) && string.IsNullOrEmpty(content))
            throw new InvalidOperationException("LLM trả về tin rỗng (không content, không tool call).");

        return new AiChatCompletion(content ?? string.Empty, body.Model ?? model,
            toolCalls is { Count: > 0 } ? toolCalls : null);
    }

    /// <summary>
    /// Đọc phản hồi Ollama streaming (mỗi dòng 1 JSON object, "message.content" là DELTA — không
    /// phải full text tích luỹ) — gộp lại thành 1 <see cref="OllamaChatResponse"/> duy nhất để phần
    /// còn lại của <see cref="SendOnceAsync"/> xử lý y hệt đường non-streaming. Chỉ đổi CÁCH ĐỌC WIRE;
    /// mục đích duy nhất là giữ traffic chảy liên tục qua tunnel/proxy trong lúc model sinh câu trả
    /// lời dài, không lộ chunk ra ngoài (caller vẫn nhận đúng 1 <see cref="AiChatCompletion"/> đầy đủ).
    /// </summary>
    private static async Task<OllamaChatResponse?> ReadStreamedResponseAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? modelName = null;
        string? role = null;
        var content = new StringBuilder();
        var thinking = new StringBuilder();
        List<OllamaToolCall>? toolCalls = null;
        var done = false;
        var sawAnyLine = false;

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;

            var chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line, JsonOptions);
            if (chunk is null) continue;
            sawAnyLine = true;

            modelName ??= chunk.Model;
            if (chunk.Message is not null)
            {
                role ??= chunk.Message.Role;
                if (!string.IsNullOrEmpty(chunk.Message.Content)) content.Append(chunk.Message.Content);
                if (!string.IsNullOrEmpty(chunk.Message.Thinking)) thinking.Append(chunk.Message.Thinking);
                if (chunk.Message.ToolCalls is { Count: > 0 }) toolCalls = chunk.Message.ToolCalls;
            }
            if (chunk.Done) { done = true; break; }
        }

        if (!sawAnyLine) return null;

        return new OllamaChatResponse
        {
            Model = modelName,
            Done = done,
            Message = new OllamaMessage
            {
                Role = role ?? "assistant",
                Content = content.ToString(),
                Thinking = thinking.Length > 0 ? thinking.ToString() : null,
                ToolCalls = toolCalls,
            },
        };
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

    private OllamaOptions? BuildOllamaOptions(AiCompletionOptions? options)
    {
        if (_options.NumCtx <= 0 && options?.Temperature is null) return null;
        return new OllamaOptions
        {
            NumCtx = _options.NumCtx > 0 ? _options.NumCtx : null,
            Temperature = options?.Temperature,
        };
    }

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

        /// <summary>"json" ép model trả JSON hợp lệ (workspace intake) — bỏ trống cho chat tự do.</summary>
        [JsonPropertyName("format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Format { get; init; }

        /// <summary>Bật/tắt thinking (model hỗ trợ như qwen3). null = không gửi, theo mặc định model.</summary>
        [JsonPropertyName("think")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Think { get; init; }

        [JsonPropertyName("options")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OllamaOptions? Options { get; init; }
    }

    private sealed class OllamaOptions
    {
        [JsonPropertyName("num_ctx")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? NumCtx { get; init; }

        [JsonPropertyName("temperature")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Temperature { get; init; }
    }

    private sealed class OllamaMessage
    {
        public string Role { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;

        /// <summary>Chuỗi suy luận của model thinking — chỉ đọc từ response, không gửi đi.</summary>
        [JsonPropertyName("thinking")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Thinking { get; init; }

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
