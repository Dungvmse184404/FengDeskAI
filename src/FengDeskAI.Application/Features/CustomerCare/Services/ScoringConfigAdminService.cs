using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Services;

public sealed class ScoringConfigAdminService : IScoringConfigAdminService
{
    private readonly IGenericRepository<ScoringParam> _params;
    private readonly IGenericRepository<ElementInputMap> _inputMap;
    private readonly IGenericRepository<WorkPurposeElementModifier> _modifiers;
    private readonly IGenericRepository<WorkspaceTypeElement> _typeElements;
    private readonly IGenericRepository<Occupation> _occupations;
    private readonly IGenericRepository<OccupationElementModifier> _occupationModifiers;
    private readonly IGenericRepository<User> _users;
    private readonly IUnitOfWork _uow;

    public ScoringConfigAdminService(
        IGenericRepository<ScoringParam> paramsRepo,
        IGenericRepository<ElementInputMap> inputMap,
        IGenericRepository<WorkPurposeElementModifier> modifiers,
        IGenericRepository<WorkspaceTypeElement> typeElements,
        IGenericRepository<Occupation> occupations,
        IGenericRepository<OccupationElementModifier> occupationModifiers,
        IGenericRepository<User> users,
        IUnitOfWork uow)
    {
        _params = paramsRepo;
        _inputMap = inputMap;
        _modifiers = modifiers;
        _typeElements = typeElements;
        _occupations = occupations;
        _occupationModifiers = occupationModifiers;
        _users = users;
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

        // Nhãn thuộc về TAG chứ không thuộc từng hành → lấy nhãn đang có của code để row mới kế thừa.
        var siblings = await _inputMap.FindAsync(
            x => x.InputKind == request.InputKind && x.InputCode == request.InputCode, ct);
        var label = string.IsNullOrWhiteSpace(request.LabelVi)
            ? siblings.Select(x => x.LabelVi).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))
            : request.LabelVi.Trim();

        if (existing is null)
        {
            existing = new ElementInputMap
            {
                InputKind = request.InputKind,
                InputCode = request.InputCode,
                LabelVi = label,
                Visibility = ElementInputVisibility.Public, // admin tự thêm → dùng chung ngay
                Element = request.Element,
                Weight = request.Weight,
            };
            await _inputMap.AddAsync(existing, ct);
        }
        else
        {
            existing.Weight = request.Weight;
            existing.LabelVi = label;
            _inputMap.Update(existing);
        }

        // Đồng bộ nhãn cho mọi hành còn lại của cùng code — tránh 1 code 2 nhãn khác nhau.
        if (!string.IsNullOrWhiteSpace(request.LabelVi))
            ApplyLabelToSiblings(siblings, existing, request.LabelVi.Trim());

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

    // ── element_input_map: thao tác theo TAG (kind, code) ──

    public async Task<IServiceResult<List<ElementInputTagDto>>> GetElementInputTagsAsync(
        ElementInputKind? kind, ElementInputVisibility? visibility, bool? isUserCreated,
        CancellationToken ct = default)
    {
        var rows = await _inputMap.GetAllAsync(ct);

        var tags = rows
            .Where(r => kind is null || r.InputKind == kind)
            .GroupBy(r => (r.InputKind, r.InputCode))
            .Select(ToTagDto)
            .Where(t => visibility is null || t.Visibility == visibility)
            .Where(t => isUserCreated is null || t.IsUserCreated == isUserCreated)
            // Tag chờ duyệt lên đầu — đó là việc admin cần xử lý trước.
            .OrderByDescending(t => t.IsPending)
            .ThenBy(t => t.InputKind)
            .ThenBy(t => t.LabelVi, StringComparer.CurrentCulture)
            .ToList();

        return ServiceResult<List<ElementInputTagDto>>.Success(tags);
    }

    public async Task<IServiceResult<ElementInputTagDto>> UpdateElementInputTagAsync(
        ElementInputKind kind, string code, UpdateElementInputTagRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return ServiceResult<ElementInputTagDto>.Failure(ApiStatusCodes.BadRequest, "Thiếu input code.");

        var rows = await _inputMap.FindAsync(x => x.InputKind == kind && x.InputCode == code, ct);
        if (rows.Count == 0)
            return ServiceResult<ElementInputTagDto>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy tag.");

        if (request.Contributions is { } contributions)
        {
            if (contributions.Count == 0)
                return ServiceResult<ElementInputTagDto>.Failure(
                    ApiStatusCodes.BadRequest, "Tag phải thuộc ít nhất 1 hành.");
            if (contributions.Any(c => c.Weight <= 0m))
                return ServiceResult<ElementInputTagDto>.Failure(
                    ApiStatusCodes.BadRequest, "Weight của mỗi hành phải > 0.");
            if (contributions.Select(c => c.Element).Distinct().Count() != contributions.Count)
                return ServiceResult<ElementInputTagDto>.Failure(
                    ApiStatusCodes.BadRequest, "Mỗi hành chỉ được khai 1 lần trong cùng tag.");
        }

        var label = string.IsNullOrWhiteSpace(request.LabelVi)
            ? rows.Select(r => r.LabelVi).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))
            : request.LabelVi.Trim();

        var byElement = rows.ToDictionary(r => r.Element);

        if (request.Contributions is { Count: > 0 } wanted)
        {
            foreach (var c in wanted)
            {
                if (byElement.TryGetValue(c.Element, out var row))
                {
                    row.Weight = c.Weight;
                    row.LabelVi = label;
                    if (request.Visibility is { } vis) row.Visibility = vis;
                    _inputMap.Update(row);
                }
                else
                {
                    await _inputMap.AddAsync(new ElementInputMap
                    {
                        InputKind = kind,
                        InputCode = code,
                        LabelVi = label,
                        // Hành thêm mới phải theo phạm vi của chính tag, không rơi về Pending.
                        Visibility = request.Visibility ?? rows[0].Visibility,
                        Element = c.Element,
                        Weight = c.Weight,
                    }, ct);
                }
            }

            // Hành bị admin bỏ khỏi danh sách → xóa (soft-delete qua Remove của repository).
            var keep = wanted.Select(c => c.Element).ToHashSet();
            foreach (var row in rows.Where(r => !keep.Contains(r.Element)))
                _inputMap.Remove(row);
        }
        else
        {
            // Chỉ đổi nhãn / phạm vi hiển thị, giữ nguyên phân bổ hành.
            foreach (var row in rows)
            {
                row.LabelVi = label;
                if (request.Visibility is { } vis) row.Visibility = vis;
                _inputMap.Update(row);
            }
        }

        await _uow.SaveChangesAsync(ct);

        var updated = await _inputMap.FindAsync(x => x.InputKind == kind && x.InputCode == code, ct);
        var group = updated.GroupBy(r => (r.InputKind, r.InputCode)).FirstOrDefault();
        if (group is null)
            return ServiceResult<ElementInputTagDto>.Failure(
                ApiStatusCodes.NotFound, "Tag không còn hành nào sau khi cập nhật.");

        return ServiceResult<ElementInputTagDto>.Success(ToTagDto(group), "Cập nhật tag thành công.");
    }

    public async Task<IServiceResult> DeleteElementInputTagAsync(
        ElementInputKind kind, string code, CancellationToken ct = default)
    {
        var rows = await _inputMap.FindAsync(x => x.InputKind == kind && x.InputCode == code, ct);
        if (rows.Count == 0) return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy tag.");

        foreach (var row in rows) _inputMap.Remove(row);
        await _uow.SaveChangesAsync(ct);

        // Lưu ý: workspace_profile_inputs đang trỏ tới code này sẽ không resolve ra hành nào nữa
        // → tag đó lặng lẽ mất khỏi vector hiện trạng của phòng (không lỗi). Xóa tag phổ biến cần cân nhắc.
        return ServiceResult.Success("Đã xóa tag.");
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

    // ── occupations (P5) ──

    public async Task<IServiceResult<List<OccupationAdminDto>>> GetOccupationsAsync(bool includeInactive, CancellationToken ct = default)
    {
        var rows = await _uow.ScoringConfig.GetOccupationsAsync(includeInactive, ct);
        return ServiceResult<List<OccupationAdminDto>>.Success(rows.Select(ToDto).ToList());
    }

    /// <summary>
    /// Tạo mới (truyền <paramref name="code"/> = null, lấy mã từ body) hoặc sửa tên/mô tả/trạng thái
    /// của một nghề. <b>Không đụng tới delta</b> — delta đi đường riêng vì nó là phát biểu phong thủy,
    /// không phải nhãn hiển thị.
    /// </summary>
    public async Task<IServiceResult<OccupationAdminDto>> UpsertOccupationAsync(
        string? code, UpsertOccupationRequest request, CancellationToken ct = default)
    {
        string? targetCode = code ?? request.Code;
        if (string.IsNullOrWhiteSpace(targetCode))
            return ServiceResult<OccupationAdminDto>.Failure(ApiStatusCodes.BadRequest, "Thiếu mã nghề.");
        if (string.IsNullOrWhiteSpace(request.NameVi))
            return ServiceResult<OccupationAdminDto>.Failure(ApiStatusCodes.BadRequest, "Thiếu tên nghề.");

        targetCode = targetCode.Trim();
        var existing = (await _occupations.FindAsync(o => o.Code == targetCode, ct)).FirstOrDefault();

        if (existing is null)
        {
            if (code is not null)
                return ServiceResult<OccupationAdminDto>.Failure(ApiStatusCodes.NotFound, $"Không tìm thấy nghề '{targetCode}'.");

            existing = new Occupation
            {
                Code = targetCode,
                NameVi = request.NameVi.Trim(),
                Description = request.Description,
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
                IsSystemSeeded = false,
            };
            await _occupations.AddAsync(existing, ct);
        }
        else
        {
            existing.NameVi = request.NameVi.Trim();
            existing.Description = request.Description;
            existing.IsActive = request.IsActive;
            existing.SortOrder = request.SortOrder;
            _occupations.Update(existing);
        }

        await _uow.SaveChangesAsync(ct);
        var saved = await _uow.ScoringConfig.GetOccupationByCodeAsync(targetCode, ct);
        return ServiceResult<OccupationAdminDto>.Success(ToDto(saved!), "Lưu nghề thành công.");
    }

    /// <summary>
    /// Ghi đè TRỌN GÓI bảng delta của một nghề.
    ///
    /// <para>
    /// Chặn <c>|delta| &gt; 1</c>: <c>r</c> vốn nằm trong [−1, 1] nên delta lớn hơn thế chỉ có thể ép
    /// mọi hành về biên, biến tham số <c>OCCUPATION_SHARE</c> thành công tắc bật/tắt thay vì một núm
    /// hiệu chỉnh. Việc chặn hành khắc mệnh thì nằm trong engine, không đặt ở đây — dữ liệu được phép
    /// khai ý định, engine mới là nơi cưỡng chế kiêng kỵ.
    /// </para>
    /// </summary>
    public async Task<IServiceResult<OccupationAdminDto>> ReplaceOccupationModifiersAsync(
        string code, ReplaceOccupationModifiersRequest request, CancellationToken ct = default)
    {
        var occupation = await _uow.ScoringConfig.GetOccupationByCodeAsync(code, ct);
        if (occupation is null)
            return ServiceResult<OccupationAdminDto>.Failure(ApiStatusCodes.NotFound, $"Không tìm thấy nghề '{code}'.");

        var rows = request.Modifiers ?? new List<OccupationModifierInput>();
        if (rows.Select(m => m.Element).Distinct().Count() != rows.Count)
            return ServiceResult<OccupationAdminDto>.Failure(ApiStatusCodes.BadRequest, "Mỗi hành chỉ được khai một lần.");
        if (rows.Any(m => Math.Abs(m.Delta) > 1m))
            return ServiceResult<OccupationAdminDto>.Failure(ApiStatusCodes.BadRequest, "Delta phải nằm trong [-1, 1].");

        var existing = await _occupationModifiers.FindAsync(m => m.OccupationId == occupation.Id, ct);
        foreach (var row in existing) _occupationModifiers.Remove(row);

        foreach (var row in rows.Where(m => m.Delta != 0m))
            await _occupationModifiers.AddAsync(new OccupationElementModifier
            {
                OccupationId = occupation.Id,
                Element = row.Element,
                Delta = row.Delta,
            }, ct);

        await _uow.SaveChangesAsync(ct);
        var saved = await _uow.ScoringConfig.GetOccupationByCodeAsync(code, ct);
        return ServiceResult<OccupationAdminDto>.Success(ToDto(saved!), "Đã lưu bảng delta của nghề.");
    }

    /// <summary>
    /// Xóa mềm một nghề. <b>Chặn khi còn user đang chọn nghề đó</b> — FK là <c>Restrict</c>, xóa mềm
    /// mà vẫn để user trỏ tới sẽ đẻ ra một hồ sơ trỏ vào bản ghi vô hình. Muốn ẩn khỏi danh sách chọn
    /// thì dùng <c>isActive = false</c>.
    /// </summary>
    public async Task<IServiceResult> DeleteOccupationAsync(string code, CancellationToken ct = default)
    {
        var occupation = (await _occupations.FindAsync(o => o.Code == code, ct)).FirstOrDefault();
        if (occupation is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, $"Không tìm thấy nghề '{code}'.");

        var inUse = (await _users.FindAsync(u => u.OccupationId == occupation.Id, ct)).Count;
        if (inUse > 0)
            return ServiceResult.Failure(ApiStatusCodes.BadRequest,
                $"Còn {inUse} người dùng đang chọn nghề này. Đặt isActive = false để ẩn khỏi danh sách chọn thay vì xóa.");

        var modifiers = await _occupationModifiers.FindAsync(m => m.OccupationId == occupation.Id, ct);
        foreach (var row in modifiers) _occupationModifiers.Remove(row);
        _occupations.Remove(occupation);

        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã xóa nghề.");
    }

    private static OccupationAdminDto ToDto(Occupation o) => new()
    {
        Id = o.Id,
        Code = o.Code,
        NameVi = o.NameVi,
        Description = o.Description,
        IsActive = o.IsActive,
        IsSystemSeeded = o.IsSystemSeeded,
        SortOrder = o.SortOrder,
        Modifiers = o.Modifiers
            .OrderBy(m => m.Element)
            .Select(m => new OccupationModifierDto { Element = m.Element.ToString(), Delta = m.Delta })
            .ToList(),
    };

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
        Id = r.Id, InputKind = r.InputKind, InputCode = r.InputCode, LabelVi = r.LabelVi,
        Visibility = r.Visibility, Element = r.Element, Weight = r.Weight,
    };

    /// <summary>Gộp các row cùng (kind, code) thành 1 tag cho admin thao tác.</summary>
    private static ElementInputTagDto ToTagDto(IGrouping<(ElementInputKind Kind, string Code), ElementInputMap> g)
    {
        var rows = g.ToList();
        return new ElementInputTagDto
        {
            InputKind = g.Key.Kind,
            InputCode = g.Key.Code,
            LabelVi = rows.Select(r => r.LabelVi).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? g.Key.Code,
            // Phạm vi của tag = mức THẤP NHẤT trong các hành — tránh tag nửa công khai nửa riêng tư
            // (chỉ xảy ra nếu dữ liệu bị sửa tay ngoài API này).
            Visibility = rows.Min(r => r.Visibility),
            CreatedBy = rows.Select(r => r.CreatedBy).FirstOrDefault(id => id is not null),
            Contributions = rows
                .OrderByDescending(r => r.Weight).ThenBy(r => r.Element)
                .Select(r => new ElementInputTagContributionDto(r.Id, r.Element, r.Weight))
                .ToList(),
            TotalWeight = rows.Sum(r => r.Weight),
            UpdatedAt = rows.Max(r => r.UpdatedAt),
        };
    }

    /// <summary>Áp nhãn cho mọi hành khác của cùng code (trừ row vừa xử lý).</summary>
    private void ApplyLabelToSiblings(List<ElementInputMap> siblings, ElementInputMap current, string label)
    {
        foreach (var row in siblings.Where(r => r.Id != current.Id && r.LabelVi != label))
        {
            row.LabelVi = label;
            _inputMap.Update(row);
        }
    }

    private static WorkPurposeModifierDto ToDto(WorkPurposeElementModifier r) => new()
    {
        Id = r.Id, WorkPurpose = r.WorkPurpose, Element = r.Element, Delta = r.Delta,
    };

    private static WorkspaceTypeElementDto ToDto(WorkspaceTypeElement r) => new()
    {
        Id = r.Id, WorkspaceTypeId = r.WorkspaceTypeId, Source = r.Source, Element = r.Element, Weight = r.Weight,
    };
}
