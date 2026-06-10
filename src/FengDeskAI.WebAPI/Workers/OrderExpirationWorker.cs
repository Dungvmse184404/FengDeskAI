using FengDeskAI.Application.Features.Sales.Services;
using Microsoft.Extensions.Options;

namespace FengDeskAI.WebAPI.Workers;

/// <summary>
/// Quét định kỳ các đơn online còn Pending quá hạn thanh toán và chuyển sang Expired
/// (kèm transaction → Expired, hủy link PayOS, hoàn kho, ghi OrderStatusLog).
/// Dùng IOptionsMonitor để đọc lại IsActive mỗi tick — cho phép bật/tắt qua appsettings
/// mà không cần restart app.
/// </summary>
public sealed class OrderExpirationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<OrderExpirationOptions> _optionsMonitor;
    private readonly ILogger<OrderExpirationWorker> _logger;

    public OrderExpirationWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OrderExpirationOptions> optionsMonitor,
        ILogger<OrderExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _optionsMonitor.CurrentValue;
        var interval = TimeSpan.FromSeconds(opts.ScanIntervalSeconds);
        _logger.LogInformation(
            "OrderExpirationWorker chạy: đơn quá {Timeout} phút chưa thanh toán sẽ hết hạn, quét mỗi {Interval}s. IsActive={IsActive}",
            opts.PendingTimeoutMinutes, interval.TotalSeconds, opts.IsActive);

        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                var current = _optionsMonitor.CurrentValue;
                if (!current.IsActive)
                {
                    _logger.LogDebug("OrderExpirationWorker tạm tắt (IsActive=false), bỏ qua lượt quét.");
                    continue;
                }

                try
                {
                    var timeout = TimeSpan.FromMinutes(current.PendingTimeoutMinutes);
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var expiration = scope.ServiceProvider.GetRequiredService<IOrderExpirationService>();
                    await expiration.ExpireOverdueOrdersAsync(timeout, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lượt quét đơn quá hạn thất bại — thử lại ở chu kỳ sau.");
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

