namespace FengDeskAI.Application.Features.Workspace;

/// <summary>
/// Cấu hình AI cho workspace intake / autofill (bind từ section "Ai:Intake").
/// Tác vụ trích xuất JSON có cấu trúc → cần output deterministic: temperature thấp + JSON mode.
/// </summary>
public sealed class WorkspaceIntakeOptions
{
    public const string SectionName = "Ai:Intake";

    /// <summary>Model dùng cho intake (trích xuất field từ mô tả tự do).</summary>
    public string Model { get; set; } = "gemma3:4b";

    /// <summary>Temperature — mặc định 0 để output ổn định/deterministic. null = theo mặc định model.</summary>
    public double? Temperature { get; set; } = 0;

    /// <summary>Ép model trả JSON hợp lệ (Ollama format="json").</summary>
    public bool JsonMode { get; set; } = true;
}
