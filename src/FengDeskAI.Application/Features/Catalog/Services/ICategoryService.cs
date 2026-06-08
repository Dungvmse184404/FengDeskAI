using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Catalog.DTOs;

namespace FengDeskAI.Application.Features.Catalog.Services;

public interface ICategoryService
{
    Task<IServiceResult<List<CategoryResponse>>> GetAllAsync(CancellationToken ct = default);
    Task<IServiceResult<CategoryResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IServiceResult<CategoryResponse>> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<IServiceResult<CategoryResponse>> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default);
    Task<IServiceResult> DeleteAsync(Guid id, CancellationToken ct = default);
}
