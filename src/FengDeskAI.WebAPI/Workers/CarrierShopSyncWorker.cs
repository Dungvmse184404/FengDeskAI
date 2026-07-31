using FengDeskAI.Application.Features.Shipping.Services;
using Microsoft.Extensions.Options;

namespace FengDeskAI.WebAPI.Workers;

/// <summary>
/// Quét định kỳ các garden store đã có địa chỉ nhưng chưa có mã shop nhà vận chuyển
/// (<c>GhnShopId</c>) và đăng ký giúp — mỗi store là một điểm lấy hàng riêng bên GHN.
/// Bổ sung cho 2 điểm cấp phát tức thời (owner lưu địa chỉ / lúc tạo vận đơn): worker này
/// lo các store cũ và các lần đăng ký trước đó bị lỗi mạng.
/// Đọc lại IsActive mỗi tick qua IOptionsMonitor để bật/tắt không cần restart.
/// </summary>
public sealed class CarrierShopSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<CarrierShopSyncOptions> _optionsMonitor;
    private readonly ILogger<CarrierShopSyncWorker> _logger;

    public CarrierShopSyncWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<CarrierShopSyncOptions> optionsMonitor,
        ILogger<CarrierShopSyncWorker> logger)
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
            "CarrierShopSyncWorker chạy: quét store thiếu mã shop mỗi {Interval}s, tối đa {BatchSize} store/lượt. IsActive={IsActive}",
            interval.TotalSeconds, opts.BatchSize, opts.IsActive);

        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                var current = _optionsMonitor.CurrentValue;
                if (!current.IsActive)
                {
                    _logger.LogDebug("CarrierShopSyncWorker tạm tắt (IsActive=false), bỏ qua lượt quét.");
                    continue;
                }

                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var provisioner = scope.ServiceProvider.GetRequiredService<IStoreShopProvisioner>();
                    var result = await provisioner.BackfillAsync(current.BatchSize, stoppingToken);
                    if (result.Provisioned > 0)
                        _logger.LogInformation("CarrierShopSyncWorker đã cấp mã shop cho {Count}/{Scanned} store.",
                            result.Provisioned, result.Scanned);
                    if (result.Skipped.Count > 0)
                        _logger.LogWarning("CarrierShopSyncWorker bỏ qua {Count} store thiếu điều kiện: {Stores}",
                            result.Skipped.Count,
                            string.Join(", ", result.Skipped.Select(s => $"{s.StoreName} ({s.Reason})")));
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lượt quét cấp mã shop thất bại — thử lại ở chu kỳ sau.");
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

/// <summary>Cấu hình worker đồng bộ mã shop nhà vận chuyển (section <c>CarrierShopSync</c>).</summary>
public class CarrierShopSyncOptions
{
    public const string SectionName = "CarrierShopSync";

    /// <summary>Bật/tắt worker. Tắt khi chạy provider Mock hoặc môi trường không có credential thật.</summary>
    public bool IsActive { get; set; }
    /// <summary>Chu kỳ quét (giây). Mặc định 30 phút — store mới đã được cấp ngay lúc lưu địa chỉ.</summary>
    public int ScanIntervalSeconds { get; set; } = 1800;
    /// <summary>Số store xử lý tối đa mỗi lượt, tránh gọi dồn nhà vận chuyển.</summary>
    public int BatchSize { get; set; } = 20;
}
