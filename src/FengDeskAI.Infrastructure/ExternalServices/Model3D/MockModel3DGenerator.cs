using FengDeskAI.Application.Interfaces.External;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Infrastructure.ExternalServices.Model3D;

/// <summary>
/// Mock của Meshy — không gọi API ngoài, không tốn credit. Trả ngay trạng thái Succeeded với
/// một file GLB công khai (<c>MeshySettings.MockGlbUrl</c>) để chạy toàn bộ luồng (kể cả re-host
/// sang storage) khi dev. Thay bằng <see cref="MeshyModel3DGenerator"/> khi <c>UseMock = false</c>.
/// </summary>
public sealed class MockModel3DGenerator : IModel3DGenerator
{
    private readonly HttpClient _http;
    private readonly MeshySettings _settings;
    private readonly ILogger<MockModel3DGenerator> _logger;

    public MockModel3DGenerator(HttpClient http, IOptions<MeshySettings> options, ILogger<MockModel3DGenerator> logger)
    {
        _http = http;
        _settings = options.Value;
        _logger = logger;
    }

    public Task<string> StartImageTo3DAsync(string imageUrl, CancellationToken ct = default)
    {
        _logger.LogInformation("[MockMeshy] Giả lập tạo job image-to-3D cho ảnh {Image}.", imageUrl);
        return Task.FromResult($"mock-{Guid.NewGuid():N}");
    }

    public Task<Model3DTaskResult> GetTaskAsync(string taskId, CancellationToken ct = default)
    {
        // Mock: hoàn tất ngay sau 1 lượt poll.
        return Task.FromResult(new Model3DTaskResult(
            Model3DGenerationState.Succeeded, 100, _settings.MockGlbUrl, null, null));
    }

    public async Task<Stream> DownloadAsync(string url, CancellationToken ct = default)
    {
        var bytes = await _http.GetByteArrayAsync(url, ct);
        return new MemoryStream(bytes);
    }
}
