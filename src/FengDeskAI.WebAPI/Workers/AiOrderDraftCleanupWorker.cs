using FengDeskAI.Application.Features.CustomerCare;
using FengDeskAI.Application.Interfaces.Repositories;
using Microsoft.Extensions.Options;

namespace FengDeskAI.WebAPI.Workers;

/// <summary>
/// Dọn bảng <c>ai_order_drafts</c>: xóa CỨNG draft Pending đã hết hạn và draft kẹt ở Confirming
/// (process chết giữa lúc tạo đơn). Draft đặt xong / user bỏ đã bị xóa ngay tại tool, worker chỉ lo phần sót.
/// Dùng IOptionsMonitor để đọc lại IsActive mỗi tick — bật/tắt qua appsettings không cần restart.
/// </summary>
public sealed class AiOrderDraftCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<AiOrderDraftOptions> _optionsMonitor;
    private readonly ILogger<AiOrderDraftCleanupWorker> _logger;

    public AiOrderDraftCleanupWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<AiOrderDraftOptions> optionsMonitor,
        ILogger<AiOrderDraftCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(30, _optionsMonitor.CurrentValue.ScanIntervalSeconds));

        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                var current = _optionsMonitor.CurrentValue;
                if (!current.IsActive)
                {
                    _logger.LogDebug("AiOrderDraftCleanupWorker tạm tắt (IsActive=false), bỏ qua lượt quét.");
                    continue;
                }

                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var purged = await uow.AiOrderDrafts.PurgeStaleAsync(
                        DateTime.UtcNow, TimeSpan.FromMinutes(current.ConfirmingGraceMinutes), stoppingToken);
                    if (purged > 0)
                        _logger.LogInformation("AiOrderDraftCleanupWorker: đã xóa {Count} draft hết hạn/kẹt.", purged);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lượt dọn draft đơn hàng AI thất bại — thử lại ở chu kỳ sau.");
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
