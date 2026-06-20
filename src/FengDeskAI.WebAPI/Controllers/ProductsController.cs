using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
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
    private readonly IProductModel3DService _model3DService;

    public ProductsController(IProductService service, IProductModel3DService model3DService)
    {
        _service = service;
        _model3DService = model3DService;
    }

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

    /// <summary>Tải ảnh sản phẩm (multipart/form-data, field "file"). Lưu vào storage rồi gắn URL vào sản phẩm.</summary>
    [HttpPost("{id:guid}/images")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file, [FromForm] int sortOrder = 0, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return ToActionResult(ServiceResult<ProductImageResponse>.Failure(
                ApiStatusCodes.BadRequest, ApiStatusMessages.Product.ImageFileRequired));

        await using var stream = file.OpenReadStream();
        return ToActionResult(await _service.UploadImageAsync(
            id, CurrentUserId, IsAdmin, stream, file.FileName, file.ContentType, sortOrder, ct));
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken ct)
        => ToActionResult(await _service.DeleteImageAsync(id, imageId, CurrentUserId, IsAdmin, ct));

    // ----- Model 3D (sinh từ ảnh qua Meshy AI) -----

    /// <summary>Trạng thái/kết quả model 3D của sản phẩm.</summary>
    [HttpGet("{id:guid}/model-3d")]
    [AllowAnonymous]
    public async Task<IActionResult> GetModel3D(Guid id, CancellationToken ct)
        => ToActionResult(await _model3DService.GetAsync(id, ct));

    /// <summary>Yêu cầu sinh model 3D từ một ảnh sản phẩm (xử lý nền). Trả về 202 + trạng thái Processing.</summary>
    [HttpPost("{id:guid}/model-3d")]
    public async Task<IActionResult> GenerateModel3D(Guid id, [FromBody] GenerateModel3DRequest request, CancellationToken ct)
        => ToActionResult(await _model3DService.GenerateAsync(id, CurrentUserId, IsAdmin, request ?? new GenerateModel3DRequest(), ct));

    [HttpDelete("{id:guid}/model-3d")]
    public async Task<IActionResult> DeleteModel3D(Guid id, CancellationToken ct)
        => ToActionResult(await _model3DService.DeleteAsync(id, CurrentUserId, IsAdmin, ct));

    // ----- Category / Tag links -----

    [HttpPut("{id:guid}/categories")]
    public async Task<IActionResult> SetCategories(Guid id, [FromBody] SetCategoriesRequest request, CancellationToken ct)
        => ToActionResult(await _service.SetCategoriesAsync(id, CurrentUserId, IsAdmin, request, ct));

    [HttpPut("{id:guid}/tags")]
    public async Task<IActionResult> SetTags(Guid id, [FromBody] SetTagsRequest request, CancellationToken ct)
        => ToActionResult(await _service.SetTagsAsync(id, CurrentUserId, IsAdmin, request, ct));

    // ----- Thuộc tính phong thủy (ứng viên gợi ý) -----

    [HttpPut("{id:guid}/feng-shui")]
    public async Task<IActionResult> SetFengShui(Guid id, [FromBody] SetProductFengShuiRequest request, CancellationToken ct)
        => ToActionResult(await _service.SetFengShuiAsync(id, CurrentUserId, IsAdmin, request, ct));
}
