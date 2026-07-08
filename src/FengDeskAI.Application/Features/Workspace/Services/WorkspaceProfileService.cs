using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Application.Features.Workspace.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Recommendation;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Application.Features.Workspace.Services;

public class WorkspaceProfileService : IWorkspaceProfileService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<WorkspaceProfileService> _logger;

    public WorkspaceProfileService(IUnitOfWork uow, IMapper mapper, ILogger<WorkspaceProfileService> logger)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IServiceResult<List<WorkspaceProfileResponse>>> GetMineAsync(Guid userId, CancellationToken ct = default)
    {
        var profiles = await _uow.WorkspaceProfiles.GetByUserIdAsync(userId, ct);
        return ServiceResult<List<WorkspaceProfileResponse>>.Success(
            _mapper.Map<List<WorkspaceProfileResponse>>(profiles));
    }

    public async Task<IServiceResult<WorkspaceProfileResponse>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var profile = await _uow.WorkspaceProfiles.GetByIdForUserAsync(id, userId, ct);
        if (profile is null)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.WorkspaceProfile.NotFound);

        return ServiceResult<WorkspaceProfileResponse>.Success(_mapper.Map<WorkspaceProfileResponse>(profile));
    }

    public async Task<IServiceResult<WorkspaceElementAnalysisResponse>> GetElementAnalysisAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var profile = await _uow.WorkspaceProfiles.GetByIdForUserAsync(id, userId, ct);
        if (profile is null)
            return ServiceResult<WorkspaceElementAnalysisResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.WorkspaceProfile.NotFound);

        var analysis = await AnalyzeAsync(profile, ct);

        // Sắp giảm dần theo Gap: thiếu nhất (gap dương lớn) → thừa nhất (gap âm).
        var rows = analysis.Ideal.Enumerate().Select(x => new ElementAnalysisRow
        {
            Element = x.Element.ToString(),
            Ideal = Math.Round(x.Value, 3),
            AdjustedIdeal = Math.Round(analysis.AdjustedIdeal[x.Element], 3),
            Current = Math.Round(analysis.Current[x.Element], 3),
            Gap = Math.Round(analysis.Gap[x.Element], 3),
        })
        .OrderByDescending(r => r.Gap)
        .ToList();

        var response = new WorkspaceElementAnalysisResponse
        {
            WorkspaceProfileId = profile.Id,
            DominantNeed = analysis.Gap.Dominant().ToString(),
            Elements = rows,
        };

        return ServiceResult<WorkspaceElementAnalysisResponse>.Success(response);
    }

    /// <summary>Nạp dữ liệu cấu hình rồi dựng 4 vector ngũ hành cho workspace (dùng chung công thức với engine).</summary>
    private async Task<WorkspaceElementAnalysis> AnalyzeAsync(
        Domain.Entities.Workspace.WorkspaceProfile profile, CancellationToken ct)
    {
        var typeElements = new List<WorkspaceTypeElement>();
        if (profile.WorkspaceTypeId is { } typeId
            && await _uow.WorkspaceTypes.GetByIdAsync(typeId, ct) is not null)
        {
            typeElements = await _uow.ScoringConfig.GetWorkspaceTypeElementsAsync(typeId, ct);
        }

        var resolver = new ElementInputResolver(await _uow.ScoringConfig.GetElementInputMapAsync(ct));
        var modifiers = await _uow.ScoringConfig.GetWorkPurposeModifiersAsync(profile.WorkPurpose, ct);
        var profileInputs = await _uow.ScoringConfig.GetWorkspaceProfileInputsAsync(profile.Id, ct);

        return WorkspaceElementAnalyzer.Analyze(typeElements, modifiers, profileInputs, resolver);
    }

    public async Task<IServiceResult<WorkspaceProfileResponse>> GetDefaultAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await _uow.WorkspaceProfiles.GetDefaultByUserIdAsync(userId, ct);
        if (profile is null)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.WorkspaceProfile.NoDefault);

        return ServiceResult<WorkspaceProfileResponse>.Success(_mapper.Map<WorkspaceProfileResponse>(profile));
    }

    public async Task<IServiceResult<WorkspaceProfileResponse>> CreateAsync(Guid userId, CreateWorkspaceProfileRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.WorkspaceProfile.NameRequired);
        if (request.DeskArea <= 0)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.WorkspaceProfile.SurfaceAreaInvalid);
        if (request.WorkspaceTypeId is { } createTypeId && !await _uow.WorkspaceTypes.IsAvailableToUserAsync(createTypeId, userId, ct))
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, "Loại không gian không hợp lệ.");
        if (string.IsNullOrWhiteSpace(request.StyleCode) || await _uow.Styles.GetByIdAsync(request.StyleCode, ct) is null)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, "Mã phong cách (style) không hợp lệ.");

        var entity = _mapper.Map<Domain.Entities.Workspace.WorkspaceProfile>(request);
        entity.UserId = userId;
        entity.Name = request.Name.Trim();

        var anyExisting = (await _uow.WorkspaceProfiles.GetByUserIdAsync(userId, ct)).Count > 0;

        if (request.IsDefault || !anyExisting)
        {
            await _uow.WorkspaceProfiles.ClearDefaultsForUserAsync(userId, ct);
            entity.IsDefault = true;
        }

        await _uow.WorkspaceProfiles.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Workspace profile created: {ProfileId} for user {UserId}", entity.Id, userId);
        return ServiceResult<WorkspaceProfileResponse>.Success(
            _mapper.Map<WorkspaceProfileResponse>(entity),
            ApiStatusMessages.WorkspaceProfile.Created,
            ApiStatusCodes.Created);
    }

    public async Task<IServiceResult<WorkspaceProfileResponse>> UpdateAsync(Guid id, Guid userId, UpdateWorkspaceProfileRequest request, CancellationToken ct = default)
    {
        var profile = await _uow.WorkspaceProfiles.GetByIdForUserAsync(id, userId, ct);
        if (profile is null)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.WorkspaceProfile.NotFound);

        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.WorkspaceProfile.NameRequired);
        if (request.DeskArea <= 0)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.WorkspaceProfile.SurfaceAreaInvalid);
        if (request.WorkspaceTypeId is { } updateTypeId && !await _uow.WorkspaceTypes.IsAvailableToUserAsync(updateTypeId, userId, ct))
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, "Loại không gian không hợp lệ.");
        if (string.IsNullOrWhiteSpace(request.StyleCode) || await _uow.Styles.GetByIdAsync(request.StyleCode, ct) is null)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, "Mã phong cách (style) không hợp lệ.");

        _mapper.Map(request, profile);
        profile.Name = request.Name.Trim();
        _uow.WorkspaceProfiles.Update(profile);
        await _uow.SaveChangesAsync(ct);

        return ServiceResult<WorkspaceProfileResponse>.Success(
            _mapper.Map<WorkspaceProfileResponse>(profile),
            ApiStatusMessages.WorkspaceProfile.Updated);
    }

    public async Task<IServiceResult<WorkspaceProfileResponse>> SetDefaultAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var profile = await _uow.WorkspaceProfiles.GetByIdForUserAsync(id, userId, ct);
        if (profile is null)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.WorkspaceProfile.NotFound);

        return await _uow.ExecuteInTransactionAsync(async _ =>
        {
            await _uow.WorkspaceProfiles.ClearDefaultsForUserAsync(userId, ct);
            profile.IsDefault = true;
            _uow.WorkspaceProfiles.Update(profile);
            return ServiceResult<WorkspaceProfileResponse>.Success(
                _mapper.Map<WorkspaceProfileResponse>(profile),
                ApiStatusMessages.WorkspaceProfile.SetDefault);
        }, ct);
    }

    public async Task<IServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var profile = await _uow.WorkspaceProfiles.GetByIdForUserAsync(id, userId, ct);
        if (profile is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.WorkspaceProfile.NotFound);

        _uow.WorkspaceProfiles.Remove(profile); // SaveChangesAsync biến thành soft-delete
        await _uow.SaveChangesAsync(ct);

        return ServiceResult.Success(ApiStatusMessages.WorkspaceProfile.Deleted);
    }
}
