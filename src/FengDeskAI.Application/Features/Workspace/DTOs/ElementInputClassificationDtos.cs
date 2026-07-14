using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.Workspace.DTOs;

/// <summary>Người dùng gõ tên 1 tag mới (chưa có trong element_input_map) — AI phân loại thành hành + weight.</summary>
public sealed record ClassifyElementInputRequest(ElementInputKind Kind, string Label);

public sealed record ElementContributionDto(FengShuiElement Element, decimal Weight);

/// <summary>
/// Kết quả phân loại — đã qua chuẩn hoá deterministic (clamp/normalize weight), sẵn sàng lưu
/// vào element_input_map và dùng ngay làm input đã chọn cho workspace hiện tại.
/// </summary>
public sealed record ClassifyElementInputResponse(string Code, List<ElementContributionDto> Elements);
