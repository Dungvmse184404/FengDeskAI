using FengDeskAI.Application.Features.Geography.Services;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>Tra cứu dữ liệu hành chính (tỉnh → quận → phường). Public.</summary>
[Route("api/locations")]
[AllowAnonymous]
public class LocationsController : ApiControllerBase
{
    private readonly ILocationService _service;

    public LocationsController(ILocationService service) => _service = service;

    [HttpGet("provinces")]
    public async Task<IActionResult> GetProvinces(CancellationToken ct)
        => ToActionResult(await _service.GetProvincesAsync(ct));

    [HttpGet("provinces/{provinceId:guid}/districts")]
    public async Task<IActionResult> GetDistricts(Guid provinceId, CancellationToken ct)
        => ToActionResult(await _service.GetDistrictsAsync(provinceId, ct));

    [HttpGet("districts/{districtId:guid}/wards")]
    public async Task<IActionResult> GetWards(Guid districtId, CancellationToken ct)
        => ToActionResult(await _service.GetWardsAsync(districtId, ct));

    /// <summary>Tra ngược phường → quận → tỉnh. FE dùng để dựng lại dropdown khi mở form sửa địa chỉ đã lưu.</summary>
    [HttpGet("wards/{wardId:guid}/path")]
    public async Task<IActionResult> GetWardPath(Guid wardId, CancellationToken ct)
        => ToActionResult(await _service.GetWardPathAsync(wardId, ct));
}
