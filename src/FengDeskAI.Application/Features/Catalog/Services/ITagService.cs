using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Catalog.DTOs;

namespace FengDeskAI.Application.Features.Catalog.Services;

public interface ITagService
{
    Task<IServiceResult<List<TagResponse>>> GetAllAsync(CancellationToken ct = default);
    Task<IServiceResult<TagResponse>> CreateAsync(CreateTagRequest request, CancellationToken ct = default);
    Task<IServiceResult<TagResponse>> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken ct = default);
    Task<IServiceResult> DeleteAsync(Guid id, CancellationToken ct = default);
}
