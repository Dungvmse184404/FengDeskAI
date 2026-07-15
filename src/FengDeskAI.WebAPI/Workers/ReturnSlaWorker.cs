using FengDeskAI.Application.Features.Returns.Services;
using Microsoft.Extensions.Options;

namespace FengDeskAI.WebAPI.Workers;

/// <summary>
/// Quét định kỳ để đảm bảo không ticket/refund/công nợ nào treo vĩnh viễn (SLA):
///  1) Auto-reject ticket quá hạn bổ sung bằng chứng (NeedMoreEvidence → Rejected).
///  2) Auto-retry refund Failed còn lượt; hết lượt → escalate Manager (Failed → ManagerReview).
///  3) Auto-settle công nợ vendor quá hạn dispute (Pending → Settled).
/// Dùng IOptionsMonitor để bật/tắt qua appsettings không cần restart.
/// </summary>
public sealed class ReturnSlaWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<ReturnSlaOptions> _optionsMonitor;
    private readonly ILogger<ReturnSlaWorker> _logger;

    public ReturnSlaWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<ReturnSlaOptions> optionsMonitor,
        ILogger<ReturnSlaWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(15, _optionsMonitor.CurrentValue.ScanIntervalSeconds));
        _logger.LogInformation("ReturnSlaWorker chạy, quét mỗi {Interval}s. IsActive={IsActive}",
            interval.TotalSeconds, _optionsMonitor.CurrentValue.IsActive);

        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                if (!_optionsMonitor.CurrentValue.IsActive)
                {
                    _logger.LogDebug("ReturnSlaWorker tạm tắt (IsActive=false), bỏ qua lượt quét.");
                    continue;
                }

                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var returns = scope.ServiceProvider.GetRequiredService<IReturnService>();
                    var refunds = scope.ServiceProvider.GetRequiredService<IRefundService>();
                    var liabilities = scope.ServiceProvider.GetRequiredService<IVendorLiabilityService>();

                    var rejected = await returns.AutoRejectOverdueEvidenceAsync(stoppingToken);
                    var retried = await refunds.AutoProcessFailedRefundsAsync(stoppingToken);
                    var settled = await liabilities.AutoSettleOverdueAsync(stoppingToken);

                    if (rejected + retried + settled > 0)
                        _logger.LogInformation("ReturnSla: auto-reject={Rejected}, refund-retry={Retried}, liability-settle={Settled}",
                            rejected, retried, settled);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lượt quét SLA RMA thất bại — thử lại chu kỳ sau.");
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
