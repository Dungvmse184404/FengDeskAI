using FengDeskAI.Domain.Entities.Geography;

namespace FengDeskAI.Application.Interfaces.Repositories;

/// <summary>Read-only truy vấn dữ liệu hành chính (tỉnh/quận/phường).</summary>
public interface ILocationRepository
{
    Task<List<Province>> GetProvincesAsync(CancellationToken ct = default);
    Task<List<District>> GetDistrictsByProvinceAsync(Guid provinceId, CancellationToken ct = default);
    Task<List<Ward>> GetWardsByDistrictAsync(Guid districtId, CancellationToken ct = default);
    Task<bool> WardExistsAsync(Guid wardId, CancellationToken ct = default);
}
