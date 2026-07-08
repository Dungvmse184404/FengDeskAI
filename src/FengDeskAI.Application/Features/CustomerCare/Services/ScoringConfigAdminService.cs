using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Recommendation;

namespace FengDeskAI.Application.Features.CustomerCare.Services;

public sealed class ScoringConfigAdminService : IScoringConfigAdminService
{
    private readonly IGenericRepository<ScoringParam> _params;
    private readonly IGenericRepository<ElementInputMap> _inputMap;
    private readonly IGenericRepository<WorkPurposeElementModifier> _modifiers;
    private readonly IGenericRepository<WorkspaceTypeElement> _typeElements;
    private readonly IUnitOfWork _uow;

    public ScoringConfigAdminService(
        IGenericRepository<ScoringParam> paramsRepo,
        IGenericRepository<ElementInputMap> inputMap,
        IGenericRepository<WorkPurposeElementModifier> modifiers,
        IGenericRepository<WorkspaceTypeElement> typeElements,
        IUnitOfWork uow)
    {
        _params = paramsRepo;
        _inputMap = inputMap;
        _modifiers = modifiers;
        _typeElements = typeElements;
        _uow = uow;
    }

    // ── scoring_params ──

    public async Task<IServiceResult<List<ScoringParamDto>>> GetParamsAsync(CancellationToken ct = default)
    {
        var rows = await _params.GetAllAsync(ct);
        return ServiceResult<List<ScoringParamDto>>.Success(rows
            .OrderBy(r => r.Code)
            .Select(r => new ScoringParamDto { Id = r.Id, Code = r.Code, Value = r.Value, Description = r.Description })
            .ToList());
    }

    public async Task<IServiceResult<ScoringParamDto>> UpsertParamAsync(string code, UpsertScoringParamRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return ServiceResult<ScoringParamDto>.Failure(ApiStatusCodes.BadRequest, "Thiếu code tham số.");

        var existing = (await _params.FindAsync(x => x.Code == code, ct)).FirstOrDefault();
        if (existing is null)
        {
            existing = new ScoringParam { Code = code, Value = request.Value, Description = request.Description };
            await _params.AddAsync(existing, ct);
        }
        else
        {
            existing.Value = request.Value;
            existing.Description = request.Description;
            _params.Update(existing);
        }
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<ScoringParamDto>.Success(
            new ScoringParamDto { Id = existing.Id, Code = existing.Code, Value = existing.Value, Description = existing.Description },
            "Lưu tham số thành công.");
    }

    // ── element_input_map ──

    public async Task<IServiceResult<List<ElementInputMapDto>>> GetElementInputsAsync(CancellationToken ct = default)
    {
        var rows = await _inputMap.GetAllAsync(ct);
        return ServiceResult<List<ElementInputMapDto>>.Success(rows
            .OrderBy(r => r.InputKind).ThenBy(r => r.InputCode)
            .Select(ToDto).ToList());
    }

