using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Interfaces.Repositories;

/// <summary>
/// Đọc dữ liệu cấu hình cho engine chấm điểm v3 (tham số, map ngũ hành, vector loại phòng,
/// modifier intent, input phòng/sản phẩm). Chỉ đọc — seed &amp; admin CRUD nằm nơi khác.
/// </summary>
public interface IScoringConfigRepository
{
    Task<List<ScoringParam>> GetScoringParamsAsync(CancellationToken ct = default);
    Task<List<ElementInputMap>> GetElementInputMapAsync(CancellationToken ct = default);
    Task<List<WorkspaceTypeElement>> GetWorkspaceTypeElementsAsync(Guid workspaceTypeId, CancellationToken ct = default);
    Task<List<WorkPurposeElementModifier>> GetWorkPurposeModifiersAsync(WorkPurpose purpose, CancellationToken ct = default);
    Task<List<WorkspaceProfileInput>> GetWorkspaceProfileInputsAsync(Guid workspaceProfileId, CancellationToken ct = default);
    Task<List<ProductElementInput>> GetProductElementInputsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);

    /// <summary>Thay toàn bộ input (màu/vật liệu/hình khối) của 1 sản phẩm. Chưa commit — caller lưu qua UoW.</summary>
    Task ReplaceProductElementInputsAsync(Guid productId, IEnumerable<ProductElementInput> inputs, CancellationToken ct = default);
}
