using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.Workspace.DTOs;

/// <summary>Mô tả không gian bằng lời (10..2000 ký tự) — AI phân tích thành draft, KHÔNG lưu DB.</summary>
public sealed class ParseWorkspaceDescriptionRequest
{
    public string Description { get; set; } = null!;
}

/// <summary>
/// Draft AI intake — mọi field nullable (null = AI không suy ra được, KHÔNG được đoán bừa).
/// Chỉ để prefill form; lưu thật vẫn đi qua Create/Update sẵn có (validate 1 nơi duy nhất).
/// </summary>
public sealed class WorkspaceProfileDraftResponse
{
    public string? Name { get; set; }
    public LocationType? LocationType { get; set; }
    public Guid? WorkspaceTypeId { get; set; }
    public string? StyleCode { get; set; }
    public LightingType? Lighting { get; set; }
    public DeskType? DeskType { get; set; }
    public CompassDirection? DeskOrientation { get; set; }
    public CompassDirection? RoomFacingDirection { get; set; }
    public WorkPurpose? WorkPurpose { get; set; }
    public int? DeskArea { get; set; }

    /// <summary>Input codes hợp lệ cho workspace_profile_inputs (màu/vật liệu nhận ra được).</summary>
    public List<WorkspaceProfileInputDto> Inputs { get; set; } = new();

    /// <summary>0..1 — mức tự tin tổng thể của lượt parse (FE hiện badge).</summary>
    public decimal Confidence { get; set; }

    /// <summary>Chi tiết user nhắc đến nhưng hệ thống không map được — FE hiện để user tự xử lý.</summary>
    public List<string> Unrecognized { get; set; } = new();
}
