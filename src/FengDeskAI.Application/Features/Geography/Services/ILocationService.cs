using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Geography.DTOs;

namespace FengDeskAI.Application.Features.Geography.Services;

public interface ILocationService
{
    Task<IServiceResult<List<ProvinceResponse>>> GetProvincesAsync(CancellationToken ct = default);
    Task<IServiceResult<List<DistrictResponse>>> GetDistrictsAsync(Guid provinceId, CancellationToken ct = default);
    Task<IServiceResult<List<WardResponse>>> GetWardsAsync(Guid districtId, CancellationToken ct = default);

    /// <summary>Tra ngược phường → quận → tỉnh, để FE dựng lại dropdown khi sửa địa chỉ đã lưu.</summary>
    Task<IServiceResult<WardPathResponse>> GetWardPathAsync(Guid wardId, CancellationToken ct = default);
}
