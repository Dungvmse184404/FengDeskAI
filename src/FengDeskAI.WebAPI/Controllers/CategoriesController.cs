using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Application.Features.Catalog.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>Danh mục sản phẩm. Đọc public; CRUD cho Manager trở lên.</summary>
[Route("api/categories")]
[Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
public class CategoriesController : ApiControllerBase
{
    private readonly ICategoryService _service;

    public CategoriesController(ICategoryService service) => _service = service;

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => ToActionResult(await _service.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request, CancellationToken ct)
        => ToActionResult(await _service.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToActionResult(await _service.DeleteAsync(id, ct));
}
