namespace FengDeskAI.Domain.Enums.Workspace;

/// <summary>
/// Mức độ riêng tư của không gian — quyết định bộ lọc mệnh là "hard" (loại) hay "soft" (trừ điểm)
/// trong engine chấm điểm v3. Private = bàn cá nhân; Shared = phòng họp; Public = sảnh/lễ tân.
/// </summary>
public enum WorkspaceScope
{
    Private,
    Shared,
    Public,
}
