using FengDeskAI.Application.Features.Geography.Services;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.WebAPI.Workers;

/// <summary>
/// Tự động đồng bộ dữ liệu hành chính VN khi khởi động app (chạy 1 lần, nền).
/// Nếu bảng provinces chưa đủ 63 tỉnh (vd: DB local mới chỉ seed 3 tỉnh mẫu từ
/// vietnam-geography.json) → gọi GeoSyncService: Bước A nạp cây tỉnh/quận/phường
/// từ open-api.vn, Bước B điền mã GHN. Idempotent — upsert theo Code.
/// Tắt bằng config <c>Seeding:AutoGeoSync = false</c>.
/// Vẫn có thể chạy tay: <c>dotnet run --project src/FengDeskAI.WebAPI -- sync-geo</c>.
/// </summary>
public sealed class GeoAutoSyncWorker : BackgroundService
{
    /// <summary>Số tỉnh/thành chuẩn — dưới ngưỡng này coi như dữ liệu chưa đầy đủ.</summary>
    private const int FullProvinceCount = 63;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeoAutoSyncWorker> _logger;

    public GeoAutoSyncWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<GeoAutoSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Seeding:AutoGeoSync", true))
        {
            _logger.LogInformation("[GeoAutoSync] Đã tắt qua config Seeding:AutoGeoSync — bỏ qua.");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        int provinceCount;
        try
        {
            provinceCount = await sp.GetRequiredService<AppDbContext>()
                .Set<Province>().CountAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GeoAutoSync] Không đọc được bảng provinces (DB chưa migrate?) — bỏ qua.");
            return;
        }

        if (provinceCount >= FullProvinceCount)
        {
            _logger.LogInformation("[GeoAutoSync] Đã có {Count} tỉnh/thành — dữ liệu đầy đủ, bỏ qua.", provinceCount);
            return;
        }

        _logger.LogInformation(
            "[GeoAutoSync] Chỉ có {Count}/{Full} tỉnh/thành — bắt đầu đồng bộ tự động (chạy nền)…",
            provinceCount, FullProvinceCount);

        var geo = sp.GetRequiredService<IGeoSyncService>();

        // Bước A: nạp cây tỉnh/quận/phường từ open-api.vn (bắt buộc)
        try
        {
            var report = await geo.ImportGovernmentDataAsync(stoppingToken);
            _logger.LogInformation(
                "[GeoAutoSync] Bước A xong: {Provinces} tỉnh, {Districts} quận/huyện, {Wards} phường/xã.",
                report.Provinces, report.Districts, report.Wards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[GeoAutoSync] Bước A (nạp dữ liệu hành chính) thất bại — giữ nguyên dữ liệu hiện có. " +
                "Có thể chạy tay: dotnet run -- sync-geo");
            return;
        }

        // Bước B: điền mã GHN (không bắt buộc — thiếu token GHN vẫn dùng được dropdown)
        try
        {
            var report = await geo.SyncGhnCodesAsync(stoppingToken);
            _logger.LogInformation(
                "[GeoAutoSync] Hoàn tất: khớp mã GHN cho {Provinces} tỉnh, {Districts} quận/huyện, {Wards} phường/xã ({Unmatched} không khớp).",
                report.Provinces, report.Districts, report.Wards, report.Unmatched);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[GeoAutoSync] Bước B (khớp mã GHN) thất bại — dropdown địa chỉ vẫn hoạt động, " +
                "nhưng tính phí ship GHN có thể thiếu mã. Chạy lại: dotnet run -- sync-geo");
        }
    }
}
