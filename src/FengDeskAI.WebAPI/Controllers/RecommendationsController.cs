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

    /// <summary>Gợi ý vật phẩm mang theo người (đeo tay, mặt dây, treo xe) — chấm theo bản mệnh, không cần workspace.</summary>
    [HttpPost("personal")]
    public async Task<IActionResult> GeneratePersonal(
        [FromBody] GeneratePersonalRecommendationRequest request, CancellationToken ct)
        => ToActionResult(await _service.GeneratePersonalAsync(CurrentUserId, request, ct));

    /// <summary>Lấy lại một phiên gợi ý đã lưu.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetByIdAsync(id, CurrentUserId, ct));

    /// <summary>Độ phù hợp của 1 sản phẩm với 1 workspace — không loại sản phẩm, cho trang chi tiết sản phẩm.</summary>
    [HttpGet("fit")]
    public async Task<IActionResult> GetProductFit(
        [FromQuery] Guid productId, [FromQuery] Guid workspaceProfileId, CancellationToken ct)
        => ToActionResult(await _service.GetProductFitAsync(productId, workspaceProfileId, CurrentUserId, ct));

    /// <summary>
    /// Độ phù hợp của 1 sản phẩm với BẢN MỆNH user — không cần workspace. Dành cho vật phẩm mang theo
    /// người, thứ mà <c>GET fit</c> chấm sai bản chất vì nó luôn chấm theo gap của một phòng.
    /// </summary>
    [HttpGet("fit/personal")]
    public async Task<IActionResult> GetPersonalFit([FromQuery] Guid productId, CancellationToken ct)
        => ToActionResult(await _service.GetPersonalFitAsync(productId, CurrentUserId, ct));
}
