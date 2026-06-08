using FengDeskAI.Application.Features.Sales.DTOs;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>Giỏ hàng của user đang đăng nhập.</summary>
[Route("api/cart")]
[Authorize]
public class CartController : ApiControllerBase
{
    private readonly ICartService _service;

    public CartController(ICartService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct)
        => ToActionResult(await _service.GetMineAsync(CurrentUserId, ct));

    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] AddCartItemRequest request, CancellationToken ct)
        => ToActionResult(await _service.AddItemAsync(CurrentUserId, request, ct));

    [HttpPut("items/{itemId:guid}")]
    public async Task<IActionResult> UpdateItem(Guid itemId, [FromBody] UpdateCartItemRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpdateItemAsync(CurrentUserId, itemId, request, ct));

    [HttpDelete("items/{itemId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid itemId, CancellationToken ct)
        => ToActionResult(await _service.RemoveItemAsync(CurrentUserId, itemId, ct));

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken ct)
        => ToActionResult(await _service.ClearAsync(CurrentUserId, ct));
}
