using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.DTOs;

// ── scoring_params ──
public sealed record ScoringParamDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public decimal Value { get; init; }
    public string? Description { get; init; }
}

public sealed record UpsertScoringParamRequest
{
    public decimal Value { get; init; }
    public string? Description { get; init; }
}

// ── element_input_map ──
public sealed record ElementInputMapDto
{
    public Guid Id { get; init; }
    public ElementInputKind InputKind { get; init; }
    public string InputCode { get; init; } = null!;
    /// <summary>Nhãn tiếng Việt hiển thị cho user — dùng chung cho mọi hành của cùng 1 code.</summary>
    public string? LabelVi { get; init; }
    public ElementInputVisibility Visibility { get; init; }
    public FengShuiElement Element { get; init; }
    public decimal Weight { get; init; }
}

public sealed record UpsertElementInputMapRequest
{
    public ElementInputKind InputKind { get; init; }
    public string InputCode { get; init; } = null!;
    /// <summary>Bỏ trống = giữ nhãn hiện tại. Có giá trị = áp cho TẤT CẢ hành của code này.</summary>
    public string? LabelVi { get; init; }
    public FengShuiElement Element { get; init; }
    public decimal Weight { get; init; } = 1.0m;
}

// ── element_input_map: view gộp theo TAG (đơn vị admin thực sự thao tác) ──

/// <summary>
/// Một tag = 1 cặp (kind, code), gồm nhiều row (mỗi hành 1 row). Admin sửa theo tag chứ không sửa
/// từng row rời — nếu không, nhãn tiếng Việt của cùng 1 code dễ lệch nhau giữa các hành.
/// </summary>
public sealed record ElementInputTagDto
{
    public ElementInputKind InputKind { get; init; }
    public string InputCode { get; init; } = null!;
    public string LabelVi { get; init; } = null!;

    /// <summary>`Pending` (chờ admin xem) · `Personal` (đã xem, giữ riêng) · `Public` (tag chính thức).</summary>
    public ElementInputVisibility Visibility { get; init; }

    /// <summary>null = tag seed hệ thống; có giá trị = tag do user tự tạo ở bước intake.</summary>
    public Guid? CreatedBy { get; init; }

    /// <summary>true khi tag do user tự tạo (FE lọc "chờ duyệt" / "do user tạo").</summary>
    public bool IsUserCreated => CreatedBy is not null;

    /// <summary>true khi tag còn nằm trong hàng đợi chờ admin xem.</summary>
    public bool IsPending => Visibility == ElementInputVisibility.Pending;

    public List<ElementInputTagContributionDto> Contributions { get; init; } = new();

    /// <summary>
    /// Σ weight = số "phiếu" tag này bỏ vào vector hiện trạng phòng. Chuẩn là <b>1.0</b>;
    /// khác 1.0 nghĩa là tag được cố ý cho nặng/nhẹ hơn tag khác — FE cảnh báo để admin biết.
    /// </summary>
    public decimal TotalWeight { get; init; }

    public DateTime UpdatedAt { get; init; }
}

public sealed record ElementInputTagContributionDto(Guid Id, FengShuiElement Element, decimal Weight);

/// <summary>Sửa trọn 1 tag trong một lần: nhãn + toàn bộ phân bổ hành (thay thế, không cộng dồn).</summary>
public sealed record UpdateElementInputTagRequest
{
    public string? LabelVi { get; init; }

    /// <summary>
    /// Danh sách hành + weight MỚI của tag. Bỏ trống = giữ nguyên phân bổ hiện tại (chỉ sửa nhãn).
    /// Hành có trong DB mà không có ở đây sẽ bị xóa.
    /// </summary>
    public List<UpdateElementInputTagContribution>? Contributions { get; init; }

    /// <summary>
    /// Bỏ trống = giữ nguyên. `Public` = duyệt thành tag chung · `Personal` = đã xem, giữ riêng cho
    /// người tạo (ra khỏi hàng đợi) · `Pending` = trả lại hàng đợi.
    /// </summary>
    public ElementInputVisibility? Visibility { get; init; }
}

public sealed record UpdateElementInputTagContribution
{
    public FengShuiElement Element { get; init; }
    public decimal Weight { get; init; }
}

// ── work_purpose_element_modifiers ──
public sealed record WorkPurposeModifierDto
{
    public Guid Id { get; init; }
    public WorkPurpose WorkPurpose { get; init; }
    public FengShuiElement Element { get; init; }
    public decimal Delta { get; init; }
}

public sealed record UpsertWorkPurposeModifierRequest
{
    public WorkPurpose WorkPurpose { get; init; }
    public FengShuiElement Element { get; init; }
    public decimal Delta { get; init; }
}

// ── workspace_type_elements ──
public sealed record WorkspaceTypeElementDto
{
    public Guid Id { get; init; }
    public Guid WorkspaceTypeId { get; init; }
    public string Source { get; init; } = null!;
    public FengShuiElement Element { get; init; }
    public decimal Weight { get; init; }
}

public sealed record UpsertWorkspaceTypeElementRequest
{
    public Guid WorkspaceTypeId { get; init; }
    public string Source { get; init; } = null!;   // Ideal | Interior
    public FengShuiElement Element { get; init; }
    public decimal Weight { get; init; }
}
