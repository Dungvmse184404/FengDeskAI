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
    public FengShuiElement Element { get; init; }
    public decimal Weight { get; init; }
}

public sealed record UpsertElementInputMapRequest
{
    public ElementInputKind InputKind { get; init; }
    public string InputCode { get; init; } = null!;
    public FengShuiElement Element { get; init; }
    public decimal Weight { get; init; } = 1.0m;
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
