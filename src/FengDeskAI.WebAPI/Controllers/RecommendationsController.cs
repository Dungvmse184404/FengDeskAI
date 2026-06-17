using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Services;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Gợi ý sản phẩm phong thủy cho một workspace của user. Engine .NET chấm điểm,
/// AI diễn giải. User chỉ truy cập gợi ý của chính mình.
/// </summary>
[Route("api/recommendations")]
[Authorize]
public class RecommendationsController : ApiControllerBase
{
    private readonly IRecommendationService _service;

    public RecommendationsController(IRecommendationService service)
    {
        _service = service;
    }

    /// <summary>Tạo phiên gợi ý cho workspace đã chọn.</summary>
    [HttpPost]
    public async Task<IActionResult> Generate([FromBody] GenerateRecommendationRequest request, CancellationToken ct)
        => ToActionResult(await _service.GenerateAsync(CurrentUserId, request, ct));

    /// <summary>Lấy lại một phiên gợi ý đã lưu.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetByIdAsync(id, CurrentUserId, ct));
}
