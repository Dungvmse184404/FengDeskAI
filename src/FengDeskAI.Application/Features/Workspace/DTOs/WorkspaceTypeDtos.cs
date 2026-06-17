namespace FengDeskAI.Application.Features.Workspace.DTOs;

public class WorkspaceTypeResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public decimal PersonalWeight { get; set; }
    public bool IsSystemSeeded { get; set; }
}

public class CreateWorkspaceTypeRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }

    /// <summary>Tùy chọn. Bỏ trống → mặc định 1.0 (theo quy tắc: type tự thêm mặc định 1.0). Kẹp [0, 1].</summary>
    public decimal? PersonalWeight { get; set; }
}
