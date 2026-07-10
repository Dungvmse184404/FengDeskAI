using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Contracts.Recommendation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Infrastructure.ExternalServices.Ai;

/// <summary>
/// Gọi AI microservice (Python) qua HTTP theo <c>Contracts/Recommendation/CONTRACT.md</c>.
/// Bật bằng <c>AiRecommendationSettings.UseMock = false</c>. Việc validate luật (không thêm/bớt
/// sản phẩm) do <c>RecommendationService.ApplyAiResponse</c> đảm nhiệm phía gọi.
/// </summary>
public sealed class HttpAiRecommendationClient : IAiRecommendationClient
{
    // Bỏ hẳn key khỏi payload khi null (thay vì gửi "key": null) — field workspace optional (Lighting,
    // DeskOrientation, DeskArea) không nên xuất hiện dưới dạng chuỗi "null" cho AI diễn giải.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly AiRecommendationSettings _settings;
    private readonly ILogger<HttpAiRecommendationClient> _logger;

    public HttpAiRecommendationClient(
        HttpClient http, IOptions<AiRecommendationSettings> options, ILogger<HttpAiRecommendationClient> logger)
    {
        _settings = options.Value;
        _logger = logger;
        _http = http;
        _http.BaseAddress = new Uri(_settings.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

        // Chỉ gắn header khi đã cấu hình key — client vẫn resolve được khi chưa có key.
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
            _http.DefaultRequestHeaders.Add(_settings.ApiKeyHeader, _settings.ApiKey);
    }

    public async Task<AiRecommendationResponse> ExplainAsync(AiRecommendationRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("[AI] POST {Path} với {Count} ứng viên.",
            _settings.ExplainPath, request.Candidates.Count);

        using var resp = await _http.PostAsJsonAsync(_settings.ExplainPath, request, JsonOptions, ct);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<AiRecommendationResponse>(JsonOptions, ct);
        return result ?? throw new InvalidOperationException("AI service trả về body rỗng.");
    }
}
