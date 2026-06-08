using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;

namespace FengDeskAI.Application.Features.Catalog.Services;

public class TagService : ITagService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public TagService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<IServiceResult<List<TagResponse>>> GetAllAsync(CancellationToken ct = default)
        => ServiceResult<List<TagResponse>>.Success(
            _mapper.Map<List<TagResponse>>(await _uow.Tags.GetAllOrderedAsync(ct)));

    public async Task<IServiceResult<TagResponse>> CreateAsync(CreateTagRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<TagResponse>.Failure(ApiStatusCodes.BadRequest, "Tên tag không được để trống.");
        if (await _uow.Tags.NameExistsAsync(request.Name.Trim(), null, ct))
            return ServiceResult<TagResponse>.Failure(ApiStatusCodes.Conflict, "Tag đã tồn tại.");

        var entity = _mapper.Map<Tag>(request);
        entity.Name = request.Name.Trim();
        await _uow.Tags.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<TagResponse>.Success(_mapper.Map<TagResponse>(entity), "Tạo tag thành công.", ApiStatusCodes.Created);
    }

    public async Task<IServiceResult<TagResponse>> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken ct = default)
    {
        var entity = await _uow.Tags.GetByIdAsync(id, ct);
        if (entity is null) return ServiceResult<TagResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy tag.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<TagResponse>.Failure(ApiStatusCodes.BadRequest, "Tên tag không được để trống.");
        if (await _uow.Tags.NameExistsAsync(request.Name.Trim(), id, ct))
            return ServiceResult<TagResponse>.Failure(ApiStatusCodes.Conflict, "Tên tag đã được dùng.");

        entity.Name = request.Name.Trim();
        entity.Description = request.Description;
        _uow.Tags.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<TagResponse>.Success(_mapper.Map<TagResponse>(entity), "Cập nhật tag thành công.");
    }

    public async Task<IServiceResult> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _uow.Tags.GetByIdAsync(id, ct);
        if (entity is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy tag.");
        _uow.Tags.Remove(entity);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã xóa tag.");
    }
}
