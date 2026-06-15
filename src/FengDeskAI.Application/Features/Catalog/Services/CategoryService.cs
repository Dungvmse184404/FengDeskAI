using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;

namespace FengDeskAI.Application.Features.Catalog.Services;

public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public CategoryService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<IServiceResult<List<CategoryResponse>>> GetAllAsync(CancellationToken ct = default)
        => ServiceResult<List<CategoryResponse>>.Success(
            _mapper.Map<List<CategoryResponse>>(await _uow.Categories.GetAllOrderedAsync(ct)));

    public async Task<IServiceResult<CategoryResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _uow.Categories.GetByIdAsync(id, ct);
        if (c is null) return ServiceResult<CategoryResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Category.NotFound);
        return ServiceResult<CategoryResponse>.Success(_mapper.Map<CategoryResponse>(c));
    }

    public async Task<IServiceResult<CategoryResponse>> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<CategoryResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Category.NameRequired);
        if (request.ParentId is { } pid && !await _uow.Categories.ExistsAsync(pid, ct))
            return ServiceResult<CategoryResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Category.ParentNotFound);

        var entity = _mapper.Map<Category>(request);
        entity.Name = request.Name.Trim();
        entity.IsActive = true;
        await _uow.Categories.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<CategoryResponse>.Success(_mapper.Map<CategoryResponse>(entity), ApiStatusMessages.Category.Created, ApiStatusCodes.Created);
    }

    public async Task<IServiceResult<CategoryResponse>> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var entity = await _uow.Categories.GetByIdAsync(id, ct);
        if (entity is null) return ServiceResult<CategoryResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Category.NotFound);
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<CategoryResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Category.NameRequired);
        if (request.ParentId == id)
            return ServiceResult<CategoryResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Category.SelfParent);
        if (request.ParentId is { } pid && !await _uow.Categories.ExistsAsync(pid, ct))
            return ServiceResult<CategoryResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Category.ParentNotFound);

        entity.Name = request.Name.Trim();
        entity.Description = request.Description;
        entity.ParentId = request.ParentId;
        entity.IsActive = request.IsActive;
        _uow.Categories.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<CategoryResponse>.Success(_mapper.Map<CategoryResponse>(entity), ApiStatusMessages.Category.Updated);
    }

    public async Task<IServiceResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _uow.Categories.GetByIdAsync(id, ct);
        if (entity is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Category.NotFound);
        _uow.Categories.Remove(entity);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success(ApiStatusMessages.Category.Deleted);
    }
}
