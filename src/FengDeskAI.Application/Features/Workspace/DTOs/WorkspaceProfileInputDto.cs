using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.Workspace.DTOs;

/// <summary>
/// Một tín hiệu màu/vật liệu/hình khối của workspace (khớp <c>element_input_map</c>).
/// Dùng cả khi tạo/sửa profile (Inputs của Create/Update request) lẫn trong draft AI intake.
/// </summary>
public sealed record WorkspaceProfileInputDto(ElementInputKind InputKind, string InputCode);
