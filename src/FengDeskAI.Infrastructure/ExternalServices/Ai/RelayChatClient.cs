using FengDeskAI.Application.Interfaces.External;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Infrastructure.ExternalServices.Ai;

/// <summary>
/// Cổng LLM có chuỗi relay: thử lần lượt các provider trong <c>Ai:Relay:Providers</c> (thứ tự = ưu tiên)
/// cho tới khi 1 cái thành công. Cả chat lẫn intake dùng chung — không service nào biết có nhiều backend.
/// Request CÓ ảnh chỉ chạy trên provider <see cref="AiProviderConfig.SupportsVision"/>=true.
/// </summary>
internal sealed class RelayChatClient : IAiChatClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AiRelayOptions _options;
    private readonly IReadOnlyDictionary<string, IAiChatTransport> _transports;
    private readonly ILogger<RelayChatClient> _logger;

    public RelayChatClient(
        IHttpClientFactory httpFactory,
        IOptions<AiRelayOptions> options,
        IEnumerable<IAiChatTransport> transports,
        ILogger<RelayChatClient> logger)
    {
        _httpFactory = httpFactory;
        _options = options.Value;
        _transports = transports.ToDictionary(t => t.Type, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public async Task<AiChatCompletion> CompleteAsync(
        string model, IReadOnlyList<AiChatMessage> messages, IReadOnlyList<AiToolSpec>? tools = null,
        AiCompletionOptions? options = null, IProgress<AiStreamChunk>? onDelta = null, CancellationToken ct = default)
    {
        var hasImages = messages.Any(m => m.Images is { Count: > 0 });

        // Chỉ giữ provider bật + (nếu request có ảnh) nhìn được ảnh. Thứ tự danh sách = độ ưu tiên.
        var candidates = _options.Providers
            .Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.BaseUrl) && (!hasImages || p.SupportsVision))
            .ToList();

        if (candidates.Count == 0)
            throw new InvalidOperationException(hasImages
                ? "Không có AI provider nào nhìn được ảnh đang bật (cần Ollama/vision-capable)."
                : "Không có AI provider nào được cấu hình/bật (Ai:Relay:Providers).");

        Exception? last = null;
        for (var i = 0; i < candidates.Count; i++)
        {
            var cfg = candidates[i];
            if (!_transports.TryGetValue(cfg.Type, out var transport))
            {
                _logger.LogWarning("[AiRelay] Provider {Name} Type '{Type}' không nhận diện được — bỏ qua.", cfg.Name, cfg.Type);
                continue;
            }
            try
            {
                var http = BuildClient(cfg);
                var result = await transport.CompleteAsync(http, cfg, model, messages, tools, options, onDelta, ct);
                if (i > 0)
                    _logger.LogInformation("[AiRelay] Provider '{Name}' xử lý (đã fallback qua {Skipped} provider trước).", cfg.Name, i);
                return result;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "[AiRelay] Provider '{Name}' lỗi → thử provider kế.", cfg.Name);
                last = ex;
            }
        }

        // Hủy bởi client (đóng tab…) → ném đúng cancel để lớp trên phân biệt với lỗi hạ tầng.
        ct.ThrowIfCancellationRequested();
        throw last ?? new InvalidOperationException("Tất cả AI provider trong chuỗi relay đều lỗi.");
    }

    private HttpClient BuildClient(AiProviderConfig cfg)
    {
        // KHÔNG set BaseAddress: transport dùng URL tuyệt đối (cfg.ChatUrl) để tránh bẫy path bị ghi đè.
        var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(cfg.TimeoutSeconds > 0 ? cfg.TimeoutSeconds : 60);
        if (!string.IsNullOrWhiteSpace(cfg.ApiKey))
        {
            // Header "Authorization" → chuẩn Bearer (OpenAI/DeepSeek); header khác → gửi raw (vd Ollama x-api-key).
            var value = cfg.ApiKeyHeader.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                ? $"Bearer {cfg.ApiKey}"
                : cfg.ApiKey;
            http.DefaultRequestHeaders.TryAddWithoutValidation(cfg.ApiKeyHeader, value);
        }
        return http;
    }
}
