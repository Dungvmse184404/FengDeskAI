namespace FengDeskAI.Application.Features.Geography.Services;

/// <summary>
/// Đồng bộ dữ liệu hành chính VN + mã GHN. Chạy như lệnh một lần (<c>dotnet run -- sync-geo</c>),
/// không phải endpoint runtime. Hai bước độc lập, chạy theo thứ tự. Xem Documents/GHN_INTEGRATION.md §10.
/// </summary>
public interface IGeoSyncService
{
    /// <summary>Bước A: nạp toàn bộ cây tỉnh→quận→phường (kèm mã GSO) từ nguồn chính phủ, upsert theo Code (idempotent).</summary>
    Task<GeoSyncReport> ImportGovernmentDataAsync(CancellationToken ct = default);

    /// <summary>Bước B: gọi master-data GHN, khớp theo Code/tên rồi điền GhnProvinceId/GhnDistrictId/GhnWardCode.</summary>
    Task<GeoSyncReport> SyncGhnCodesAsync(CancellationToken ct = default);
}

/// <summary>Kết quả tóm tắt một bước đồng bộ (để in report theo §10.4).</summary>
public record GeoSyncReport(int Provinces, int Districts, int Wards, int Unmatched)
{
    public static readonly GeoSyncReport Empty = new(0, 0, 0, 0);
}
