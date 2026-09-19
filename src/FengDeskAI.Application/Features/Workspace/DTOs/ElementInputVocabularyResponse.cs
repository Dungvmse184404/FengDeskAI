namespace FengDeskAI.Application.Features.Workspace.DTOs;

/// <summary>Một tag chọn được ở bước intake: <paramref name="Code"/> để lưu, <paramref name="LabelVi"/> để hiện.</summary>
public sealed record ElementInputOptionDto(string Code, string LabelVi);

public sealed record ElementInputVocabularyResponse(
    List<ElementInputOptionDto> Colors,
    List<ElementInputOptionDto> Materials,
    List<ElementInputOptionDto> Shapes,
    List<ElementInputOptionDto> DecorItems);
