using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Application.Features.Catalog.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Sản phẩm. Đọc (list/detail) public; ghi (product + items/images/category/tag links)
/// yêu cầu owner/staff của store sở hữu sản phẩm — quyền sở hữu kiểm tra ở service layer.
/// </summary>
[Route("api/products")]
[Authorize]
public class ProductsController : ApiControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service) => _service = service;

    private bool IsAdmin => User.IsInRole(Roles.Admin);

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Search([FromQuery] ProductQueryParams query, CancellationToken ct)
        => ToActionResult(await _service.SearchAsync(query, ct));

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken ct)
        => ToActionResult(await _service.CreateAsync(CurrentUserId, IsAdmin, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpdateAsync(id, CurrentUserId, IsAdmin, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToActionResult(await _service.DeleteAsync(id, CurrentUserId, IsAdmin, ct));

    // ----- Product items (SKU) -----

    [HttpPost("{id:guid}/items")]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] CreateProductItemRequest request, CancellationToken ct)
        => ToActionResult(await _service.AddItemAsync(id, CurrentUserId, IsAdmin, request, ct));

    [HttpPut("{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> UpdateItem(Guid id, Guid itemId, [FromBody] UpdateProductItemRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpdateItemAsync(id, itemId, CurrentUserId, IsAdmin, request, ct));

    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid id, Guid itemId, CancellationToken ct)
        => ToActionResult(await _service.DeleteItemAsync(id, itemId, CurrentUserId, IsAdmin, ct));

    // ----- Product images -----

    [HttpPost("{id:guid}/images")]
    public async Task<IActionResult> AddImage(Guid id, [FromBody] CreateProductImageRequest request, CancellationToken ct)
        => ToActionResult(await _service.AddImageAsync(id, CurrentUserId, IsAdmin, request, ct));

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken ct)
        => ToActionResult(await _service.DeleteImageAsync(id, imageId, CurrentUserId, IsAdmin, ct));

    // ----- Category / Tag links -----

    [HttpPut("{id:guid}/categories")]
    public async Task<IActionResult> SetCategories(Guid id, [FromBody] SetCategoriesRequest request, CancellationToken ct)
        => ToActionResult(await _service.SetCategoriesAsync(id, CurrentUserId, IsAdmin, request, ct));

    [HttpPut("{id:guid}/tags")]
    public async Task<IActionResult> SetTags(Guid id, [FromBody] SetTagsRequest request, CancellationToken ct)
        => ToActionResult(await _service.SetTagsAsync(id, CurrentUserId, IsAdmin, request, ct));
}