    public async Task<IServiceResult<ElementInputMapDto>> UpsertElementInputAsync(UpsertElementInputMapRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.InputCode))
            return ServiceResult<ElementInputMapDto>.Failure(ApiStatusCodes.BadRequest, "Thiếu input code.");
        if (request.Weight <= 0m)
            return ServiceResult<ElementInputMapDto>.Failure(ApiStatusCodes.BadRequest, "Weight phải > 0.");

        var existing = (await _inputMap.FindAsync(
            x => x.InputKind == request.InputKind && x.InputCode == request.InputCode && x.Element == request.Element, ct))
            .FirstOrDefault();

        if (existing is null)
        {
            existing = new ElementInputMap
            {
                InputKind = request.InputKind,
                InputCode = request.InputCode,
                Element = request.Element,
                Weight = request.Weight,
            };
            await _inputMap.AddAsync(existing, ct);
        }
        else
        {
            existing.Weight = request.Weight;
            _inputMap.Update(existing);
        }
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<ElementInputMapDto>.Success(ToDto(existing), "Lưu map ngũ hành thành công.");
    }

    public async Task<IServiceResult> DeleteElementInputAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _inputMap.GetByIdAsync(id, ct);
        if (entity is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy map.");
        _inputMap.Remove(entity);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã xóa map.");
    }

    // ── work_purpose_element_modifiers ──

    public async Task<IServiceResult<List<WorkPurposeModifierDto>>> GetPurposeModifiersAsync(CancellationToken ct = default)
    {
        var rows = await _modifiers.GetAllAsync(ct);
        return ServiceResult<List<WorkPurposeModifierDto>>.Success(rows
            .OrderBy(r => r.WorkPurpose).ThenBy(r => r.Element)
            .Select(ToDto).ToList());
    }

    public async Task<IServiceResult<WorkPurposeModifierDto>> UpsertPurposeModifierAsync(UpsertWorkPurposeModifierRequest request, CancellationToken ct = default)
    {
        var existing = (await _modifiers.FindAsync(
            x => x.WorkPurpose == request.WorkPurpose && x.Element == request.Element, ct)).FirstOrDefault();

        if (existing is null)
        {
            existing = new WorkPurposeElementModifier
            {
                WorkPurpose = request.WorkPurpose,
                Element = request.Element,
                Delta = request.Delta,
            };
            await _modifiers.AddAsync(existing, ct);
        }
        else
        {
            existing.Delta = request.Delta;
            _modifiers.Update(existing);
        }
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<WorkPurposeModifierDto>.Success(ToDto(existing), "Lưu modifier thành công.");
    }

    public async Task<IServiceResult> DeletePurposeModifierAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _modifiers.GetByIdAsync(id, ct);
        if (entity is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy modifier.");
        _modifiers.Remove(entity);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã xóa modifier.");
    }

    // ── workspace_type_elements ──

    public async Task<IServiceResult<List<WorkspaceTypeElementDto>>> GetWorkspaceTypeElementsAsync(Guid? workspaceTypeId, CancellationToken ct = default)
    {
        var rows = workspaceTypeId is { } id
            ? await _typeElements.FindAsync(x => x.WorkspaceTypeId == id, ct)
            : await _typeElements.GetAllAsync(ct);
        return ServiceResult<List<WorkspaceTypeElementDto>>.Success(rows
            .OrderBy(r => r.WorkspaceTypeId).ThenBy(r => r.Source).ThenBy(r => r.Element)
            .Select(ToDto).ToList());
    }

    public async Task<IServiceResult<WorkspaceTypeElementDto>> UpsertWorkspaceTypeElementAsync(UpsertWorkspaceTypeElementRequest request, CancellationToken ct = default)
    {
        if (!IsValidSource(request.Source))
            return ServiceResult<WorkspaceTypeElementDto>.Failure(ApiStatusCodes.BadRequest,
                $"Source phải là '{WorkspaceElementSources.Ideal}' hoặc '{WorkspaceElementSources.Interior}'.");
        if (request.Weight < 0m)
            return ServiceResult<WorkspaceTypeElementDto>.Failure(ApiStatusCodes.BadRequest, "Weight không được âm.");

        var existing = (await _typeElements.FindAsync(
            x => x.WorkspaceTypeId == request.WorkspaceTypeId && x.Source == request.Source && x.Element == request.Element, ct))
            .FirstOrDefault();

        if (existing is null)
        {
            existing = new WorkspaceTypeElement
            {
                WorkspaceTypeId = request.WorkspaceTypeId,
                Source = request.Source,
                Element = request.Element,
                Weight = request.Weight,
            };
            await _typeElements.AddAsync(existing, ct);
        }
        else
        {
            existing.Weight = request.Weight;
            _typeElements.Update(existing);
        }
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<WorkspaceTypeElementDto>.Success(ToDto(existing), "Lưu vector loại phòng thành công.");
    }

    public async Task<IServiceResult> DeleteWorkspaceTypeElementAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _typeElements.GetByIdAsync(id, ct);
        if (entity is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy row.");
        _typeElements.Remove(entity);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã xóa row.");
    }

    // ── mappers ──

    private static bool IsValidSource(string? s) =>
        string.Equals(s, WorkspaceElementSources.Ideal, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(s, WorkspaceElementSources.Interior, StringComparison.OrdinalIgnoreCase);

    private static ElementInputMapDto ToDto(ElementInputMap r) => new()
    {
        Id = r.Id, InputKind = r.InputKind, InputCode = r.InputCode, Element = r.Element, Weight = r.Weight,
    };

    private static WorkPurposeModifierDto ToDto(WorkPurposeElementModifier r) => new()
    {
        Id = r.Id, WorkPurpose = r.WorkPurpose, Element = r.Element, Delta = r.Delta,
    };

    private static WorkspaceTypeElementDto ToDto(WorkspaceTypeElement r) => new()
    {
        Id = r.Id, WorkspaceTypeId = r.WorkspaceTypeId, Source = r.Source, Element = r.Element, Weight = r.Weight,
    };
}
