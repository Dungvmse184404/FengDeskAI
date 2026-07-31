using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FengDeskAI.Application.Interfaces.External;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Infrastructure.ExternalServices.Model3D;

/// <summary>
/// Gọi Meshy AI multi-image-to-3D (bất đồng bộ, 1–4 ảnh): POST tạo job → nhận task id; GET poll
/// trạng thái. Dùng chung cho cả Initial (1 ảnh) lẫn Regenerate (nhiều ảnh) — xem
/// docs/adr/refactor-model3d-request-flow.md mục 6.
/// </summary>
public sealed class MeshyModel3DGenerator : IModel3DGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly MeshySettings _settings;
    private readonly ILogger<MeshyModel3DGenerator> _logger;

    public MeshyModel3DGenerator(HttpClient http, IOptions<MeshySettings> options, ILogger<MeshyModel3DGenerator> logger)
    {
        _settings = options.Value;
        _logger = logger;
        _http = http;
        _http.BaseAddress = new Uri(_settings.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
    }

    public int InsufficientCreditsBackoffMinutes => _settings.InsufficientCreditsBackoffMinutes;

    public async Task<string> StartImageTo3DAsync(IReadOnlyList<string> imageUrls, CancellationToken ct = default)
    {
        if (imageUrls is null || imageUrls.Count == 0)
            throw new ArgumentException("Cần ít nhất 1 ảnh nguồn.", nameof(imageUrls));
        if (imageUrls.Count > 4)
            throw new ArgumentException("Meshy multi-image-to-3D chỉ nhận tối đa 4 ảnh.", nameof(imageUrls));

        var payload = new MeshyCreateRequest
        {
            ImageUrls = imageUrls,
            AiModel = _settings.AiModel,
            Topology = _settings.Topology,
            TargetPolycount = _settings.TargetPolycount,
            ShouldTexture = _settings.ShouldTexture,
        };

        _logger.LogInformation("[Meshy] POST {Path} (multi-image-to-3D, {Count} ảnh).", _settings.MultiImageTo3DPath, imageUrls.Count);
        using var resp = await _http.PostAsJsonAsync(_settings.MultiImageTo3DPath, payload, JsonOptions, ct);

        // 402 Payment Required = hết credit — ném riêng để caller requeue thay vì fail hẳn.
        // Nguồn: https://docs.meshy.ai/en/api/errors
        if (resp.StatusCode == HttpStatusCode.PaymentRequired)
        {
            _logger.LogWarning("[Meshy] 402 Payment Required — hết credit.");
            throw new InsufficientCreditsException("Meshy hết credit (402 Payment Required).");
        }

        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<MeshyCreateResponse>(JsonOptions, ct);
        if (body is null || string.IsNullOrWhiteSpace(body.Result))
            throw new InvalidOperationException("Meshy trả về task id rỗng.");
        return body.Result;
    }

    public async Task<Model3DTaskResult> GetTaskAsync(string taskId, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync($"{_settings.MultiImageTo3DPath}/{taskId}", ct);
        resp.EnsureSuccessStatusCode();

        var task = await resp.Content.ReadFromJsonAsync<MeshyTaskResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Meshy trả về body rỗng khi poll.");

        var state = task.Status?.ToUpperInvariant() switch
        {
            "SUCCEEDED" => Model3DGenerationState.Succeeded,
            "FAILED" or "CANCELED" or "EXPIRED" => Model3DGenerationState.Failed,
            _ => Model3DGenerationState.Running, // PENDING, IN_PROGRESS
        };

        return new Model3DTaskResult(
            state,
            task.Progress,
            task.ModelUrls?.Glb,
            task.ThumbnailUrl,
            task.TaskError?.Message);
    }

    public async Task<Stream> DownloadAsync(string url, CancellationToken ct = default)
    {
        // Dùng absolute URL của provider (khác BaseAddress) — HttpClient cho phép URI tuyệt đối.
        var bytes = await _http.GetByteArrayAsync(url, ct);
        return new MemoryStream(bytes);
    }

    // ----- DTO khớp Meshy OpenAPI v1 (multi-image-to-3d) -----

    private sealed class MeshyCreateRequest
    {
        [JsonPropertyName("image_urls")] public IReadOnlyList<string> ImageUrls { get; set; } = null!;
        [JsonPropertyName("ai_model")] public string AiModel { get; set; } = null!;
        [JsonPropertyName("topology")] public string Topology { get; set; } = null!;
        [JsonPropertyName("target_polycount")] public int TargetPolycount { get; set; }
        [JsonPropertyName("should_texture")] public bool ShouldTexture { get; set; }
    }

    private sealed class MeshyCreateResponse
    {
        [JsonPropertyName("result")] public string? Result { get; set; }
    }

    private sealed class MeshyTaskResponse
    {
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("progress")] public int Progress { get; set; }
        [JsonPropertyName("thumbnail_url")] public string? ThumbnailUrl { get; set; }
        [JsonPropertyName("model_urls")] public MeshyModelUrls? ModelUrls { get; set; }
        [JsonPropertyName("task_error")] public MeshyTaskError? TaskError { get; set; }
    }

    private sealed class MeshyModelUrls
    {
        [JsonPropertyName("glb")] public string? Glb { get; set; }
        [JsonPropertyName("fbx")] public string? Fbx { get; set; }
        [JsonPropertyName("usdz")] public string? Usdz { get; set; }
        [JsonPropertyName("obj")] public string? Obj { get; set; }
    }

    private sealed class MeshyTaskError
    {
        [JsonPropertyName("message")] public string? Message { get; set; }
    }
}
