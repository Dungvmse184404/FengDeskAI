using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FengDeskAI.Application.Interfaces.External;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.ExternalServices.Ai;

/// <summary>
/// Transport Ollama <c>POST /api/chat</c> (hỗ trợ stream NDJSON + tool calling). Wire logic tách khỏi
/// HttpClient/config → dùng chung cho mọi provider Type="Ollama" trong chuỗi relay.
/// </summary>
internal sealed class OllamaTransport : IAiChatTransport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<OllamaTransport> _logger;

    public OllamaTransport(ILogger<OllamaTransport> logger) => _logger = logger;

    public string Type => "Ollama";

    // Chỉ retry lỗi tầng kết nối/stream (socket bị cắt GIỮA CHỪNG khi model đang lên). HTTP non-success
    // (vd ngrok 404 offline = máy tắt) KHÔNG retry — để relay sang provider kế ngay. 1 lần là đủ.
    private const int MaxTransientRetries = 1;
    private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromMilliseconds(400) };

    /// <summary>Lỗi transport tạm thời (socket bị OS/tunnel cắt) — an toàn thử lại khi ct chưa bị hủy.
    /// KHÔNG gồm lỗi status HTTP (ta ném <see cref="AiEndpointDownException"/> cho ca đó → không retry).</summary>
    private static bool IsTransient(Exception ex, CancellationToken ct)
        => !ct.IsCancellationRequested
           && ex is HttpRequestException or IOException or SocketException or OperationCanceledException;

    /// <summary>Endpoint trả HTTP non-success (offline/lỗi server) — dứt khoát, KHÔNG retry, để relay fallback ngay.</summary>
    private sealed class AiEndpointDownException(string message) : Exception(message);

    public async Task<AiChatCompletion> CompleteAsync(
        HttpClient http, AiProviderConfig cfg, string model, IReadOnlyList<AiChatMessage> messages,
        IReadOnlyList<AiToolSpec>? tools, AiCompletionOptions? options, IProgress<AiStreamChunk>? onDelta, CancellationToken ct)
    {
        // Ollama hiểu tên model do caller chọn (qwen3.5 / qwen3-vl khi có ảnh); cfg.Model chỉ là mặc định.
        var effectiveModel = !string.IsNullOrWhiteSpace(model) ? model : cfg.Model;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await SendOnceAsync(http, cfg, effectiveModel, messages, tools, options, onDelta, ct);
            }
            catch (Exception ex) when (attempt <= MaxTransientRetries && IsTransient(ex, ct))
            {
                _logger.LogWarning(ex,
                    "[Ollama] Lỗi kết nối tạm thời (lần {Attempt}/{Max}) — thử lại.", attempt, MaxTransientRetries);
                await Task.Delay(RetryDelays[attempt - 1], ct);
            }
        }
    }

    private async Task<AiChatCompletion> SendOnceAsync(
        HttpClient http, AiProviderConfig cfg, string model, IReadOnlyList<AiChatMessage> messages,
        IReadOnlyList<AiToolSpec>? tools, AiCompletionOptions? options, IProgress<AiStreamChunk>? onDelta, CancellationToken ct)
    {
        var streaming = options?.Stream ?? false;
        var payload = new OllamaChatRequest
        {
            Model = model,
            Stream = streaming,
            KeepAlive = string.IsNullOrWhiteSpace(cfg.KeepAlive) ? null : cfg.KeepAlive,
            Messages = messages.Select(ToWire).ToList(),
            Tools = tools is { Count: > 0 } ? tools.Select(ToWireTool).ToList() : null,
            Format = options?.JsonMode == true ? "json" : null,
            Think = options?.Think,
            Options = BuildOllamaOptions(cfg, options),
        };

        _logger.LogInformation("[Ollama] POST {Url} model={Model} stream={Stream} ({Count} tin, {Tools} tools).",
            cfg.ChatUrl, model, streaming, payload.Messages.Count, payload.Tools?.Count ?? 0);

        using var request = new HttpRequestMessage(HttpMethod.Post, cfg.ChatUrl)
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        using var resp = await http.SendAsync(
            request,
            streaming ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
            ct);

        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            if (payload.Tools is { Count: > 0 } && err.Contains("does not support tools", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[Ollama] Model {Model} không hỗ trợ tools — fallback chat thường.", model);
                return await SendOnceAsync(http, cfg, model, messages, tools: null, options, onDelta, ct);
            }
            // Non-success = server đã trả lời dứt khoát (vd ngrok "offline" 404) → KHÔNG retry, ném lỗi
            // non-transient để relay chuyển ngay sang provider kế.
            var preview = err.Length > 200 ? err[..200] : err;
            _logger.LogWarning("[Ollama] HTTP {Status} — không retry, để relay fallback. {Err}", resp.StatusCode, preview);
            throw new AiEndpointDownException($"Ollama trả HTTP {(int)resp.StatusCode}.");
        }

        var body = streaming
            ? await ReadStreamedResponseAsync(resp, onDelta, ct)
            : await resp.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, ct);
        if (body?.Message is null)
            throw new InvalidOperationException("Ollama trả về body rỗng.");

        var toolCalls = body.Message.ToolCalls?
            .Where(tc => tc.Function is not null)
            .Select(tc => new AiToolCall(tc.Function!.Name, tc.Function.Arguments?.ToJsonString() ?? "{}"))
            .ToList();

        var content = body.Message.Content;
        if ((toolCalls is null || toolCalls.Count == 0) && string.IsNullOrEmpty(content)
            && !string.IsNullOrWhiteSpace(body.Message.Thinking))
        {
            _logger.LogWarning("[Ollama] Content rỗng nhưng thinking có nội dung — dùng thinking làm câu trả lời.");
            content = body.Message.Thinking;
        }

        if ((toolCalls is null || toolCalls.Count == 0) && string.IsNullOrEmpty(content))
            throw new InvalidOperationException("Ollama trả về tin rỗng (không content, không tool call).");

        return new AiChatCompletion(content ?? string.Empty, body.Model ?? model,
            toolCalls is { Count: > 0 } ? toolCalls : null);
    }

    /// <summary>Gộp stream NDJSON của Ollama thành 1 response (giữ traffic sống qua tunnel; caller vẫn nhận đủ 1 lần).</summary>
    private static async Task<OllamaChatResponse?> ReadStreamedResponseAsync(
        HttpResponseMessage resp, IProgress<AiStreamChunk>? onDelta, CancellationToken ct)
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
                if (!string.IsNullOrEmpty(chunk.Message.Content))
                {
                    content.Append(chunk.Message.Content);
                    onDelta?.Report(new AiStreamChunk(AiStreamKind.Content, chunk.Message.Content));
                }
                if (!string.IsNullOrEmpty(chunk.Message.Thinking))
                {
                    thinking.Append(chunk.Message.Thinking);
                    onDelta?.Report(new AiStreamChunk(AiStreamKind.Thinking, chunk.Message.Thinking));
                }
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

    private static OllamaOptions? BuildOllamaOptions(AiProviderConfig cfg, AiCompletionOptions? options)
    {
        if (cfg.NumCtx <= 0 && options?.Temperature is null) return null;
        return new OllamaOptions
        {
            NumCtx = cfg.NumCtx > 0 ? cfg.NumCtx : null,
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

        [JsonPropertyName("format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Format { get; init; }

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
