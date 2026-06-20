using FengDeskAI.Application.Features.Catalog.Services;
using FengDeskAI.Infrastructure.ExternalServices.Model3D;
using Microsoft.Extensions.Options;

namespace FengDeskAI.WebAPI.Workers;

/// <summary>
/// Quét định kỳ các job sinh 3D đang Processing và hoàn tất chúng (tải GLB từ Meshy →
/// re-host lên storage → ghi ModelUrl) hoặc đánh dấu Failed. Chu kỳ đọc từ
/// <c>MeshySettings.PollIntervalSeconds</c>.
/// </summary>
public sealed class Model3DPollingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<MeshySettings> _options;
    private readonly ILogger<Model3DPollingWorker> _logger;

    public Model3DPollingWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<MeshySettings> options,
        ILogger<Model3DPollingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.CurrentValue.PollIntervalSeconds));
        _logger.LogInformation("Model3DPollingWorker chạy: poll job sinh 3D mỗi {Interval}s.", interval.TotalSeconds);

        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var service = scope.ServiceProvider.GetRequiredService<IProductModel3DService>();
                    await service.PollPendingAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lượt poll job sinh 3D thất bại — thử lại ở chu kỳ sau.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // app shutdown — thoát êm
        }
    }
}
