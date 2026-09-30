using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Services;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

[Route("api/[controller]")]
[Authorize]
public class ReviewController : ApiControllerBase
{
    private readonly IReviewService _service;

    public ReviewController(IReviewService service) => _service = service;

    /// <summary>Đánh giá công khai, phân trang — lọc theo <c>productId</c> và/hoặc <c>storeId</c>.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetList([FromQuery] ReviewQueryParams query, CancellationToken ct)
        => ToActionResult(await _service.GetListAsync(query, ct));

    /// <summary>Điểm trung bình + phân bố sao của MỘT sản phẩm hoặc MỘT cửa hàng.</summary>
    [HttpGet("summary")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSummary([FromQuery] RatingSummaryQuery query, CancellationToken ct)
        => ToActionResult(await _service.GetSummaryAsync(query, ct));

    [HttpGet("my")]
    public async Task<IActionResult> GetMy(CancellationToken ct)
        => ToActionResult(await _service.GetMyAsync(CurrentUserId, ct));

    /// <summary>User hiện tại có được đánh giá sản phẩm này không (và vì sao nếu không).</summary>
    [HttpGet("eligibility")]
    public async Task<IActionResult> GetEligibility([FromQuery] Guid productId, CancellationToken ct)
        => ToActionResult(await _service.GetEligibilityAsync(CurrentUserId, productId, ct));

    /// <summary>Trạng thái đánh giá từng dòng của một đơn của chính user.</summary>
    [HttpGet("orders/{orderId:guid}/items")]
    public async Task<IActionResult> GetOrderItems(Guid orderId, CancellationToken ct)
        => ToActionResult(await _service.GetOrderItemsAsync(CurrentUserId, orderId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequest request, CancellationToken ct)
        => ToActionResult(await _service.CreateAsync(CurrentUserId, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReviewRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpdateAsync(id, CurrentUserId, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToActionResult(await _service.DeleteAsync(id, CurrentUserId, ct));
}
