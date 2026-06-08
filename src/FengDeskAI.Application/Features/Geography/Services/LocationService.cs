using AutoMapper;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Geography.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;

namespace FengDeskAI.Application.Features.Geography.Services;

public class LocationService : ILocationService
{
    private readonly ILocationRepository _locations;
    private readonly IMapper _mapper;

    public LocationService(ILocationRepository locations, IMapper mapper)
    {
        _locations = locations;
        _mapper = mapper;
    }

    public async Task<IServiceResult<List<ProvinceResponse>>> GetProvincesAsync(CancellationToken ct = default)
        => ServiceResult<List<ProvinceResponse>>.Success(
            _mapper.Map<List<ProvinceResponse>>(await _locations.GetProvincesAsync(ct)));

    public async Task<IServiceResult<List<DistrictResponse>>> GetDistrictsAsync(Guid provinceId, CancellationToken ct = default)
        => ServiceResult<List<DistrictResponse>>.Success(
            _mapper.Map<List<DistrictResponse>>(await _locations.GetDistrictsByProvinceAsync(provinceId, ct)));

    public async Task<IServiceResult<List<WardResponse>>> GetWardsAsync(Guid districtId, CancellationToken ct = default)
        => ServiceResult<List<WardResponse>>.Success(
            _mapper.Map<List<WardResponse>>(await _locations.GetWardsByDistrictAsync(districtId, ct)));
}
