using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Geography.DTOs;

namespace FengDeskAI.Application.Features.Geography.Services;

public interface ILocationService
{
    Task<IServiceResult<List<ProvinceResponse>>> GetProvincesAsync(CancellationToken ct = default);
    Task<IServiceResult<List<DistrictResponse>>> GetDistrictsAsync(Guid provinceId, CancellationToken ct = default);
    Task<IServiceResult<List<WardResponse>>> GetWardsAsync(Guid districtId, CancellationToken ct = default);
}
