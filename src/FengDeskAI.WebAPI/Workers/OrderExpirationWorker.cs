using FengDeskAI.Application.Features.Sales.Services;
using Microsoft.Extensions.Options;

namespace FengDeskAI.WebAPI.Workers;

/// <summary>
/// Quét định kỳ các đơn online còn Pending quá hạn thanh toán và chuyển sang Expired
/// (kèm transaction → Expired, hủy link PayOS, hoàn kho, ghi OrderStatusLog).
/// </summary>
public sealed class OrderExpirationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OrderExpirationOptions _options;
    private readonly ILogger<OrderExpirationWorker> _logger;

    public OrderExpirationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<OrderExpirationOptions> options,
        ILogger<OrderExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timeout = TimeSpan.FromMinutes(_options.PendingTimeoutMinutes);
        var interval = TimeSpan.FromSeconds(_options.ScanIntervalSeconds);
        _logger.LogInformation("OrderExpirationWorker chạy: đơn quá {Timeout} phút chưa thanh toán sẽ hết hạn, quét mỗi {Interval}s.",
            timeout.TotalMinutes, interval.TotalSeconds);

        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                try
                {
                    // Scope mới mỗi lượt — IUnitOfWork/DbContext là scoped.
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
