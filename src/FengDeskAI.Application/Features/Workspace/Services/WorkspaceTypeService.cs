using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Workspace.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Workspace;

namespace FengDeskAI.Application.Features.Workspace.Services;

public class WorkspaceTypeService : IWorkspaceTypeService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public WorkspaceTypeService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<IServiceResult<List<WorkspaceTypeResponse>>> GetAvailableAsync(Guid userId, CancellationToken ct = default)
    {
        var types = await _uow.WorkspaceTypes.GetAvailableForUserAsync(userId, ct);
        return ServiceResult<List<WorkspaceTypeResponse>>.Success(_mapper.Map<List<WorkspaceTypeResponse>>(types));
    }

    public async Task<IServiceResult<WorkspaceTypeResponse>> CreateAsync(Guid userId, CreateWorkspaceTypeRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<WorkspaceTypeResponse>.Failure(ApiStatusCodes.BadRequest, "Tên loại không gian là bắt buộc.");

        // Quy tắc: loại tự thêm mặc định trọng số cá nhân 1.0; cho phép override, kẹp [0, 1].
        decimal weight = Math.Clamp(request.PersonalWeight ?? 1.0m, 0m, 1m);

        var entity = new WorkspaceType
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            IsPublic = request.IsPublic,
            PersonalWeight = weight,
            IsSystemSeeded = false, // CreatedBy được audit tự gán = userId
        };

        await _uow.WorkspaceTypes.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);

        return ServiceResult<WorkspaceTypeResponse>.Success(
            _mapper.Map<WorkspaceTypeResponse>(entity), "Tạo loại không gian thành công.", ApiStatusCodes.Created);
    }
}
