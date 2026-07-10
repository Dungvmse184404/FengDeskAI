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
        var responses = new List<WorkspaceProfileResponse>(profiles.Count);
        foreach (var profile in profiles)
            responses.Add(await ToResponseAsync(profile, ct));

        return ServiceResult<List<WorkspaceProfileResponse>>.Success(responses);
    }

    public async Task<IServiceResult<WorkspaceProfileResponse>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var profile = await _uow.WorkspaceProfiles.GetByIdForUserAsync(id, userId, ct);
        if (profile is null)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.WorkspaceProfile.NotFound);

        return ServiceResult<WorkspaceProfileResponse>.Success(await ToResponseAsync(profile, ct));
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

        return ServiceResult<WorkspaceProfileResponse>.Success(await ToResponseAsync(profile, ct));
    }

    public async Task<IServiceResult<WorkspaceProfileResponse>> CreateAsync(Guid userId, CreateWorkspaceProfileRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.WorkspaceProfile.NameRequired);
        if (request.DeskArea is <= 0)
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

        if (request.Inputs is { Count: > 0 })
        {
            var validInputs = await ResolveValidInputsAsync(request.Inputs, ct);
            await _uow.ScoringConfig.ReplaceWorkspaceProfileInputsAsync(entity.Id, validInputs, ct);
        }

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Workspace profile created: {ProfileId} for user {UserId}", entity.Id, userId);
        return ServiceResult<WorkspaceProfileResponse>.Success(
            await ToResponseAsync(entity, ct),
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
        if (request.DeskArea is <= 0)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.WorkspaceProfile.SurfaceAreaInvalid);
        if (request.WorkspaceTypeId is { } updateTypeId && !await _uow.WorkspaceTypes.IsAvailableToUserAsync(updateTypeId, userId, ct))
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, "Loại không gian không hợp lệ.");
        if (string.IsNullOrWhiteSpace(request.StyleCode) || await _uow.Styles.GetByIdAsync(request.StyleCode, ct) is null)
            return ServiceResult<WorkspaceProfileResponse>.Failure(ApiStatusCodes.BadRequest, "Mã phong cách (style) không hợp lệ.");

        _mapper.Map(request, profile);
        profile.Name = request.Name.Trim();
        _uow.WorkspaceProfiles.Update(profile);

        // null = không đổi input hiện có; [] (rỗng nhưng không null) = xóa hết.
        if (request.Inputs is not null)
        {
            var validInputs = await ResolveValidInputsAsync(request.Inputs, ct);
            await _uow.ScoringConfig.ReplaceWorkspaceProfileInputsAsync(profile.Id, validInputs, ct);
        }

        await _uow.SaveChangesAsync(ct);

        return ServiceResult<WorkspaceProfileResponse>.Success(
            await ToResponseAsync(profile, ct),
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
                await ToResponseAsync(profile, ct),
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

    /// <summary>Chỉ giữ input có (kind, code) tồn tại trong element_input_map — vd draft AI intake không bịa được.</summary>
    private async Task<List<WorkspaceProfileInput>> ResolveValidInputsAsync(
        IEnumerable<WorkspaceProfileInputDto> inputs, CancellationToken ct)
    {
        var validCodes = (await _uow.ScoringConfig.GetElementInputMapAsync(ct))
            .Select(m => (m.InputKind, m.InputCode))
            .ToHashSet();

        return inputs
            .Where(i => validCodes.Contains((i.InputKind, i.InputCode)))
            .DistinctBy(i => (i.InputKind, i.InputCode))
            .Select(i => new WorkspaceProfileInput { InputKind = i.InputKind, InputCode = i.InputCode })
            .ToList();
    }

    /// <summary>Map entity → response + tính % hoàn thiện hồ sơ (không lưu DB, chỉ để FE hiện progress).</summary>
    private async Task<WorkspaceProfileResponse> ToResponseAsync(
        Domain.Entities.Workspace.WorkspaceProfile profile, CancellationToken ct)
    {
        var response = _mapper.Map<WorkspaceProfileResponse>(profile);
        var (percent, hints) = await ComputeCompletenessAsync(profile, ct);
        response.CompletenessPercent = percent;
        response.MissingFieldHints = hints;
        return response;
    }

    private async Task<(int Percent, List<string> Hints)> ComputeCompletenessAsync(
        Domain.Entities.Workspace.WorkspaceProfile profile, CancellationToken ct)
    {
        var hasProfileInput = (await _uow.ScoringConfig.GetWorkspaceProfileInputsAsync(profile.Id, ct)).Count > 0;

        var checks = new (bool HasValue, string Hint)[]
        {
            (profile.WorkspaceTypeId.HasValue, "Chọn loại không gian để tính trọng số cá nhân chính xác hơn"),
            (profile.Lighting.HasValue, "Thêm ánh sáng phòng để gợi ý vật phẩm hợp không gian hơn"),
            (profile.DeskType.HasValue, "Thêm loại bàn để lọc vật phẩm vừa kích thước"),
            (profile.DeskOrientation.HasValue, "Thêm hướng bàn để AI diễn giải sát hơn"),
            (profile.RoomFacingDirection.HasValue, "Thêm hướng phòng để AI diễn giải sát hơn"),
            (profile.DeskArea.HasValue, "Thêm diện tích mặt bàn để lọc vật phẩm vừa kích thước"),
            (profile.EntranceDirection.HasValue, "Thêm hướng cửa ra vào để nhận gợi ý vị trí đặt"),
            (profile.ToiletDirection.HasValue, "Thêm hướng nhà vệ sinh để tránh gợi ý sai vị trí"),
            (hasProfileInput, "Mô tả thêm màu sắc/vật liệu không gian để engine tính ngũ hành sát hơn"),
        };

        var filled = checks.Count(c => c.HasValue);
        var percent = (int)Math.Round(filled * 100.0 / checks.Length);
        var hints = checks.Where(c => !c.HasValue).Select(c => c.Hint).ToList();
        return (percent, hints);
    }
}
